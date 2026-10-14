"""Attack-shaped regression cases. Use test databases, never real campaigns."""

import asyncio
import copy
import hashlib
import json
import time
import uuid
from concurrent.futures import ThreadPoolExecutor
from threading import Barrier, Event, current_thread
from types import SimpleNamespace
from unittest import skipUnless
from unittest.mock import patch

from django.contrib.auth import get_user_model
from django.core.cache import cache
from django.db import connection, close_old_connections
from django.test import TestCase, TransactionTestCase, SimpleTestCase
from rest_framework.authtoken.models import Token
from rest_framework.test import APIClient

from . import play
from .demo import DOCUMENT
from .domain import DomainError
from .models import Scenario, PlaySession
from .test_play import assignment, turn


class AdversarialInputTests(TestCase):
    def setUp(self):
        cache.clear()
        self.user = get_user_model().objects.create_user("adversarial-author")
        self.client = APIClient(raise_request_exception=False)
        self.client.force_authenticate(self.user)

    def tearDown(self):
        cache.clear()

    def post_scenario(self, document=None, title="Attack fixture", creation_id=None):
        body = {
            "id": creation_id or str(uuid.uuid4()),
            "title": title,
            "document": document if document is not None else copy.deepcopy(DOCUMENT),
        }
        return self.client.post(
            "/api/scenarios/", json.dumps(body), content_type="application/json"
        )

    def test_hostile_storage_strings_return_400_without_writes(self):
        for field in ("title", "text", "secret", "id"):
            for value in ("nul\x00inside", "unpaired\ud800"):
                with self.subTest(field=field, value=repr(value)):
                    document = copy.deepcopy(DOCUMENT)
                    document["nodes"][0][field] = value
                    before = Scenario.objects.count()
                    response = self.post_scenario(document)
                    self.assertEqual(response.status_code, 400)
                    self.assertEqual(Scenario.objects.count(), before)
        for title in ("Title\x00", "Title\udfff"):
            with self.subTest(title=repr(title)):
                self.assertEqual(self.post_scenario(title=title).status_code, 400)

    def test_large_integer_coordinate_returns_400(self):
        doc = copy.deepcopy(DOCUMENT)
        doc["nodes"][0]["x"] = 10**1000
        self.assertEqual(self.post_scenario(doc).status_code, 400)

    def test_nested_json_returns_400_not_recursion_500(self):
        raw = (
            '{"id":"'
            + str(uuid.uuid4())
            + '","title":"Nested","document":'
            + ("[" * 1200)
            + "0"
            + ("]" * 1200)
            + "}"
        )
        self.assertEqual(
            self.client.post(
                "/api/scenarios/", raw, content_type="application/json"
            ).status_code,
            400,
        )

    def test_unknown_metadata_cannot_hide_bad_strings_or_nonfinite_numbers(self):
        for value in ("metadata\x00", "metadata\ud800", float("inf")):
            doc = copy.deepcopy(DOCUMENT)
            doc["metadata"] = {"value": value}
            with self.subTest(value=repr(value)):
                self.assertEqual(self.post_scenario(doc).status_code, 400)

    def test_optional_node_fields_are_canonical_for_the_editor(self):
        doc = copy.deepcopy(DOCUMENT)
        for n in doc["nodes"]:
            for field in ("text", "secret", "x", "y"):
                n.pop(field, None)
        response = self.post_scenario(doc)
        self.assertEqual(response.status_code, 201)
        for n in response.json()["document"]["nodes"]:
            self.assertEqual(n["text"], "")
            self.assertEqual(n["secret"], "")
            self.assertEqual(n["x"], 0)
            self.assertEqual(n["y"], 0)

    def test_changed_create_retry_is_a_conflict_not_silent_success(self):
        creation_id = str(uuid.uuid4())
        self.assertEqual(self.post_scenario(creation_id=creation_id).status_code, 201)
        self.assertEqual(
            self.post_scenario(
                title="Changed request", creation_id=creation_id
            ).status_code,
            409,
        )
        doc = copy.deepcopy(DOCUMENT)
        doc["nodes"][0]["text"] = "Different document"
        self.assertEqual(
            self.post_scenario(document=doc, creation_id=creation_id).status_code, 409
        )
        self.assertEqual(Scenario.objects.get(pk=creation_id).title, "Attack fixture")

    def test_exact_create_retry_still_works_after_later_edits(self):
        creation_id = str(uuid.uuid4())
        self.assertEqual(self.post_scenario(creation_id=creation_id).status_code, 201)
        response = self.client.put(
            f"/api/scenarios/{creation_id}/",
            {"title": "Later edit", "document": DOCUMENT, "version": 1},
            format="json",
        )
        self.assertEqual(response.status_code, 200)
        replay = self.post_scenario(creation_id=creation_id)
        self.assertEqual(replay.status_code, 200)
        self.assertEqual(replay.json()["title"], "Later edit")

    def test_login_objects_are_bad_requests_not_server_errors(self):
        client = APIClient(raise_request_exception=False)
        for field in ("username", "password"):
            for value in ({"nested": "x"}, ["x"], True, 42, None):
                cache.clear()
                body = {"username": "absent", "password": "wrong", field: value}
                with self.subTest(field=field, value=value):
                    self.assertEqual(
                        client.post("/api/session/", body, format="json").status_code,
                        400,
                    )

    def test_health_probe_does_not_exhaust_user_throttle(self):
        client = APIClient()
        for _ in range(40):
            self.assertEqual(client.get("/api/health/").status_code, 200)

    def test_dependency_failures_are_503_and_health_does_not_need_cache(self):
        from redis.exceptions import ConnectionError as RedisConnectionError

        with patch(
            "django.core.cache.backends.locmem.LocMemCache.get",
            side_effect=RedisConnectionError("fixture unavailable"),
        ):
            self.assertEqual(self.client.get("/api/scenarios/").status_code, 503)
            self.assertEqual(self.client.get("/api/health/").status_code, 200)


