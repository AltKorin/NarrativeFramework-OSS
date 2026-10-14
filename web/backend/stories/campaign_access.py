"""One authority boundary for wiki REST, exports, preview and author MCP.

Callers hold a campaign row lock for coherent projections and all mutations.
Player views are explicit projections of publications, never filtered GM JSON.
"""

import uuid

from django.db.models import Q

from .domain_errors import DomainError
from .models import Campaign, CampaignMember


def campaign_access(user, campaign_id, *, lock=True, minimum="gm"):
    try:
        campaign_id = uuid.UUID(str(campaign_id))
    except (ValueError, TypeError, AttributeError):
        raise DomainError("Invalid UUID") from None
    qs = Campaign.objects.select_for_update() if lock else Campaign.objects
    c = qs.filter(pk=campaign_id).first()
    if not c:
        raise DomainError("Campaign not found", 404)
    role, member = role_for(c, user)
    if (
        role is None
        or minimum == "owner"
        and role != "owner"
        or minimum == "gm"
        and role not in {"owner", "gm"}
    ):
        raise DomainError("Campaign not found", 404)
    c.wiki_role = role
    c.wiki_member = member
    return c


def role_for(campaign, user):
    if campaign.owner_id == user.pk:
        return "owner", None
    member = CampaignMember.objects.filter(campaign=campaign, user=user).first()
    return (member.role, member) if member else (None, None)


def projection(c, preview=None):
    role, member = c.wiki_role, c.wiki_member
    if preview:
        if role not in {"owner", "gm"}:
            raise DomainError("Campaign not found", 404)
        try:
            member = c.members.filter(pk=uuid.UUID(str(preview)), role="player").first()
        except (ValueError, TypeError):
            member = None
        if not member:
            raise DomainError("Member not found", 404)
        role = "player"
    return role, member


def visible_records(qs, role, member):
    if role in {"owner", "gm"}:
        return qs
    return (
        qs.filter(published__isnull=False)
        .filter(Q(audience="all") | Q(audience="selected", recipients=member))
        .distinct()
    )


def public_page(p, full=True):
    # Deliberately omit draft fields, gm_notes, recipients and current draft version.
    published = p.published
    result = {
        "id": str(p.id),
        "campaign_id": str(p.campaign_id),
        **{k: published[k] for k in ("title", "kind", "version")},
    }
    if full:
        result["body"] = published["body"]
    return result


def public_relation(r):
    return {
        "id": str(r.id),
        "source": str(r.source_id),
        "target": str(r.target_id),
        "label": r.published["label"],
    }
