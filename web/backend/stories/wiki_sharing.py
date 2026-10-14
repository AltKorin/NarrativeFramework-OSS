"""Campaign membership and deliberate publication. Every mutation locks campaign first."""

import hashlib
import secrets
from datetime import timedelta

from django.db import transaction
from django.utils import timezone

from . import wiki
from .campaign_access import campaign_access
from .domain_errors import DomainError
from .models import CampaignAuditEvent, CampaignInvitation, CampaignMember


def change(c, user, action, resource=""):
    c.version += 1
    c.save(update_fields=["version", "updated_at"])
    CampaignAuditEvent.objects.create(
        campaign=c,
        actor=user,
        action=action,
        resource_id=str(resource),
        version=c.version,
    )


def expected(value, version):
    if type(value) is not int or value != version:
        raise DomainError("Campaign version conflict; reload before saving", 409)


def role(value):
    if value not in ("gm", "player"):
        raise DomainError("Invalid campaign role")
    return value


@transaction.atomic
def members(user, campaign_id):
    c = campaign_access(user, campaign_id)
    result = [
        {"id": str(m.id), "username": m.user.get_username(), "role": m.role}
        for m in c.members.select_related("user").order_by("user__username")
    ]
    return {"owner": c.owner.get_username(), "members": result}


@transaction.atomic
def invitations(user, campaign_id, data=None):
    c = campaign_access(user, campaign_id, minimum="owner")
    if data is not None:
        wiki.body_object(data)
        value = role(data.get("role"))
        if (
            c.invitations.filter(
                created_at__gte=timezone.now() - timedelta(hours=1)
            ).count()
            >= 30
        ):
            raise DomainError("Invitation rate limit reached", 429)
        if (
            c.invitations.filter(
                revoked=False, accepted_at__isnull=True, expires_at__gt=timezone.now()
            ).count()
            >= 20
        ):
            raise DomainError("Invitation limit reached", 409)
        token = secrets.token_urlsafe(32)
        invite = CampaignInvitation.objects.create(
            campaign=c,
            digest=hashlib.sha256(token.encode()).hexdigest(),
            role=value,
            expires_at=timezone.now() + timedelta(days=7),
        )
        change(c, user, "invitation.created", invite.id)
        return {
            "id": str(invite.id),
            "token": token,
            "role": value,
            "expires_at": invite.expires_at,
        }
    return [
        {
            "id": str(i.id),
            "role": i.role,
            "expires_at": i.expires_at,
            "revoked": i.revoked,
            "accepted_by": i.accepted_by.get_username() if i.accepted_by else None,
            "accepted": i.accepted_at is not None,
        }
        for i in c.invitations.select_related("accepted_by").order_by("-created_at")[
            :100
        ]
    ]


@transaction.atomic
def revoke_invitation(user, campaign_id, invitation_id):
    c = campaign_access(user, campaign_id, minimum="owner")
    i = c.invitations.filter(pk=wiki.identifier(invitation_id)).first()
    if not i:
        raise DomainError("Invitation not found", 404)
    i.revoked = True
    i.save(update_fields=["revoked"])
    change(c, user, "invitation.revoked", i.id)
    return {"revoked": True}


def token_invitation(data):
    wiki.body_object(data)
    token = wiki.text(data.get("token"), 128, True)
    i = CampaignInvitation.objects.filter(
        digest=hashlib.sha256(token.encode()).hexdigest()
    ).first()
    if not i:
        raise DomainError("Invitation unavailable", 404)
    return i


