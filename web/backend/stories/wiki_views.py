"""Session-authenticated REST API for the private campaign wiki."""

from functools import wraps

from django.http import HttpResponse
from rest_framework.decorators import api_view
from rest_framework.response import Response

from . import wiki, wiki_sharing
from .domain_errors import DomainError


@api_view(["GET", "POST"])
def runtime(request, campaign_id):
    from . import campaign_runtime

    try:
        result = (campaign_runtime.read(request.user, campaign_id)
                  if request.method == "GET"
                  else campaign_runtime.command(request.user, campaign_id, request.data))
        return Response(result)
    except DomainError as error:
        return Response({"error": str(error)}, status=error.status)


def errors(fn):
    @wraps(fn)
    def wrapped(*args, **kwargs):
        try:
            return fn(*args, **kwargs)
        except DomainError as error:
            return Response({"error": str(error)}, status=error.status)

    return wrapped


@api_view(["POST"])
@errors
def runtime_preview(request, campaign_id):
    from . import campaign_runtime

    return Response(campaign_runtime.command(
        request.user, campaign_id, request.data, preview=True))


@api_view(["GET", "POST"])
@errors
def campaigns(request):
    if request.method == "GET":
        return Response(wiki.list_campaigns(request.user))
    result, created = wiki.create_campaign(request.user, request.data)
    return Response(result, status=201 if created else 200)


@api_view(["GET", "PUT", "DELETE"])
@errors
def campaign(request, campaign_id):
    if request.method == "PUT":
        return Response(wiki.update_campaign(request.user, campaign_id, request.data))
    if request.method == "DELETE":
        return Response(wiki.delete_campaign(request.user, campaign_id, request.data))
    return Response(
        wiki.campaign_detail(
            request.user, campaign_id, request.query_params.get("preview")
        )
    )


@api_view(["GET", "POST"])
@errors
def pages(request, campaign_id):
    if request.method == "GET":
        return Response(
            wiki.search_pages(
                request.user,
                campaign_id,
                request.query_params.get("q", ""),
                request.query_params.get("preview"),
            )
        )
    result, created = wiki.create_page(request.user, campaign_id, request.data)
    return Response(result, status=201 if created else 200)


@api_view(["GET", "PUT", "DELETE"])
@errors
def page(request, campaign_id, page_id):
    if request.method == "GET":
        return Response(
            wiki.read_page(
                request.user, campaign_id, page_id, request.query_params.get("preview")
            )
        )
    if request.method == "DELETE":
        wiki.body_object(request.data)
        return Response(
            wiki.delete_page(
                request.user, campaign_id, page_id, request.data.get("version")
            )
        )
    return Response(wiki.save_page(request.user, campaign_id, page_id, request.data))


@api_view(["POST"])
@errors
def relations(request, campaign_id):
    result, created = wiki.create_relation(request.user, campaign_id, request.data)
    return Response(result, status=201 if created else 200)


@api_view(["DELETE"])
@errors
def relation(request, campaign_id, relation_id):
    return Response(wiki.delete_relation(request.user, campaign_id, relation_id))


@api_view(["POST"])
@errors
def use_page(request, campaign_id, page_id):
    return Response(
        wiki.import_to_scenario(request.user, campaign_id, page_id, request.data)
    )


@api_view(["GET"])
@errors
def export_campaign(request, campaign_id):
    import json

    result = wiki.export_campaign(
        request.user, campaign_id, request.query_params.get("preview")
    )
    response = HttpResponse(
        json.dumps(result, ensure_ascii=False, indent=2),
        content_type="application/json; charset=utf-8",
    )
    response["Content-Disposition"] = 'attachment; filename="campaign-wiki.json"'
    return response


@api_view(["GET"])
@errors
def members(request, campaign_id):
    return Response(wiki_sharing.members(request.user, campaign_id))


@api_view(["PATCH", "DELETE"])
@errors
def member(request, campaign_id, member_id):
    return Response(
        wiki_sharing.change_member(
            request.user,
            campaign_id,
            member_id,
            request.data if request.method == "PATCH" else None,
        )
    )


@api_view(["POST"])
@errors
def leave(request, campaign_id):
    return Response(wiki_sharing.leave(request.user, campaign_id))


@api_view(["GET", "POST"])
@errors
def invitations(request, campaign_id):
    return Response(
        wiki_sharing.invitations(
            request.user,
            campaign_id,
            request.data if request.method == "POST" else None,
        )
    )


@api_view(["DELETE"])
@errors
def revoke_invitation(request, campaign_id, invitation_id):
    return Response(
        wiki_sharing.revoke_invitation(request.user, campaign_id, invitation_id)
    )


@api_view(["POST"])
@errors
def inspect_invitation(request):
    return Response(wiki_sharing.invitation(request.user, request.data))


@api_view(["POST"])
@errors
def accept_invitation(request):
    return Response(wiki_sharing.invitation(request.user, request.data, accept=True))


@api_view(["POST"])
@errors
def publication(request, campaign_id, page_id):
    return Response(
        wiki_sharing.publish(request.user, campaign_id, page_id, request.data)
    )


@api_view(["POST"])
@errors
def relation_publication(request, campaign_id, relation_id):
    return Response(
        wiki_sharing.publish(
            request.user, campaign_id, relation_id, request.data, relation=True
        )
    )


@api_view(["GET"])
@errors
def audit(request, campaign_id):
    return Response(wiki_sharing.audit(request.user, campaign_id))
