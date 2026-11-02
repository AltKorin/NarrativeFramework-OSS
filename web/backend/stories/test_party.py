"""Party page: every player sees who plays whom and what they are good at, never another player's full sheet."""
import json
import uuid

from django.contrib.auth import get_user_model
from django.test import TestCase, override_settings
from rest_framework.test import APIClient

from .models import Campaign, CampaignMember, CampaignRuleset, CharacterDraft
from .test_rulesets import mini


def pack(**extra):
    document = mini()
    document["skills"] += [
        {"id": "skradanie", "label": "Skradanie", "stat": "WOL", "cost": 1, "max": 6},
        {"id": "wiedza", "label": "Wiedza", "stat": "WOL", "cost": 1, "max": 6},
        {"id": "jazda", "label": "Jazda", "stat": "SIL", "cost": 1, "max": 6}]
    document["methods"].append({"id": "punkty", "label": "Punkty", "steps": [
        {"type": "role"}, {"type": "stats_points", "pool": 11}, {"type": "skills_points", "pool": 20, "max": 6},
        {"type": "name"}]})
    return {**document, "skill_value": "STAT + RANK", "skill_tiers": [{"min": 8, "label": "Wyszkolony"}], **extra}


def skill_less():
    document = pack()
    document.pop("skill_value"), document.pop("skill_tiers")
    document["catalog"] = [{**item, "modifiers": []} for item in document["catalog"]]
    document["roles"] = [{**role, "skill_package": {}} for role in document["roles"]]
    return {**document, "skills": []}


def events(name=None, stats=None, skills=None, focus=None):
    result = [{"type": "role_chosen", "role": "wojownik"}]
    if stats:
        result.append({"type": "stats_set", "values": stats})
    if skills:
        result.append({"type": "skills_set", "values": skills})
    if name:
        result.append({"type": "name_set", "name": name, "look": ""})
    if focus:
        result.append({"type": "focus_set", "text": focus})
    return result


@override_settings(PARTY_PAGE=True)
class PartyTests(TestCase):
    def setUp(self):
        users = get_user_model().objects
        self.gm, self.anna, self.bart = (users.create_user(n) for n in ("gm", "anna", "bart"))
        self.campaign = Campaign.objects.create(owner=self.gm, title="World")
        self.members = {u.username: CampaignMember.objects.create(campaign=self.campaign, user=u, role="player")
                        for u in (self.anna, self.bart)}
        CampaignRuleset.objects.create(campaign=self.campaign, document=pack())
        self.url = f"/api/campaigns/{self.campaign.id}/party/"

    def draft(self, owner, status="draft", document=None, **kwargs):
        return CharacterDraft.objects.create(
            id=uuid.uuid4(), campaign=self.campaign, member=self.members[owner], ruleset=document or pack(),
            method="punkty", status=status, events=events(**kwargs))

    def get(self, user, status=200):
        client = APIClient()
        client.force_authenticate(user)
        response = client.get(self.url)
        self.assertEqual(response.status_code, status, getattr(response, "data", None))
        return response.data

    def tile(self, data, who):
        return next(t for t in data["players"] if t["member"] == str(self.members[who].pk))

    def test_flag_and_switch_hide_the_page(self):
        with override_settings(PARTY_PAGE=False):
            self.get(self.anna, 404)
        Campaign.objects.filter(pk=self.campaign.pk).update(party_enabled=False)
        self.get(self.anna, 404)
        self.get(get_user_model().objects.create_user("outsider"), 404)

    def test_player_sees_every_player_without_private_details(self):
        self.draft("anna", name="Mira", stats={"SIL": 5, "WOL": 6}, skills={"walka": 2})
        self.draft("bart", name="Tor", stats={"SIL": 7, "WOL": 4}, skills={"walka": 4}, focus="Strzelam")
        data = self.get(self.anna)
        self.assertEqual([t["display"] for t in data["players"]], ["Gracz 1", "Gracz 2"])
        hung = self.tile(data, "bart")["character"]
        self.assertEqual((hung["name"], hung["axes"], hung["focus"]), ("Tor", "Wojownik", "Strzelam"))
        self.assertEqual(hung["skills"], [{"label": "Walka", "value": 11, "tier": "Wyszkolony"}])
        text = json.dumps(data)
        for private in ('"stats"', '"items"', '"events"', '"tables"', '"SIL"', '"account"', "anna", "bart"):
            self.assertNotIn(private, text)

    def test_approved_character_wins_and_other_drafts_are_listed(self):
        approved = self.draft("anna", status="approved", name="Mira")
        later = self.draft("anna", name="Porzucony")
        CharacterDraft.objects.filter(pk=later.pk).update(updated_at=approved.updated_at.replace(year=2099))
        tile = self.tile(self.get(self.bart), "anna")
        self.assertEqual((tile["character"]["id"], tile["character"]["status"]), (str(approved.id), "approved"))
        self.assertEqual([d["name"] for d in tile["drafts"]], ["Porzucony"])

    def test_without_approval_the_latest_draft_is_on_top(self):
        older = self.draft("anna", name="Stary")
        newer = self.draft("anna", name="Nowy")
        CharacterDraft.objects.filter(pk=older.pk).update(updated_at=newer.updated_at.replace(year=2000))
        tile = self.tile(self.get(self.anna), "anna")
        self.assertEqual((tile["character"]["name"], [d["name"] for d in tile["drafts"]]), ("Nowy", ["Stary"]))

    def test_specialization_is_three_highest_ranked_skills_with_tiers(self):
        self.draft("anna", name="Mira", stats={"SIL": 3, "WOL": 8},
                   skills={"walka": 4, "skradanie": 1, "wiedza": 3, "jazda": 0})
        skills = self.tile(self.get(self.bart), "anna")["character"]["skills"]
        self.assertEqual(skills, [{"label": "Wiedza", "value": 11, "tier": "Wyszkolony"},
                                  {"label": "Skradanie", "value": 9, "tier": "Wyszkolony"},
                                  {"label": "Walka", "value": 7, "tier": None}])

    def test_fresh_draft_and_skill_less_pack_do_not_break(self):
        CharacterDraft.objects.create(id=uuid.uuid4(), campaign=self.campaign, member=self.members["anna"],
                                      ruleset=pack(), method="pelna", events=[])
        self.draft("bart", document=skill_less(),
                   name="Tor", stats={"SIL": 5, "WOL": 5})
        data = self.get(self.anna)
        fresh = self.tile(data, "anna")["character"]
        self.assertEqual((fresh["name"], fresh["axes"], fresh["skills"]), (None, "", []))
        self.assertEqual(self.tile(data, "bart")["character"]["skills"], [])

    def test_player_without_a_character_has_an_empty_tile_and_the_gm_has_none(self):
        data = self.get(self.gm)
        self.assertEqual([(t["account"], t["character"], t["drafts"]) for t in data["players"]],
                         [("anna", None, []), ("bart", None, [])])

    def test_gm_toggles_the_switch(self):
        client = APIClient()
        client.force_authenticate(self.gm)
        base = f"/api/campaigns/{self.campaign.id}/"
        campaign = client.get(base).data
        self.assertTrue(campaign["party"])
        response = client.put(base, {"title": "World", "version": campaign["version"], "party_enabled": False}, format="json")
        self.assertEqual(response.status_code, 200, response.data)
        self.assertFalse(response.data["party"])
        player = APIClient()
        player.force_authenticate(self.anna)
        self.assertEqual(player.put(base, {"title": "World", "version": 0, "party_enabled": True}, format="json").status_code, 404)


