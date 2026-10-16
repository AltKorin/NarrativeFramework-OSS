"""Campaign access and atomic persistence around the portable narrative engine."""

from django.db import transaction

from .campaign_access import campaign_access
from .domain_errors import DomainError
from . import narrative_engine as engine
from .wiki import body_object, touch


@transaction.atomic
def read(user, campaign_id):
    campaign = campaign_access(user, campaign_id)
    return {"version": campaign.version, **engine.view(campaign.runtime)}


@transaction.atomic
def command(user, campaign_id, data, *, preview=False):
    body_object(data)
    campaign = campaign_access(user, campaign_id)
    if type(data.get("version")) is not int or data["version"] != campaign.version:
        raise DomainError("Campaign version conflict; reload before saving", 409)
    result = engine.execute(campaign.runtime, data)
    # Actor references are campaign wiki IDs, never foreign campaign records.
    actors = engine.actor_ids(result)
    allowed = {str(p) for p in campaign.pages.filter(kind__in=("npc", "pc", "organization")).values_list("id", flat=True)}
    if not actors <= allowed:
        raise DomainError("Choose a character from this campaign")
    if preview:
        return {"version": campaign.version, **engine.view(result)}
    campaign.runtime = result
    campaign.save(update_fields=["runtime"])
    touch(campaign, user, "narrative." + data["action"], str(campaign.id))
    return {"version": campaign.version, **engine.view(result)}
