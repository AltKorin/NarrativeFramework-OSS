"""Public test accounts: CSRF protection, bounded fields and ordinary user privileges."""

import re
from django.conf import settings
from django.contrib.auth import get_user_model, login
from django.contrib.auth.password_validation import validate_password
from django.core.exceptions import ValidationError
from django.db import IntegrityError, transaction
from django.middleware.csrf import CsrfViewMiddleware, get_token
from rest_framework.decorators import api_view, permission_classes, throttle_classes
from rest_framework.permissions import AllowAny
from rest_framework.response import Response
from rest_framework.throttling import SimpleRateThrottle


class RegistrationThrottle(SimpleRateThrottle):
    scope = "registration"

    def get_cache_key(self, request, view):
        return self.cache_format % {
            "scope": self.scope,
            "ident": self.get_ident(request),
        }


@api_view(["POST"])
@permission_classes([AllowAny])
@throttle_classes([RegistrationThrottle])
def register(request):
    if not settings.ALLOW_REGISTRATION:
        return Response({"error": "Registration is disabled"}, status=403)
    if request.user.is_authenticated:
        return Response(
            {"error": "Log out before creating another account"}, status=409
        )
    # DRF skips anonymous CSRF checks; signup creates a session and must enforce them.
    denial = CsrfViewMiddleware(lambda r: None).process_view(
        request._request, lambda r: None, (), {}
    )
    if denial:
        return Response({"error": "CSRF check failed"}, status=403)
    if not isinstance(request.data, dict):
        return Response({"error": "Expected a JSON object"}, status=400)
    username = request.data.get("username")
    password = request.data.get("password")
    confirmation = request.data.get("password_confirm")
    if not isinstance(username, str) or not re.fullmatch(
        r"[A-Za-z0-9_.-]{3,32}", username
    ):
        return Response(
            {"error": "Use 3–32 letters, numbers, dots, underscores or hyphens"},
            status=400,
        )
    if (
        not isinstance(password, str)
        or not isinstance(confirmation, str)
        or len(password) > 4096
    ):
        return Response({"error": "Invalid password fields"}, status=400)
    if password != confirmation:
        return Response({"error": "Passwords do not match"}, status=400)
    User = get_user_model()
    candidate = User(username=username)
    try:
        validate_password(password, candidate)
    except ValidationError:
        return Response(
            {
                "error": "Use at least 12 characters and avoid common, numeric or username-like passwords"
            },
            status=400,
        )
    try:
        with transaction.atomic():
            user = User.objects.create_user(username=username, password=password)
    except IntegrityError:
        return Response({"error": "This username is already taken"}, status=409)
    login(request, user, backend="django.contrib.auth.backends.ModelBackend")
    return Response(
        {
            "user": user.username,
            "csrf": get_token(request),
            "registration_enabled": True,
        },
        status=201,
    )
