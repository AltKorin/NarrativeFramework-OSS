"""Party page: who plays whom and what they are good at. Players see every tile, never another player's sheet."""
from django.conf import settings
from django.db import transaction

from . import characters, rulesets
from .campaign_access import campaign_access
from .domain_errors import DomainError

ACTIVE = ("submitted", "returned", "draft")


@transaction.atomic
def overview(user, campaign_id):
    c = campaign_access(user, campaign_id, minimum="player")
    if not (settings.PARTY_PAGE and c.party_enabled):
        raise DomainError("Campaign not found", 404)
    players = c.members.filter(role="player").select_related("user").order_by("user__username")
    drafts = {}
    for draft in c.character_drafts.filter(member__in=players).order_by("-updated_at"):
        drafts.setdefault(draft.member_id, []).append(draft)
    return {"players": [tile(member, drafts.get(member.pk, [])) for member in players]}


def tile(member, drafts):
    """Approved character on top, otherwise the most recently changed draft; the rest stay folded away."""
    approved = [d for d in drafts if d.status == "approved"]
    active = [d for d in drafts if d.status in ACTIVE]
    ordered = approved + active
    top = ordered[0] if ordered else None
    return {"member": str(member.pk), "account": member.user.username,
            "character": card(top) if top else None,
            "drafts": [card(d) for d in ordered[1:]]}


def card(draft):
    pack = rulesets.load(draft.ruleset)
    state = characters.fold(draft.events)
    return {"id": str(draft.id), "name": state["name"], "status": draft.status,
            "axes": axes(pack, state), "skills": specialization(pack, draft, state), "focus": state["focus"]}


def axes(pack, state):
    if pack.get("axes"):
        return " · ".join(line.split(": ", 1)[1] for line in characters.axis_lines(pack, state))
    role = characters.lookup(pack, "roles").get(state["role"])
    return role["label"] if role else ""


def specialization(pack, draft, state):
    """The three highest skills the character has ranks in, valued by the pack, with its tier names."""
    result = characters.compute(pack, draft, state)
    sheet, ranks = result["sheet"], result["skills"] or {}
    if not sheet:
        return []
    order = {s["id"]: i for i, s in enumerate(pack["skills"])}
    labels = {s["id"]: s["label"] for s in pack["skills"]}
    totals = sheet.get("skill_values") or rulesets.skill_totals(pack, result["stats"], ranks)
    picked = sorted((k for k, r in ranks.items() if r > 0 and k in totals), key=lambda k: (-totals[k], order[k]))[:3]
    tiers = pack.get("skill_tiers") or []
    return [{"label": labels[k], "value": totals[k],
             "tier": next((t["label"] for t in reversed(tiers) if totals[k] >= t["min"]), None)} for k in picked]
