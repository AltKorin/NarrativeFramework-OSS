"""Session-cookie endpoints for assignment, access management and human GM review."""

import functools

from django.http import HttpResponse
from rest_framework.decorators import api_view
from rest_framework.response import Response

from . import play
from .domain import DomainError
from .views import request_language


def endpoint(methods):
    def decorate(fn):
        @api_view(methods)
        @functools.wraps(fn)
        def view(request, *args, **kwargs):
            try:
                if request.method in ("POST", "PUT") and not isinstance(
                    request.data, dict
                ):
                    raise DomainError("Expected a JSON object")
                return fn(request, *args, **kwargs)
            except DomainError as e:
                return Response({"error": str(e)}, status=e.status)

        return view

    return decorate


@endpoint(["GET", "POST"])
def plays(request):
    if request.method == "GET":
        return Response(play.list_plays(request.user))
    return Response(play.create_play(request.user, request.data), status=201)


@endpoint(["GET"])
def detail(request, pk):
    return Response(play.play_detail(request.user, pk))


@endpoint(["POST", "DELETE"])
def access(request, pk):
    return Response(
        play.issue_access(request.user, pk, revoke=request.method == "DELETE")
    )


@endpoint(["GET"])
def journal(request, pk):
    session = play.accessible(request.user, pk)
    try:
        offset = int(request.query_params.get("offset", 0))
        limit = int(request.query_params.get("limit", 10))
    except ValueError:
        raise DomainError("Invalid journal page; limit must be 1–20") from None
    return Response(play.journal(session, offset, limit))


@endpoint(["POST"])
def review(request, pk):
    return Response(
        play.review_consequences(
            request.user,
            pk,
            request.data.get("operation_id"),
            request.data.get("expected_version"),
            request.data.get("decisions"),
        )
    )


@endpoint(["GET"])
def report(request, pk):
    response = HttpResponse(
        play.export_play(request.user, pk, request_language(request)),
        content_type="text/markdown; charset=utf-8",
    )
    response["Content-Disposition"] = 'attachment; filename="play-report.md"'
    return response
