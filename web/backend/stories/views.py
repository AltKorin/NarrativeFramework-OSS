import json
from django.contrib.auth import authenticate, login, logout
from django.http import HttpResponse
from django.db import connection
from django.conf import settings
from django.middleware.csrf import get_token
from rest_framework.decorators import api_view, permission_classes, throttle_classes
from rest_framework.permissions import AllowAny
from rest_framework.response import Response
from rest_framework.throttling import SimpleRateThrottle


class LoginThrottle(SimpleRateThrottle):
    scope = "login"

    def allow_request(self, request, view):
        if request.method != "POST":
            return True
        return super().allow_request(request, view)

    def get_cache_key(self, request, view):
        return self.cache_format % {
            "scope": self.scope,
            "ident": self.get_ident(request),
        }


from rest_framework.authtoken.models import Token
from .models import Scenario
from .domain import (
    DomainError,
    serialize,
    save_document,
    inspect_document,
    markdown,
    validate_document,
    create_scenario,
)


@api_view(["GET"])
@permission_classes([AllowAny])
@throttle_classes([])
def health(request):
    with connection.cursor() as c:
        c.execute("SELECT 1")
    return Response({"status": "ok"})


@api_view(["GET", "POST", "DELETE"])
@permission_classes([AllowAny])
@throttle_classes([LoginThrottle])
def session(request):
    if request.method == "DELETE":
        logout(request)
        return Response({"user": None})
    if request.method == "POST":
        if not isinstance(request.data, dict):
            return Response({"error": "Expected a JSON object"}, status=400)
        # SessionAuthentication does not enforce CSRF for anonymous requests.
        from django.middleware.csrf import CsrfViewMiddleware

        denial = CsrfViewMiddleware(lambda r: None).process_view(
            request._request, lambda r: None, (), {}
        )
        if denial:
            return Response({"error": "CSRF check failed"}, status=403)
        username = request.data.get("username", "")
        password = request.data.get("password", "")
        if (
            not isinstance(username, str)
            or not isinstance(password, str)
            or len(username) > 150
            or len(password) > 4096
        ):
            return Response({"error": "Invalid login fields"}, status=400)
        user = authenticate(
            request,
            username=username,
            password=password,
        )
        if not user:
            return Response({"error": "Nieprawidłowy login lub hasło"}, status=400)
        login(request, user)
    return Response(
        {
            "user": request.user.username if request.user.is_authenticated else None,
            "csrf": get_token(request),
            "registration_enabled": settings.ALLOW_REGISTRATION,
        }
    )


def request_language(request):
    choice = request.query_params.get(
        "lang", request.headers.get("Accept-Language", "pl").split(",")[0]
    ).lower()
    return "en" if choice.startswith("en") else "pl"


@api_view(["GET"])
@permission_classes([AllowAny])
def demo(request):
    s = Scenario.objects.filter(is_demo=True).first()
    if not s:
        return Response({"error": "Run seed_demo"}, status=404)
    result = serialize(s)
    if request_language(request) == "en":
        from .demo import DOCUMENT_EN

        result.update(title="The Bell Beneath the Water", document=DOCUMENT_EN)
    response = Response(result)
    response["Content-Language"] = request_language(request)
    response["Vary"] = "Accept-Language, Cookie"
    return response


@api_view(["GET", "POST"])
def scenarios(request):
    if request.method == "GET":
        return Response(
            list(
                Scenario.objects.filter(owner=request.user, is_demo=False).values(
                    "id", "title", "version"
                )
            )
        )
    if not isinstance(request.data, dict):
        return Response({"error": "Expected a JSON object"}, status=400)
    # Optional client-selected UUID makes retries safe for creation.
    import uuid

    try:
        creation_id = uuid.UUID(request.data.get("id", ""))
    except (ValueError, TypeError, AttributeError):
        return Response(
            {"error": "Provide a UUID id for retry-safe creation"}, status=400
        )
    try:
        result, created = create_scenario(
            request.user,
            creation_id,
            request.data.get("title", ""),
            request.data.get("document"),
        )
        return Response(result, status=201 if created else 200)
    except DomainError as error:
        return Response({"error": str(error)}, status=error.status)


@api_view(["GET", "PUT"])
def scenario(request, pk):
    if request.method == "GET":
        s = Scenario.objects.filter(pk=pk, owner=request.user, is_demo=False).first()
        return (
            Response(serialize(s))
            if s
            else Response({"error": "Not found"}, status=404)
        )
    if not isinstance(request.data, dict):
        return Response({"error": "Expected a JSON object"}, status=400)
    try:
        return Response(
            save_document(
                request.user,
                pk,
                request.data.get("version"),
                request.data.get("title"),
                request.data.get("document"),
            )
        )
    except DomainError as e:
        return Response({"error": str(e)}, status=e.status)


@api_view(["POST"])
def validate(request, pk):
    s = Scenario.objects.filter(pk=pk, owner=request.user, is_demo=False).first()
    if not s:
        return Response({"error": "Not found"}, status=404)
    return Response(
        {
            "version": s.version,
            "issues": inspect_document(s.document, request_language(request)),
        }
    )


@api_view(["GET"])
def export(request, pk):
    s = Scenario.objects.filter(pk=pk, owner=request.user, is_demo=False).first()
    if not s:
        return Response({"error": "Not found"}, status=404)
    is_json = request.query_params.get("format") == "json"
    body = (
        json.dumps(serialize(s), ensure_ascii=False, indent=2)
        if is_json
        else markdown(s, request_language(request))
    )
    response = HttpResponse(
        body,
        content_type="application/json" if is_json else "text/markdown; charset=utf-8",
    )
    response["Content-Disposition"] = (
        'attachment; filename="adventure.' + ("json" if is_json else "md") + '"'
    )
    return response


@api_view(["POST"])
def token(request):
    # Explicit action rotates old token. Never include tokens in general session responses.
    from django.db import transaction
    from django.contrib.auth import get_user_model

    with transaction.atomic():
        get_user_model().objects.select_for_update().get(pk=request.user.pk)
        Token.objects.filter(user=request.user).delete()
        value = Token.objects.create(user=request.user).key
    return Response({"token": value})
