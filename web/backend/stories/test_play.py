"""Domain and HTTP regression tests for scoped, retry-safe individual play."""

import copy
import hashlib
import uuid
from concurrent.futures import ThreadPoolExecutor
from threading import Barrier
from unittest import skipUnless

from django.contrib.auth import get_user_model
from django.db import connection, close_old_connections
from django.test import TestCase, TransactionTestCase
from django.utils import timezone
from datetime import timedelta
from rest_framework.test import APIClient

from . import play
from .demo import DOCUMENT
from .domain import DomainError
from .models import Scenario, PlaySession, PlayEvent, PlayConsequence


def assignment(source, player, **overrides):
    return {
        "id": str(uuid.uuid4()),
        "scenario_id": str(source.pk),
        "source_version": source.version,
        "player_username": player.username,
        "mode": "absence",
        "language": "en",
        "character": "Arin the scout",
        "brief": "Find Mira before dawn.",
        "instructions": "Ask what the player does.",
        "boundaries": "No permanent injuries or rare rewards.",
        "return_condition": "Rejoin the party at dawn.",
        "gm_notes": "PRIVATE_GM_NOTE",
        "published_node_ids": ["harbor", "mira"],
        "auto_approve": [],
        **overrides,
    }


def turn(**overrides):
    return {
        "player_action": "I ask Mira about her father.",
        "narration": "Mira asks you to help.",
        "memory_markdown": "# Current state\nMira asked for help.\n",
        "current_scene": "harbor",
        "consequences": [
            {"category": "relationship", "description": "Mira may trust Arin."}
        ],
        **overrides,
    }


