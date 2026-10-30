"""Character drafts end to end through REST: ruleset upload, player building, GM review and approval."""
import uuid
from unittest.mock import patch

from django.contrib.auth import get_user_model
from django.test import TestCase
from rest_framework.test import APIClient

from .models import Campaign, CampaignMember, CharacterDraft, WikiPage
from .test_rulesets import mini


def pack():
    document = mini()
    document["methods"].append({"id": "pakiet", "label": "Pakiet", "steps": [
        {"type": "role"}, {"type": "stats_role_roll"}, {"type": "skills_package"},
        {"type": "skills_points", "pool": 4, "max": 6, "require_basic": True},
        {"type": "gear_package", "loss_mode": "roll"}, {"type": "name"}]})
    document["roles"][0]["gear_package"] = [{"choose": 1, "items": ["ramie", "plaszcz"]}]
    document["tables"][0]["choose"] = True
    return document


class CharacterFlowTests(TestCase):
    def setUp(self):
        users = get_user_model().objects
        self.gm, self.player, self.other = (users.create_user(n) for n in ("gm", "player", "other"))
        self.campaign = Campaign.objects.create(owner=self.gm, title="World")
        self.member = CampaignMember.objects.create(campaign=self.campaign, user=self.player, role="player")
        CampaignMember.objects.create(campaign=self.campaign, user=self.other, role="player")
        self.base = f"/api/campaigns/{self.campaign.id}/"

    def client_for(self, user):
        client = APIClient()
        client.force_authenticate(user)
        return client

    def put_ruleset(self, document=None, revision=0, user=None):
        return self.client_for(user or self.gm).put(self.base + "ruleset/", {"version": revision, "document": document or pack()}, format="json")

    def start(self, user, method="pelna"):
        response = self.client_for(user).post(self.base + "characters/", {"id": str(uuid.uuid4()), "method": method}, format="json")
        self.assertEqual(response.status_code, 201, response.data)
        return response.data

    def act(self, user, draft, status=200, **command):
        response = self.client_for(user).post(f"{self.base}characters/{draft['id']}/", {"version": draft["version"], **command}, format="json")
        self.assertEqual(response.status_code, status, response.data)
        return response.data if status == 200 else draft

    def test_ruleset_upload_is_gm_only_versioned_and_validated(self):
        self.assertEqual(self.put_ruleset(user=self.player).status_code, 404)
        broken = pack()
        broken["skills"][0]["stat"] = "ZRE"
        self.assertEqual(self.put_ruleset(broken).status_code, 400)
        self.assertEqual(self.put_ruleset().data["revision"], 1)
        self.assertEqual(self.put_ruleset().status_code, 409)  # stale revision
        document = pack()
        document["source"] = "GM_ONLY_SOURCE"
        self.assertEqual(self.put_ruleset(document, 1).data["revision"], 2)
        read = self.client_for(self.player).get(self.base + "ruleset/").data
        self.assertNotIn("source", read["document"])
        self.assertEqual(self.client_for(self.gm).get(self.base + "ruleset/").data["document"]["source"], "GM_ONLY_SOURCE")
        outsider = get_user_model().objects.create_user("outsider")
        self.assertEqual(self.client_for(outsider).get(self.base + "ruleset/").status_code, 404)

    def test_player_builds_gm_returns_player_fixes_gm_approves(self):
        self.assertEqual(self.client_for(self.player).post(self.base + "characters/", {"id": str(uuid.uuid4()), "method": "pelna"}, format="json").status_code, 409)
        self.put_ruleset()
        draft = self.start(self.player)
        self.assertIn("Choose a role", draft["problems"])
        draft = self.act(self.player, draft, action="choose_role", role="wojownik")
        draft = self.act(self.player, draft, action="set_stats", values={"SIL": 6, "WOL": 6})
        self.assertIn("Spend exactly 10 statistic points", draft["problems"])
        self.act(self.player, draft, 409, action="submit")
        self.act(self.player, draft, 400, action="set_stats", values={"SIL": 9, "WOL": 1})
        draft = self.act(self.player, draft, action="set_stats", values={"SIL": 5, "WOL": 5})
        draft = self.act(self.player, draft, action="set_items", items=["ramie", "ostrze"] + ["plaszcz"] * 7)  # 1050 eb > 1000
        self.assertIn("Purchases exceed the budget", draft["problems"])
        draft = self.act(self.player, draft, action="set_items", items=["ramie", "ostrze"])
        draft = self.act(self.player, draft, action="roll_table", table="dom", result=6)
        rolled = draft["state"]["tables"]["dom"][0]
        self.assertTrue(1 <= rolled["result"] <= 6)
        self.act(self.player, draft, 409, action="roll_table", table="dom")  # no rerolling at will
        self.act(self.player, draft, 400, action="roll_table", table="wrog")  # not a step of this method
        draft = self.act(self.player, draft, action="set_name", name="Kira", look="Rude włosy")
        self.assertEqual(draft["problems"], [])
        self.assertEqual(draft["sheet"]["pools"]["DUSZA"]["current"], 50 - 7 - 3)
        self.act(self.gm, draft, 409, action="set_name", name="GM edit")  # GM does not edit player drafts
        self.act(self.gm, draft, 409, action="approve")  # not submitted yet
        draft = self.act(self.player, draft, action="submit")
        self.assertEqual(draft["status"], "submitted")
        self.act(self.player, draft, 409, action="set_name", name="Late")
        self.act(self.player, draft, 403, action="approve")
        self.act(self.gm, draft, 400, action="return", note="  ")
        draft = self.act(self.gm, draft, action="return", note="Dodaj wygląd")
        self.assertEqual((draft["status"], draft["review_note"]), ("returned", "Dodaj wygląd"))
        draft = self.act(self.gm, draft, action="grant_reroll", target="dom")
        draft = self.act(self.player, draft, action="roll_table", table="dom")
        self.act(self.player, draft, 409, action="roll_table", table="dom")
        draft = self.act(self.player, draft, action="set_name", name="Kira", look="Rude włosy, cyberramię")
        draft = self.act(self.player, draft, action="submit")
        draft = self.act(self.gm, draft, action="approve")
        self.assertEqual(draft["status"], "approved")
        page = WikiPage.objects.get(pk=draft["page"])
        self.assertEqual((page.kind, page.title, page.audience), ("pc", "Kira", "gm"))
        self.assertIn("Ramię", page.body)
        self.assertIn("dom 1d6", page.gm_notes)
        self.campaign.refresh_from_db()
        actor = self.campaign.runtime["actors"][str(page.id)]
        self.assertEqual((actor["SIL"], actor["ZYCIE"], actor["DUSZA"], actor["DUSZA_MAX"]), (5, 35, 40, 50))
        self.act(self.player, draft, 409, action="set_name", name="After")
        response = self.client_for(self.player).delete(f"{self.base}characters/{draft['id']}/", {"version": draft["version"]}, format="json")
        self.assertEqual(response.status_code, 409)

    def test_isolation_limits_and_idempotent_creation(self):
        self.put_ruleset()
        mine = self.start(self.player)
        self.assertEqual(self.client_for(self.other).get(f"{self.base}characters/{mine['id']}/").status_code, 404)
        self.assertEqual(self.client_for(self.other).get(self.base + "characters/").data, [])
        self.assertEqual(len(self.client_for(self.gm).get(self.base + "characters/").data), 1)
        self.start(self.player)
        self.start(self.player)
        response = self.client_for(self.player).post(self.base + "characters/", {"id": str(uuid.uuid4()), "method": "pelna"}, format="json")
        self.assertEqual(response.status_code, 409)
        retry = self.client_for(self.player).post(self.base + "characters/", {"id": mine["id"], "method": "pelna"}, format="json")
        self.assertEqual(retry.status_code, 200)
        self.assertEqual(self.client_for(self.player).post(self.base + "characters/", {"id": mine["id"], "method": "pakiet"}, format="json").status_code, 409)
        self.assertEqual(self.client_for(self.player).post(self.base + "characters/", {"id": str(uuid.uuid4()), "method": "brak"}, format="json").status_code, 400)
        stale = dict(mine, version=mine["version"] + 5)
        self.act(self.player, stale, 409, action="set_name", name="Kira")
        self.act(self.player, mine, 400, action="eval", code="1")
        deleted = self.client_for(self.player).delete(f"{self.base}characters/{mine['id']}/", {"version": mine["version"]}, format="json")
        self.assertEqual(deleted.data, {"deleted": True})
        self.start(self.player)  # the freed slot can be used again

    def test_role_rolls_packages_and_rolled_losses_survive_resubmission(self):
        self.put_ruleset()
        draft = self.start(self.player, "pakiet")
        self.act(self.player, draft, 400, action="roll_stats")  # no role yet
        draft = self.act(self.player, draft, action="choose_role", role="wojownik")
        draft = self.act(self.player, draft, action="roll_stats")
        self.assertEqual(draft["stats"], {"SIL": 7, "WOL": 4})
        self.act(self.player, draft, 409, action="roll_stats")
        self.act(self.player, draft, 400, action="set_stats", values={"SIL": 5, "WOL": 5})  # not this method
        draft = self.act(self.player, draft, action="set_skills", values={"walka": 3})
        self.assertIn("Walka: below the role package", draft["problems"])
        draft = self.act(self.player, draft, action="set_skills", values={"walka": 6})
        self.act(self.player, draft, 400, action="set_gear", items=["ostrze"])
        draft = self.act(self.player, draft, action="set_gear", items=["ramie"])
        draft = self.act(self.player, draft, action="set_name", name="Rolf")
        self.assertEqual(draft["problems"], [])
        with patch("stories.characters.secrets.randbelow", return_value=5):  # 2d6 -> 6 + 6
            draft = self.act(self.player, draft, action="submit")
        self.assertEqual(draft["sheet"]["pools"]["DUSZA"]["current"], 40 - 12)
        draft = self.act(self.gm, draft, action="return", note="ok")
        with patch("stories.characters.secrets.randbelow", return_value=0):  # would be 2 if rerolled
            draft = self.act(self.player, draft, action="submit")
        self.assertEqual(draft["sheet"]["pools"]["DUSZA"]["current"], 28)

    def test_halved_rolls_round_as_the_pack_says(self):
        from . import characters
        with patch("stories.characters.secrets.randbelow", return_value=2):  # 1d6 -> 3
            self.assertEqual((characters.roll(1, 6, 2), characters.roll(1, 6, 2, up=True)), (1, 2))
        with patch("stories.characters.secrets.randbelow", return_value=3):  # 1d6 -> 4, even: no rounding
            self.assertEqual((characters.roll(1, 6, 2), characters.roll(1, 6, 2, up=True)), (2, 2))

    def test_gm_characters_are_approved_on_submit(self):
        self.put_ruleset()
        draft = self.start(self.gm)
        self.assertIsNone(draft["member"])
        for command in ({"action": "choose_role", "role": "wojownik"}, {"action": "set_stats", "values": {"SIL": 5, "WOL": 5}},
                        {"action": "choose_table", "table": "dom", "entry": 0}, {"action": "set_name", "name": "Strażnik"}):
            draft = self.act(self.gm, draft, **command)
        self.assertEqual(draft["state"]["tables"]["dom"][0]["text"], "Z portu.")
        draft = self.act(self.gm, draft, action="submit")
        self.assertEqual(draft["status"], "approved")
        self.assertTrue(WikiPage.objects.filter(pk=draft["page"], kind="pc").exists())
        self.assertEqual(self.client_for(self.player).get(f"{self.base}characters/{draft['id']}/").status_code, 404)

    def test_ruleset_removal_keeps_draft_snapshots(self):
        self.put_ruleset()
        draft = self.start(self.player)
        response = self.client_for(self.gm).delete(self.base + "ruleset/", {"version": 1}, format="json")
        self.assertEqual(response.data, {"deleted": True, "drafts_in_progress": 1})
        self.act(self.player, draft, action="choose_role", role="wojownik")
        self.assertEqual(CharacterDraft.objects.get(pk=draft["id"]).ruleset["id"], "mini")
        self.assertEqual(self.client_for(self.player).post(self.base + "characters/", {"id": str(uuid.uuid4()), "method": "pelna"}, format="json").status_code, 409)
