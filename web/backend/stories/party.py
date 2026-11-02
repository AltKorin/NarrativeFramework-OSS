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
    players = list(c.members.filter(role="player").select_related("user").order_by("user__username"))
    drafts = {}
    for draft in c.character_drafts.filter(member__in=players).order_by("-updated_at"):
        drafts.setdefault(draft.member_id, []).append(draft)
    gm = c.wiki_role in {"owner", "gm"}
    return {"players": [tile(member, n, drafts.get(member.pk, []), user, gm) for n, member in enumerate(players, 1)]}


@transaction.atomic
def set_display_name(user, campaign_id, data):
    """A player's own name in this party; logins stay visible to the GM only."""
    c = campaign_access(user, campaign_id, minimum="player")
    if not (settings.PARTY_PAGE and c.party_enabled):
        raise DomainError("Campaign not found", 404)
    if c.wiki_role != "player":
        raise DomainError("Only players have a name in the party", 403)
    name = data.get("display_name") if isinstance(data, dict) else None
    if not isinstance(name, str) or len(name.strip()) > 40:
        raise DomainError("Choose a name of up to 40 characters")
    c.wiki_member.display_name = name.strip()
    c.wiki_member.save(update_fields=["display_name"])
    return overview(user, campaign_id)


def tile(member, number, drafts, viewer, gm):
    """Approved character on top, otherwise the most recently changed draft; the rest stay folded away."""
    approved = [d for d in drafts if d.status == "approved"]
    active = [d for d in drafts if d.status in ACTIVE]
    ordered = approved + active
    top = ordered[0] if ordered else None
    return {"member": str(member.pk), "display": member.display_name or f"Gracz {number}",
            "mine": member.user_id == viewer.pk, **({"account": member.user.username} if gm else {}),
            "character": card(top) if top else None,
            "drafts": [card(d) for d in ordered[1:]]}


def card(draft):
    state = characters.fold(draft.events)
    result = {"id": str(draft.id), "name": state["name"], "status": draft.status, "axes": "", "skills": [],
              "focus": state["focus"]}
    try:
        pack = rulesets.load(draft.ruleset)
    except DomainError:  # a snapshot stored before a stricter validator must not take the page down for everyone
        return result
    return {**result, "axes": axes(pack, state), "skills": specialization(pack, draft, state)}


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
