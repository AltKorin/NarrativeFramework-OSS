import copy, uuid
from django.contrib.auth import get_user_model
from django.test import TestCase, Client
from rest_framework.test import APIClient
from .models import Scenario
from .demo import DOCUMENT
from .domain import inspect_document, validate_document, DomainError, save_document


class WorkshopTests(TestCase):
    def setUp(self):
        self.alice = get_user_model().objects.create_user(
            "alice", password="test-password"
        )
        self.bob = get_user_model().objects.create_user("bob", password="test-password")
        self.story = Scenario.objects.create(
            owner=self.alice, title="Adventure", document=DOCUMENT
        )
        self.demo = Scenario.objects.create(
            is_demo=True, title="Demo", document=DOCUMENT
        )
        self.client = APIClient()
        self.client.force_authenticate(self.alice)

    def test_public_demo_only(self):
        anonymous = APIClient()
        self.assertEqual(anonymous.get("/api/demo/").status_code, 200)
        self.assertIn(anonymous.get("/api/scenarios/").status_code, [401, 403])
        self.assertIn(
            anonymous.put(
                f"/api/scenarios/{self.demo.pk}/", {}, format="json"
            ).status_code,
            [401, 403],
        )

    def test_owner_isolation(self):
        self.client.force_authenticate(self.bob)
        for method, path in [("get", ""), ("post", "validate/"), ("get", "export/")]:
            self.assertEqual(
                getattr(self.client, method)(
                    f"/api/scenarios/{self.story.pk}/" + path
                ).status_code,
                404,
            )
        self.assertEqual(
            self.client.put(
                f"/api/scenarios/{self.story.pk}/",
                {"version": 1, "title": "Hack", "document": DOCUMENT},
                format="json",
            ).status_code,
            404,
        )

    def test_compare_and_swap_between_clients(self):
        a = save_document(self.alice, self.story.pk, 1, "Changed", DOCUMENT)
        self.assertEqual(a["version"], 2)
        with self.assertRaises(DomainError) as ctx:
            save_document(self.alice, self.story.pk, 1, "Lost update", DOCUMENT)
        self.assertEqual(ctx.exception.status, 409)
        self.story.refresh_from_db()
        self.assertEqual(self.story.title, "Changed")

    def test_structural_checks(self):
        issues = inspect_document(DOCUMENT)
        self.assertEqual(len(issues), 1)
        self.assertEqual(issues[0]["node"], "ledger")
        doc = copy.deepcopy(DOCUMENT)
        doc["edges"] = [e for e in doc["edges"] if e["target"] != "salt"]
        self.assertTrue(any(i["node"] == "salt" for i in inspect_document(doc)))

    def test_invalid_graph_rejected_without_write(self):
        doc = copy.deepcopy(DOCUMENT)
        doc["edges"][0]["target"] = "absent"
        r = self.client.put(
            f"/api/scenarios/{self.story.pk}/",
            {"version": 1, "title": "Bad", "document": doc},
            format="json",
        )
        self.assertEqual(r.status_code, 400)
        self.story.refresh_from_db()
        self.assertEqual(self.story.version, 1)

    def test_idempotent_creation(self):
        body = {"id": str(uuid.uuid4()), "title": "New", "document": DOCUMENT}
        a = self.client.post("/api/scenarios/", body, format="json")
        b = self.client.post("/api/scenarios/", body, format="json")
        self.assertEqual(a.status_code, 201)
        self.assertEqual(a.data["id"], b.data["id"])
        self.assertEqual(Scenario.objects.filter(pk=body["id"]).count(), 1)

    def test_export_has_secrets_and_schema(self):
        r = self.client.get(f"/api/scenarios/{self.story.pk}/export/")
        self.assertIn("Sekret MG", r.content.decode())
        r = self.client.get(f"/api/scenarios/{self.story.pk}/export/?format=json")
        self.assertEqual(r.json()["document"]["schema"], "narrative-web/1")

    def test_login_requires_csrf(self):
        c = Client(enforce_csrf_checks=True)
        self.assertEqual(
            c.post(
                "/api/session/",
                {"username": "alice", "password": "test-password"},
                content_type="application/json",
            ).status_code,
            403,
        )
        csrf = c.get("/api/session/").json()["csrf"]
        self.assertEqual(
            c.post(
                "/api/session/",
                {"username": "alice", "password": "test-password"},
                content_type="application/json",
                HTTP_X_CSRFTOKEN=csrf,
            ).status_code,
            200,
        )

    def test_token_rotation_revokes_old_token(self):
        a = self.client.post("/api/token/").data["token"]
        b = self.client.post("/api/token/").data["token"]
        self.assertNotEqual(a, b)
        c = APIClient()
        c.credentials(HTTP_AUTHORIZATION="Token " + a)
        self.assertIn(c.get("/api/scenarios/").status_code, [401, 403])

    def test_document_bounds(self):
        doc = copy.deepcopy(DOCUMENT)
        doc["nodes"][0]["text"] = "x" * 12001
        with self.assertRaises(DomainError):
            validate_document(doc)

    def test_malformed_values_are_validation_errors(self):
        mutations = [
            ("kind", []),
            ("kind", {}),
            ("x", "bad"),
            ("x", float("inf")),
            ("x", True),
        ]
        for key, value in mutations:
            doc = copy.deepcopy(DOCUMENT)
            doc["nodes"][0][key] = value
            with self.subTest(key=key, value=value), self.assertRaises(DomainError):
                validate_document(doc)
        for key in ("source", "target", "kind"):
            doc = copy.deepcopy(DOCUMENT)
            doc["edges"][0][key] = []
            with self.subTest(key=key), self.assertRaises(DomainError):
                validate_document(doc)
        doc = copy.deepcopy(DOCUMENT)
        doc["start"] = {}
        with self.assertRaises(DomainError):
            validate_document(doc)

    def test_mcp_token_cannot_authenticate_rest(self):
        from rest_framework.authtoken.models import Token

        t = Token.objects.create(user=self.alice)
        c = APIClient()
        c.credentials(HTTP_AUTHORIZATION="Token " + t.key)
        self.assertEqual(c.get("/api/scenarios/").status_code, 403)

    def test_non_object_request_is_bad_request(self):
        r = self.client.put(f"/api/scenarios/{self.story.pk}/", [], format="json")
        self.assertEqual(r.status_code, 400)