@skipUnless(
    connection.vendor == "postgresql",
    "Storage and concurrency attacks require PostgreSQL",
)
class AdversarialPostgresTests(TransactionTestCase):
    def setUp(self):
        cache.clear()
        self.gm = get_user_model().objects.create_user("attack-gm")
        self.player = get_user_model().objects.create_user("attack-player")
        self.source = Scenario.objects.create(
            owner=self.gm, title="Attack source", document=DOCUMENT
        )
        self.play_id = play.create_play(self.gm, assignment(self.source, self.player))[
            "id"
        ]
        raw = play.issue_access(self.player, self.play_id)["token"]
        self.digest = hashlib.sha256(raw.encode()).hexdigest()

    def tearDown(self):
        cache.clear()

    def test_nul_in_turn_rolls_back_before_database_error(self):
        before = PlaySession.objects.get(pk=self.play_id)
        try:
            play.commit_turn(
                self.player,
                self.play_id,
                self.digest,
                str(uuid.uuid4()),
                1,
                turn(narration="nul\x00"),
            )
        except DomainError as error:
            self.assertEqual(error.status, 400)
        else:
            self.fail("NUL must be a domain error")
        current = PlaySession.objects.get(pk=self.play_id)
        self.assertEqual(current.version, before.version)
        self.assertEqual(current.events.filter(kind="turn").count(), 0)

    def test_concurrent_author_token_rotation_does_not_raise_500(self):
        original_create = Token.objects.create
        barrier = Barrier(4)

        def slow_create(*args, **kwargs):
            # Simulated DB latency widens the deletion/creation gap without prescribing a fix.
            time.sleep(0.04)
            return original_create(*args, **kwargs)

        def rotate(_):
            close_old_connections()
            try:
                client = APIClient(raise_request_exception=False)
                client.force_authenticate(self.gm)
                barrier.wait(timeout=5)
                response = client.post("/api/token/")
                return response.status_code
            finally:
                close_old_connections()

        with (
            patch.object(Token.objects, "create", side_effect=slow_create),
            ThreadPoolExecutor(max_workers=4) as pool,
        ):
            statuses = list(pool.map(rotate, range(4)))
        self.assertEqual(statuses, [200] * 4)
        self.assertEqual(Token.objects.filter(user=self.gm).count(), 1)

    def test_context_is_not_torn_between_version_memory_and_journal(self):
        loaded = Event()
        resume = Event()
        original = play.check_grant

        def pause_after_load(*args, **kwargs):
            result = original(*args, **kwargs)
            if current_thread().name.startswith("adversarial-reader"):
                loaded.set()
                self.assertTrue(resume.wait(timeout=5))
            return result

        def reader():
            close_old_connections()
            try:
                return play.play_context(self.player, self.play_id, self.digest)
            finally:
                close_old_connections()

        def writer():
            close_old_connections()
            try:
                return play.commit_turn(
                    self.player, self.play_id, self.digest, str(uuid.uuid4()), 1, turn()
                )
            finally:
                close_old_connections()

        # An intentionally interleaved read/write must return a coherent version.
        with (
            patch.object(play, "check_grant", side_effect=pause_after_load),
            ThreadPoolExecutor(
                max_workers=1, thread_name_prefix="adversarial-reader"
            ) as readers,
            ThreadPoolExecutor(
                max_workers=1, thread_name_prefix="adversarial-writer"
            ) as writers,
        ):
            future = readers.submit(reader)
            try:
                self.assertTrue(loaded.wait(timeout=5))
                writing = writers.submit(writer)
                # Let an unprotected writer finish; a protected writer may block until resume.
                time.sleep(0.1)
            finally:
                resume.set()
            context = future.result(timeout=5)
            writing.result(timeout=5)
        self.assertTrue(
            all(
                e["version"] <= context["version"]
                for e in context["journal"]["entries"]
            )
        )
        if context["version"] == 1:
            self.assertEqual(context["memory_markdown"], "")
            self.assertEqual(context["journal"]["total"], 0)