@transaction.atomic
def invitation(user, data, accept=False):
    initial = token_invitation(data)
    # Lock order is always campaign -> invitation/membership (including revoke).
    from .models import Campaign

    c = Campaign.objects.select_for_update().filter(pk=initial.campaign_id).first()
    if not c:
        raise DomainError("Invitation unavailable", 404)
    i = c.invitations.get(pk=initial.pk)
    if i.revoked or i.expires_at <= timezone.now() or i.accepted_at is not None:
        raise DomainError("Invitation unavailable", 404)
    result = {"campaign_id": str(c.id), "title": c.title, "role": i.role}
    if not accept:
        return result
    if c.owner_id == user.pk or c.members.filter(user=user).exists():
        raise DomainError("Already a campaign member", 409)
    if c.members.count() >= 50:
        raise DomainError("Campaign member limit reached", 409)
    CampaignMember.objects.create(campaign=c, user=user, role=i.role)
    i.accepted_by = user
    i.accepted_at = timezone.now()
    i.save(update_fields=["accepted_by", "accepted_at"])
    change(c, user, "invitation.accepted", i.id)
    return result


@transaction.atomic
def change_member(user, campaign_id, member_id, data=None):
    c = campaign_access(user, campaign_id, minimum="player")
    member = c.members.filter(pk=wiki.identifier(member_id)).first()
    if not member:
        raise DomainError("Member not found", 404)
    if c.wiki_role != "owner" and (data is not None or member.user_id != user.pk):
        raise DomainError("Member not found", 404)
    if data is None:
        change(c, user, "member.removed", member.id)
        member.delete()  # Through rows cascade; a later invitation grants nothing old.
        return {"removed": True}
    wiki.body_object(data)
    expected(data.get("version"), c.version)
    member.role = role(data.get("role"))
    # Role transitions never retain old individual grants.
    member.granted_pages.clear()
    member.granted_relations.clear()
    member.save(update_fields=["role"])
    change(c, user, "member.role_changed", member.id)
    return {"id": str(member.id), "role": member.role}


@transaction.atomic
def leave(user, campaign_id):
    c = campaign_access(user, campaign_id, minimum="player")
    if c.wiki_role == "owner":
        raise DomainError("The owner cannot leave the campaign", 409)
    return change_member(user, c.id, c.wiki_member.id)


@transaction.atomic
def publish(user, campaign_id, resource_id, data, relation=False):
    wiki.body_object(data)
    c = campaign_access(user, campaign_id)
    obj = (
        (c.relations if relation else c.pages)
        .filter(pk=wiki.identifier(resource_id))
        .first()
    )
    if not obj:
        raise DomainError("Wiki page not found", 404)
    expected(data.get("version"), obj.version)
    audience = data.get("audience")
    if audience not in ("gm", "all", "selected"):
        raise DomainError("Invalid publication audience")
    recipients = data.get("recipients", [])
    if (
        not isinstance(recipients, list)
        or len(recipients) > 50
        or not all(isinstance(x, str) for x in recipients)
    ):
        raise DomainError("Invalid publication recipients")
    ids = [wiki.identifier(x) for x in recipients]
    selected = list(c.members.filter(id__in=ids, role="player"))
    if (
        len(set(ids)) != len(ids)
        or len(selected) != len(ids)
        or audience == "selected"
        and not ids
        or audience != "selected"
        and ids
    ):
        raise DomainError("Invalid publication recipients")
    obj.version += 1
    obj.audience = audience
    obj.published = (
        None
        if audience == "gm"
        else (
            {"label": obj.label, "version": obj.version}
            if relation
            else {
                "title": obj.title,
                "kind": obj.kind,
                "body": obj.body,
                "version": obj.version,
            }
        )
    )
    obj.save(update_fields=["version", "audience", "published"])
    obj.recipients.set(selected)
    change(c, user, "relation.publication" if relation else "page.publication", obj.id)
    return wiki.relation_json(obj) if relation else wiki.page_json(obj)


@transaction.atomic
def audit(user, campaign_id):
    c = campaign_access(user, campaign_id)
    return [
        {
            "actor": e.actor.get_username() if e.actor else None,
            "action": e.action,
            "resource_id": e.resource_id,
            "version": e.version,
            "created_at": e.created_at,
        }
        for e in c.audit_events.select_related("actor").order_by("-id")[:100]
    ]