class PlayTests(TestCase):
    def setUp(self):
        users = get_user_model()
        self.gm = users.objects.create_user("gm")
        self.player = users.objects.create_user("player")
        self.other = users.objects.create_user("outsider")
        document = copy.deepcopy(DOCUMENT)
        document["nodes"][0]["secret"] = "AUTHOR_SECRET_SENTINEL"
        document["nodes"][0]["extra_secret"] = "EXTRA_SECRET_SENTINEL"
        self.source = Scenario.objects.create(
            owner=self.gm, title="A night away", document=document
        )
        self.data = assignment(self.source, self.player)
        result = play.create_play(self.gm, self.data)
        self.session = PlaySession.objects.get(pk=result["id"])
        self.raw = play.issue_access(self.player, self.session.pk)["token"]
        self.digest = hashlib.sha256(self.raw.encode()).hexdigest()
        self.client = APIClient()

    def commit(self, data=None, op=None, version=1):
        return play.commit_turn(
            self.player,
            self.session.pk,
            self.digest,
            op or str(uuid.uuid4()),
            version,
            data or turn(),
        )

    def test_snapshot_is_immutable_and_filters_unpublished_materials(self):
        context = play.play_context(self.player, self.session.pk, self.digest)
        self.assertEqual(
            {n["id"] for n in context["materials"]["nodes"]}, {"harbor", "mira"}
        )
        self.assertNotIn("AUTHOR_SECRET_SENTINEL", str(context))
        self.assertNotIn("EXTRA_SECRET_SENTINEL", str(context))
        self.assertNotIn("PRIVATE_GM_NOTE", str(context))
        self.source.document["nodes"][0]["text"] = "AFTER_ASSIGNMENT"
        self.source.version = 2
        self.source.save()
        self.assertNotIn(
            "AFTER_ASSIGNMENT",
            str(play.play_context(self.player, self.session.pk, self.digest)),
        )

    def test_creation_retry_and_id_reuse(self):
        retry = play.create_play(self.gm, self.data)
        self.assertEqual(retry["id"], str(self.session.pk))
        self.assertEqual(PlaySession.objects.count(), 1)
        with self.assertRaises(DomainError) as err:
            play.create_play(self.gm, {**self.data, "character": "Changed"})
        self.assertEqual(err.exception.status, 409)

    def test_only_source_owner_can_assign(self):
        with self.assertRaises(DomainError) as err:
            play.create_play(self.player, assignment(self.source, self.player))
        self.assertEqual(err.exception.status, 404)

    def test_creation_checks_version_and_publication(self):
        for changes in (
            {"source_version": 0},
            {"published_node_ids": ["mira"]},
            {"published_node_ids": ["harbor", "absent"]},
            {"auto_approve": ["everything"]},
            {"mode": []},
        ):
            with self.subTest(changes=changes), self.assertRaises(DomainError):
                play.create_play(
                    self.gm, assignment(self.source, self.player, **changes)
                )

    def test_turn_is_atomic_and_retry_is_not_a_second_turn(self):
        op = str(uuid.uuid4())
        first = self.commit(op=op)
        retry = self.commit(op=op)
        self.assertTrue(retry["replayed"])
        self.assertEqual(first["version"], retry["version"])
        self.assertEqual(first["consequences"], retry["consequences"])
        self.session.refresh_from_db()
        self.assertEqual(self.session.version, 2)
        self.assertEqual(self.session.memory_markdown, turn()["memory_markdown"])
        self.assertEqual(self.session.events.filter(kind="turn").count(), 1)
        self.assertEqual(self.session.consequences.count(), 1)
        self.assertEqual(first["consequences"][0]["status"], "pending")
        self.source.refresh_from_db()
        self.assertEqual(self.source.version, 1)

    def test_changed_operation_payload_and_stale_write_fail(self):
        op = str(uuid.uuid4())
        self.commit(op=op)
        for data, operation in [
            (turn(narration="Different"), op),
            (turn(), str(uuid.uuid4())),
        ]:
            with self.assertRaises(DomainError) as err:
                self.commit(data=data, op=operation)
            self.assertEqual(err.exception.status, 409)
        self.assertEqual(self.session.events.filter(kind="turn").count(), 1)

    def test_agent_cannot_choose_approval_status_or_hidden_scene(self):
        for data in (
            turn(current_scene="salt"),
            turn(
                consequences=[
                    {
                        "category": "resource",
                        "description": "Rare sword",
                        "status": "approved",
                    }
                ]
            ),
            turn(memory_markdown="x" * 16001),
        ):
            with self.subTest(data=data), self.assertRaises(DomainError):
                self.commit(data=data)
        self.session.refresh_from_db()
        self.assertEqual(self.session.version, 1)
        self.assertEqual(self.session.consequences.count(), 0)

    def test_preapproval_is_category_based_and_defaults_to_pending(self):
        self.session.auto_approve = ["resource"]
        self.session.save(update_fields=["auto_approve"])
        result = self.commit(
            data=turn(
                consequences=[
                    {"category": "resource", "description": "Spend one copper"},
                    {"category": "discovery", "description": "Learn the hidden ritual"},
                ]
            )
        )
        self.assertEqual(
            [c["status"] for c in result["consequences"]], ["approved", "pending"]
        )

    def test_gm_review_and_idempotency(self):
        effect = self.commit()["consequences"][0]
        op = str(uuid.uuid4())
        decisions = [
            {"id": effect["id"], "decision": "approved", "note": "Fits the campaign."}
        ]
        with self.assertRaises(DomainError) as err:
            play.review_consequences(self.player, self.session.pk, op, 2, decisions)
        self.assertEqual(err.exception.status, 404)
        first = play.review_consequences(self.gm, self.session.pk, op, 2, decisions)
        retry = play.review_consequences(self.gm, self.session.pk, op, 2, decisions)
        self.assertEqual(first["version"], 3)
        self.assertTrue(retry["replayed"])
        self.assertEqual(
            PlayConsequence.objects.get(pk=effect["id"]).status, "approved"
        )

    def test_review_cannot_take_consequences_from_another_session(self):
        effect = self.commit()["consequences"][0]
        another = play.create_play(self.gm, assignment(self.source, self.player))
        with self.assertRaises(DomainError):
            play.review_consequences(
                self.gm,
                another["id"],
                str(uuid.uuid4()),
                1,
                [{"id": effect["id"], "decision": "rejected"}],
            )
        self.assertEqual(PlayConsequence.objects.get(pk=effect["id"]).status, "pending")

    def test_finish_is_retry_safe_and_preserves_pending_decisions(self):
        turn_op = str(uuid.uuid4())
        self.commit(op=turn_op)
        op = str(uuid.uuid4())
        first = play.finish_play(
            self.player, self.session.pk, self.digest, op, 2, "Arin returned at dawn."
        )
        self.assertEqual(first["pending_consequences"], 1)
        self.assertTrue(
            play.finish_play(
                self.player,
                self.session.pk,
                self.digest,
                op,
                2,
                "Arin returned at dawn.",
            )["replayed"]
        )
        self.assertTrue(self.commit(op=turn_op)["replayed"])
        with self.assertRaises(DomainError):
            self.commit(version=3)
        effect = self.session.consequences.first()
        play.review_consequences(
            self.gm,
            self.session.pk,
            str(uuid.uuid4()),
            3,
            [{"id": str(effect.id), "decision": "rejected"}],
        )
        self.assertEqual(
            play.play_detail(self.player, self.session.pk)["status"], "completed"
        )

    def test_rotation_expiry_and_revocation(self):
        rotated = play.issue_access(self.player, self.session.pk)["token"]
        self.assertIsNone(play.resolve_access(self.raw))
        self.assertIsNotNone(play.resolve_access(rotated))
        with self.assertRaises(DomainError):
            play.play_context(self.player, self.session.pk, self.digest)
        self.session.refresh_from_db()
        self.session.access_expires_at = timezone.now() - timedelta(seconds=1)
        self.session.save()
        self.assertIsNone(play.resolve_access(rotated))
        play.issue_access(self.player, self.session.pk, revoke=True)
        self.session.refresh_from_db()
        self.assertIsNone(self.session.access_digest)

    def test_other_users_cannot_read_report_journal_or_issue_tokens(self):
        self.client.force_authenticate(self.other)
        for path, method in [
            ("", "get"),
            ("report/", "get"),
            ("journal/", "get"),
            ("access/", "post"),
        ]:
            self.assertEqual(
                getattr(self.client, method)(
                    f"/api/plays/{self.session.pk}/" + path, {}, format="json"
                ).status_code,
                404,
            )
        self.assertEqual(self.client.get("/api/plays/").json(), [])

    def test_player_detail_and_report_omit_private_gm_notes(self):
        self.client.force_authenticate(self.player)
        detail = self.client.get(f"/api/plays/{self.session.pk}/").json()
        self.assertNotIn("gm_notes", detail)
        player_report = self.client.get(
            f"/api/plays/{self.session.pk}/report/?lang=en"
        ).content.decode()
        self.assertNotIn("PRIVATE_GM_NOTE", player_report)
        self.client.force_authenticate(self.gm)
        gm_report = self.client.get(
            f"/api/plays/{self.session.pk}/report/?lang=pl"
        ).content.decode()
        self.assertIn("PRIVATE_GM_NOTE", gm_report)
        self.assertIn("Prywatne notatki MG", gm_report)

    def test_journal_pagination_and_markdown_are_plain_data(self):
        self.commit(data=turn(narration='<script>alert("x")</script>'))
        result = play.journal(self.session, 0, 1)
        self.assertEqual(result["total"], 1)
        self.assertNotIn("memory_markdown", result["entries"][0])
        self.assertEqual(
            result["entries"][0]["narration"], '<script>alert("x")</script>'
        )
        for offset, limit in [(-1, 10), (0, 21), (True, 1)]:
            with self.assertRaises(DomainError):
                play.journal(self.session, offset, limit)