class MCPGateAdversarialTests(SimpleTestCase):
    """Deterministic ASGI fault injection; full authentication is covered by real MCP tests."""

    def test_deadlines_malformed_bodies_and_disconnect_release_admission(self):
        from . import mcp_server as server

        class RedisFixture:
            def __init__(self):
                self.released = []

            async def eval(self, *args):
                return 1

            async def zrem(self, *args):
                self.released.append(args)

        async def exercise(kind):
            messages = []
            called = []
            reads = 0

            async def app(scope, receive, send):
                called.append(True)
                if kind == "partial_response":
                    await send(
                        {"type": "http.response.start", "status": 200, "headers": []}
                    )
                    await send(
                        {"type": "http.response.body", "body": b"{", "more_body": True}
                    )
                if kind in ("tool_timeout", "partial_response"):
                    await asyncio.sleep(1)
                else:
                    await send(
                        {"type": "http.response.start", "status": 200, "headers": []}
                    )
                    await send({"type": "http.response.body", "body": b"{}"})

            async def receive():
                nonlocal reads
                reads += 1
                if kind == "disconnect":
                    return {"type": "http.disconnect"}
                if kind == "body_timeout":
                    if reads == 1:
                        return {"type": "http.request", "body": b"{", "more_body": True}
                    await asyncio.sleep(1)
                body = {
                    "deep": b"[" * 1200 + b"0" + b"]" * 1200,
                    "nul": b'{"x":"\\u0000"}',
                    "bad_json": b"{",
                    "oversize": b"x" * 300001,
                }.get(kind, b"{}")
                return {"type": "http.request", "body": body, "more_body": False}

            async def send(message):
                messages.append(message)

            async def authorized(*args):
                return SimpleNamespace(user=SimpleNamespace(pk=1))

            gate = server.Gate(app)
            gate.redis = RedisFixture()
            scope = {
                "type": "http",
                "method": "POST",
                "path": "/mcp",
                "client": ("127.0.0.1", 1234),
                "headers": [(b"authorization", b"Bearer " + b"a" * 40)],
            }
            with (
                patch.object(server, "db_call", new=authorized),
                patch.object(server, "BODY_TIMEOUT_SECONDS", 0.01),
                patch.object(server, "TOOL_TIMEOUT_SECONDS", 0.01),
            ):
                if kind == "partial_response":
                    with self.assertRaises(TimeoutError):
                        await gate(scope, receive, send)
                else:
                    await gate(scope, receive, send)
            self.assertEqual(len(gate.redis.released), 2)
            self.assertIsNone(server.principal.get(None))
            statuses = [
                m["status"] for m in messages if m["type"] == "http.response.start"
            ]
            expected = {
                "body_timeout": [408],
                "tool_timeout": [504],
                "partial_response": [200],
                "disconnect": [],
                "deep": [400],
                "nul": [400],
                "bad_json": [400],
                "oversize": [413],
                "good": [200],
            }
            self.assertEqual(statuses, expected[kind])
            self.assertEqual(
                bool(called), kind in ("tool_timeout", "partial_response", "good")
            )

        for kind in (
            "body_timeout",
            "tool_timeout",
            "partial_response",
            "disconnect",
            "deep",
            "nul",
            "bad_json",
            "oversize",
            "good",
        ):
            with self.subTest(kind=kind):
                asyncio.run(exercise(kind))