@override_settings(PARTY_PAGE=True)
class LevelledPartyTests(TestCase):
    """Packs with levels pick classes on axes and value skills by formula; the tile follows the sheet."""
    from .test_characters_levels import LevelCharacterFlowTests as _L  # helpers only; not at module level, so its tests do not run twice
    setUp, client_for, start, act, fighter = _L.setUp, _L.client_for, _L.start, _L.act, _L.fighter

    def test_tile_shows_axis_options_and_formula_valued_skills(self):
        d = self.fighter(self.player)
        d = self.act(self.player, d, action="assign_stats", values={"SIL": 15, "ZRE": 8, "WIE": 14})
        tile = self.client_for(self.player).get(f"{self.base}party/").data["players"][0]["character"]
        self.assertIn("Wojownik 1", tile["axes"])
        best = max(d["sheet"]["skill_values"].items(), key=lambda kv: kv[1])
        self.assertEqual(tile["skills"][0]["value"], best[1])


@override_settings(PARTY_PAGE=True)
class MineTests(TestCase):
    setUp, draft, get, tile = PartyTests.setUp, PartyTests.draft, PartyTests.get, PartyTests.tile

    def test_each_viewer_finds_their_own_tile(self):
        data = self.get(self.anna)
        self.assertEqual([t["mine"] for t in data["players"]], [True, False])
        self.assertEqual([t["mine"] for t in self.get(self.gm)["players"]], [False, False])


@override_settings(PARTY_PAGE=True)
class StaleSnapshotTests(TestCase):
    setUp, get, tile = PartyTests.setUp, PartyTests.get, PartyTests.tile

    def test_a_draft_whose_snapshot_no_longer_validates_does_not_break_the_page(self):
        broken = {**pack(), "skill_tiers": "not a list"}  # stored before a stricter validator existed
        CharacterDraft.objects.create(id=uuid.uuid4(), campaign=self.campaign, member=self.members["anna"],
                                      ruleset=broken, method="punkty", events=events(name="Stara"))
        card = self.tile(self.get(self.bart), "anna")["character"]
        self.assertEqual((card["name"], card["axes"], card["skills"]), ("Stara", "", []))


@override_settings(PARTY_PAGE=True)
class DisplayNameTests(TestCase):
    """Players choose how the party sees them; logins stay with the GM."""
    setUp, get, tile = PartyTests.setUp, PartyTests.get, PartyTests.tile

    def name(self, user, value, status=200):
        client = APIClient()
        client.force_authenticate(user)
        response = client.post(self.url, {"display_name": value}, format="json")
        self.assertEqual(response.status_code, status, getattr(response, "data", None))
        return response.data

    def test_player_sets_and_clears_their_name(self):
        self.name(self.bart, "  Kuba  ")
        self.assertEqual(self.tile(self.get(self.anna), "bart")["display"], "Kuba")
        self.name(self.bart, "")
        self.assertEqual(self.tile(self.get(self.anna), "bart")["display"], "Gracz 2")

    def test_name_limits_and_who_may_set_it(self):
        self.name(self.anna, "x" * 41, 400)
        self.name(self.anna, 7, 400)
        self.name(self.gm, "Mistrz", 403)

    def test_only_the_gm_sees_logins(self):
        self.assertEqual([t["account"] for t in self.get(self.gm)["players"]], ["anna", "bart"])
        self.assertNotIn("account", self.get(self.anna)["players"][0])