@skipUnless(
    connection.vendor == "postgresql", "Row-lock concurrency requires PostgreSQL"
)
class ConcurrentPlayTests(TransactionTestCase):
    def test_retry_race_creates_exactly_one_turn(self):
        users = get_user_model()
        gm, player = (
            users.objects.create_user("race-gm"),
            users.objects.create_user("race-player"),
        )
        source = Scenario.objects.create(
            owner=gm, title="Concurrent", document=DOCUMENT
        )
        session_id = play.create_play(gm, assignment(source, player))["id"]
        raw = play.issue_access(player, session_id)["token"]
        digest = hashlib.sha256(raw.encode()).hexdigest()
        barrier = Barrier(2)
        op = str(uuid.uuid4())

        def commit(_):
            close_old_connections()
            try:
                barrier.wait(timeout=5)
                return play.commit_turn(player, session_id, digest, op, 1, turn())
            finally:
                close_old_connections()

        with ThreadPoolExecutor(max_workers=2) as pool:
            results = list(pool.map(commit, range(2)))
        self.assertEqual(sorted(r["replayed"] for r in results), [False, True])
        self.assertEqual(PlaySession.objects.get(pk=session_id).version, 2)
        self.assertEqual(
            PlayEvent.objects.filter(session_id=session_id, kind="turn").count(), 1
        )
