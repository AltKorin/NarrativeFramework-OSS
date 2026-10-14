"""Registration is a security boundary, including when the visitor has no session."""

from django.contrib.auth import get_user_model
from django.core.cache import cache
from django.test import TestCase, override_settings
from rest_framework.test import APIClient


@override_settings(ALLOW_REGISTRATION=True)
class RegistrationTests(TestCase):
    def setUp(self):
        cache.clear()
        self.client = APIClient(enforce_csrf_checks=True)
        session = self.client.get("/api/session/")
        self.headers = {"HTTP_X_CSRFTOKEN": session.json()["csrf"]}
        self.body = {
            "username": "new-writer",
            "password": "A-long-unusual-phrase-74!",
            "password_confirm": "A-long-unusual-phrase-74!",
        }

    def post(self, body=None, **headers):
        return self.client.post(
            "/api/register/",
            body if body is not None else self.body,
            format="json",
            **(headers or self.headers),
        )

    def test_new_user_has_session_and_no_elevated_permissions(self):
        response = self.post({**self.body, "is_staff": True, "is_superuser": True})
        self.assertEqual(response.status_code, 201)
        user = get_user_model().objects.get(username=self.body["username"])
        self.assertFalse(user.is_staff)
        self.assertFalse(user.is_superuser)
        self.assertTrue(user.check_password(self.body["password"]))
        self.assertNotIn("password", response.json())
        self.assertEqual(self.client.get("/api/session/").json()["user"], user.username)
        self.assertEqual(self.client.get("/api/scenarios/").json(), [])

    def test_csrf_and_cross_origin_rejected_without_user_creation(self):
        anonymous = APIClient(enforce_csrf_checks=True)
        self.assertEqual(
            anonymous.post("/api/register/", self.body, format="json").status_code, 403
        )
        self.assertEqual(
            self.post(
                HTTP_X_CSRFTOKEN=self.headers["HTTP_X_CSRFTOKEN"],
                HTTP_ORIGIN="https://hostile.invalid",
            ).status_code,
            403,
        )
        self.assertEqual(get_user_model().objects.count(), 0)

    def test_invalid_fields_and_passwords_have_no_side_effects(self):
        bodies = [
            [],
            {**self.body, "username": ["bad"]},
            {**self.body, "username": "x"},
            {**self.body, "username": "bad name"},
            {**self.body, "password": {}},
            {**self.body, "password_confirm": "different"},
            {
                **self.body,
                "password": "12345678901234",
                "password_confirm": "12345678901234",
            },
            {**self.body, "password": "short", "password_confirm": "short"},
        ]
        for body in bodies:
            with self.subTest(body_type=type(body).__name__):
                cache.clear()
                self.assertEqual(self.post(body).status_code, 400)
        self.assertEqual(get_user_model().objects.count(), 0)

    def test_duplicate_username_conflicts_without_changing_existing_password(self):
        get_user_model().objects.create_user(
            self.body["username"], password="original-secret"
        )
        self.assertEqual(self.post().status_code, 409)
        self.assertTrue(
            get_user_model()
            .objects.get(username=self.body["username"])
            .check_password("original-secret")
        )

    @override_settings(ALLOW_REGISTRATION=False)
    def test_operator_can_close_registration(self):
        self.assertFalse(
            self.client.get("/api/session/").json()["registration_enabled"]
        )
        self.assertEqual(self.post().status_code, 403)
        self.assertEqual(get_user_model().objects.count(), 0)

    def test_registration_throttle_is_distinct_from_login(self):
        for _ in range(5):
            self.assertEqual(self.post({}).status_code, 400)
        self.assertEqual(self.post().status_code, 429)
        self.assertEqual(get_user_model().objects.count(), 0)
        self.assertEqual(self.client.get("/api/session/").status_code, 200)

    def test_authenticated_user_cannot_register_another_account(self):
        existing = get_user_model().objects.create_user("existing")
        self.client.force_login(existing)
        csrf = self.client.get("/api/session/").json()["csrf"]
        self.assertEqual(self.post(HTTP_X_CSRFTOKEN=csrf).status_code, 409)
        self.assertEqual(get_user_model().objects.count(), 1)