from django.test import TransactionTestCase
from django.db import connection, close_old_connections
from unittest import skipUnless
from concurrent.futures import ThreadPoolExecutor
from threading import Barrier


@skipUnless(
    connection.vendor == "postgresql", "Concurrent CAS test requires PostgreSQL"
)
class ConcurrentSaveTests(TransactionTestCase):
    def test_exactly_one_writer_wins(self):
        user = get_user_model().objects.create_user("concurrent")
        story = Scenario.objects.create(owner=user, title="Original", document=DOCUMENT)
        barrier = Barrier(2)

        def save(title):
            close_old_connections()
            try:
                barrier.wait(timeout=5)
                try:
                    save_document(user, story.pk, 1, title, DOCUMENT)
                    return 200
                except DomainError as e:
                    return e.status
            finally:
                close_old_connections()

        with ThreadPoolExecutor(max_workers=2) as pool:
            self.assertEqual(sorted(pool.map(save, ["First", "Second"])), [200, 409])
        story.refresh_from_db()
        self.assertEqual(story.version, 2)


class LanguageTests(TestCase):
    def setUp(self):
        self.user = get_user_model().objects.create_user("language-test")
        self.demo = Scenario.objects.create(
            is_demo=True, title="Dzwon pod wodą", document=DOCUMENT
        )
        self.story = Scenario.objects.create(
            owner=self.user, title="Moja przygoda", document=DOCUMENT
        )
        self.client = APIClient()

    def test_demo_languages_share_identity_without_changing_seed(self):
        pl = self.client.get("/api/demo/?lang=pl").json()
        en = self.client.get("/api/demo/?lang=en").json()
        self.assertEqual(pl["id"], en["id"])
        self.assertEqual(pl["document"]["nodes"][0]["title"], "Dzwon pod wodą")
        self.assertEqual(
            en["document"]["nodes"][0]["title"], "The Bell Beneath the Water"
        )
        self.assertEqual(
            self.client.get(
                "/api/demo/", HTTP_ACCEPT_LANGUAGE="en-GB,en;q=0.9"
            ).headers["Content-Language"],
            "en",
        )
        self.demo.refresh_from_db()
        self.assertEqual(self.demo.document, DOCUMENT)

    def test_validation_language_and_stable_codes(self):
        self.client.force_authenticate(self.user)
        pl = self.client.post(
            f"/api/scenarios/{self.story.id}/validate/?lang=pl"
        ).json()["issues"][0]
        en = self.client.post(
            f"/api/scenarios/{self.story.id}/validate/?lang=en"
        ).json()["issues"][0]
        self.assertEqual(en["code"], pl["code"])
        self.assertEqual(en["count"], 2)
        self.assertIn("sources", en["message"])
        self.assertIn("Wskazówka", pl["message"])

    def test_english_markdown_preserves_authored_content(self):
        self.client.force_authenticate(self.user)
        result = self.client.get(f"/api/scenarios/{self.story.id}/export/?lang=en")
        text = result.content.decode()
        self.assertIn("GM material", text)
        self.assertIn("GM secret", text)
        self.assertIn("Dzwon pod wodą", text)


class LoginRateTests(TestCase):
    def test_session_reads_do_not_spend_login_attempt_quota(self):
        from django.core.cache import cache

        cache.clear()
        client = APIClient()
        try:
            for _ in range(15):
                self.assertEqual(client.get("/api/session/").status_code, 200)
            for _ in range(10):
                response = client.post(
                    "/api/session/",
                    {"username": "absent", "password": "wrong"},
                    format="json",
                )
                self.assertEqual(response.status_code, 400)
            self.assertEqual(
                client.post(
                    "/api/session/",
                    {"username": "absent", "password": "wrong"},
                    format="json",
                ).status_code,
                429,
            )
        finally:
            cache.clear()
