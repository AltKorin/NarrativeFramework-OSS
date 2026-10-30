"""Character drafts with levels, character axes and choices through REST; invented mini-system only."""
import uuid
from unittest.mock import patch

from django.contrib.auth import get_user_model
from django.test import TestCase
from rest_framework.test import APIClient

from .models import Campaign, CampaignMember, WikiPage
from .test_rulesets_levels import CHOICES_WOJ3_MAG2, mini_levels


class LevelCharacterFlowTests(TestCase):
    def setUp(self):
        users = get_user_model().objects
        self.gm, self.player = (users.create_user(n) for n in ("gm", "player"))
        self.campaign = Campaign.objects.create(owner=self.gm, title="World")
        CampaignMember.objects.create(campaign=self.campaign, user=self.player, role="player")
        self.base = f"/api/campaigns/{self.campaign.id}/"
        response = self.client_for(self.gm).put(self.base + "ruleset/", {"version": 0, "document": mini_levels()}, format="json")
        self.assertEqual(response.status_code, 200, response.data)

    def client_for(self, user):
        client = APIClient()
        client.force_authenticate(user)
        return client

    def start(self, user, method="zestaw", status=201, **extra):
        response = self.client_for(user).post(self.base + "characters/", {"id": str(uuid.uuid4()), "method": method, **extra}, format="json")
        self.assertEqual(response.status_code, status, response.data)
        return response.data

    def act(self, user, draft, status=200, **command):
        response = self.client_for(user).post(f"{self.base}characters/{draft['id']}/", {"version": draft["version"], **command}, format="json")
        self.assertEqual(response.status_code, status, response.data)
        return response.data if status == 200 else draft

    def fighter(self, user, method="zestaw"):
        d = self.start(user, method)
        d = self.act(user, d, action="set_axis", axis="klasa", picks=[{"option": "woj", "levels": 1}])
        return self.act(user, d, action="set_axis", axis="pochodzenie", picks=[{"option": "rybak"}])

    def test_gm_sets_level_player_gets_minimum(self):
        self.assertEqual(self.start(self.player)["state"]["level"], 1)
        self.assertEqual(self.start(self.player, level=4)["state"]["level"], 1)  # players cannot pick a level
        self.assertEqual(self.start(self.gm, level=5)["state"]["level"], 5)
        for level in (6, 0, "5", True):
            with self.subTest(level=level):
                self.start(self.gm, status=400, level=level)

    def test_multiclass_array_build_submit_approve(self):
        d = self.start(self.gm, level=5)
        d = self.act(self.gm, d, action="set_axis", axis="klasa",
                     picks=[{"option": "woj", "levels": 3}, {"option": "mag", "levels": 2}])
        d = self.act(self.gm, d, action="set_axis", axis="pochodzenie", picks=[{"option": "rybak"}])
        d = self.act(self.gm, d, action="assign_stats", values={"SIL": 15, "ZRE": 8, "WIE": 14})
        self.assertIn("woj_styl", d["open"])
        for key, data in CHOICES_WOJ3_MAG2.items():
            d = self.act(self.gm, d, action="choose", key=key, **data)
        d = self.act(self.gm, d, action="set_name", name="Ilsa", look="")
        self.assertEqual(d["problems"], [])
        d = self.act(self.gm, d, action="submit")
        self.assertEqual(d["status"], "approved")
        self.assertEqual(d["sheet"]["derived"]["ZYCIE"], 45)
        body = WikiPage.objects.get(pk=d["page"]).body
        for text in ("Krąg 5", "Wojownik 3", "Mag 2", "Rybak", "Mur tarcz", "Pocisk", "Walka +6"):
            self.assertIn(text, body)
        runtime = self.campaign.__class__.objects.get(pk=self.campaign.pk).runtime
        self.assertIn('"LEVEL"', str(runtime).replace("'", '"'))

    def test_array_assignment_must_match_values(self):
        d = self.fighter(self.player)
        self.act(self.player, d, status=400, action="assign_stats", values={"SIL": 15, "ZRE": 15, "WIE": 8})
        self.act(self.player, d, status=400, action="assign_stats", values={"SIL": 15, "ZRE": 14})

    def test_point_costs(self):
        d = self.fighter(self.player, method="punkty")
        d = self.act(self.player, d, action="set_stats", values={"SIL": 15, "ZRE": 8, "WIE": 8})  # 9 + 0 + 0
        self.assertTrue(any("Spend exactly 10" in p for p in d["problems"]))
        d = self.act(self.player, d, action="set_stats", values={"SIL": 15, "ZRE": 9, "WIE": 8})  # 9 + 1 + 0
        self.assertFalse(any("Spend" in p for p in d["problems"]))
        self.act(self.player, d, status=400, action="set_stats", values={"SIL": 16, "ZRE": 8, "WIE": 8})

    def test_roll_keep_is_server_side_once(self):
        d = self.fighter(self.player, method="rzut")
        with patch("stories.characters.secrets.randbelow", side_effect=[5, 0, 3, 2] * 3):  # 6,1,4,3 -> 13
            d = self.act(self.player, d, action="roll_stat_set")
        self.assertEqual(d["state"]["stat_set"], [13, 13, 13])
        self.act(self.player, d, status=409, action="roll_stat_set")
        self.act(self.player, d, status=400, action="assign_stats", values={"SIL": 15, "ZRE": 13, "WIE": 13})
        d = self.act(self.player, d, action="assign_stats", values={"SIL": 13, "ZRE": 13, "WIE": 13})
        self.assertFalse(any("Roll" in p or "Assign" in p for p in d["problems"]))

    def test_choose_rejects_closed_keys(self):
        d = self.fighter(self.player)
        self.act(self.player, d, status=400, action="choose", key="woj_dar_4", picks=["twardy"])
        self.act(self.player, d, status=400, action="choose", key="mag_sztuczki", picks=["iskra"])
        d = self.act(self.player, d, action="choose", key="woj_umiej", picks=["walka"])
        self.assertEqual(d["state"]["choices"]["woj_umiej"], {"picks": ["walka"]})

    def test_incomplete_character_cannot_be_submitted(self):
        d = self.fighter(self.player)
        d = self.act(self.player, d, action="set_name", name="Bo", look="")
        self.act(self.player, d, status=409, action="submit")

    def test_malformed_commands_are_rejected(self):
        d = self.start(self.player)
        for command in [dict(action="set_axis", axis="klasa", picks="woj"),
                        dict(action="set_axis", axis="klasa", picks=[{"option": "woj", "levels": True}]),
                        dict(action="set_axis", axis="klasa", picks=[{"option": "smok", "levels": 1}]),
                        dict(action="set_axis", axis="rasa", picks=[{"option": "woj", "levels": 1}]),
                        dict(action="set_axis", axis="pochodzenie", picks=[{"option": "rybak"}, {"option": "uczony"}]),
                        dict(action="choose", key=None, picks=[]),
                        dict(action="choose", key="woj_umiej", picks=["walka"] * 10_000),
                        dict(action="choose", key="woj_umiej", picks="walka"),
                        dict(action="assign_stats", values=[15, 14, 8])]:
            with self.subTest(command=command):
                self.act(self.player, d, status=400, **command)
        d = self.fighter(self.player)
        for command in [dict(action="choose", key="rybak_cechy", values={"SIL": "2"}),
                        dict(action="choose", key="rybak_cechy", values={"SIL": True}),
                        dict(action="choose", key="rybak_cechy", values=[2, 1])]:
            with self.subTest(command=command):
                self.act(self.player, d, status=400, **command)

    def test_reroll_needs_gm(self):
        d = self.fighter(self.player, method="rzut")
        d = self.act(self.player, d, action="roll_stat_set")
        d = self.act(self.player, d, action="assign_stats", values=dict(zip(("SIL", "ZRE", "WIE"), d["state"]["stat_set"])))
        d = self.act(self.player, d, action="set_name", name="Bo", look="")
        for key, data in {"rybak_cechy": {"values": {"SIL": 1, "ZRE": 1, "WIE": 1}}, "woj_umiej": {"picks": ["walka"]}}.items():
            d = self.act(self.player, d, action="choose", key=key, **data)
        self.assertEqual(d["problems"], [], d["problems"])
        d = self.act(self.player, d, action="submit")
        d = self.act(self.gm, d, action="return", note="Przerzuć cechy")
        d = self.act(self.gm, d, action="grant_reroll", target="stats")
        d = self.act(self.player, d, action="roll_stat_set")
        self.assertIsNone(d["state"]["stats"])  # a new roll needs a new assignment
