"""Behavioral contract for the Unity core port and campaign integration."""
import copy
import uuid
from django.contrib.auth import get_user_model
from django.test import SimpleTestCase, TestCase
from rest_framework.test import APIClient
from . import narrative_engine as engine
from .domain_errors import DomainError
from .models import Campaign, CampaignMember, WikiPage


def gate_event(actor=None):
    return {"id": "gate", "title": "The gate closes", "MinTimeUnit": 3,
            "MaxTimeUnit": 4, "requiredFlags": ["warned"], "forbiddenFlags": ["safe"],
            "InvolvedActors": [actor] if actor else [],
            "choices": [{"text": "The party pays", "outcomes": [
                {"type": "ModifyResource", "targetId": "gold", "value": -5},
                {"type": "AddFlag", "targetId": "safe"}]}]}


class EngineTests(SimpleTestCase):
    def test_unity_inclusive_time_window_and_flag_semantics(self):
        item = engine.event(gate_event())
        for day, flags, expected in [(2, ["warned"], ["early"]), (3, ["warned"], []),
                                     (4, ["warned"], []), (5, ["warned"], ["expired"]),
                                     (3, [], ["missing_flag"]),
                                     (3, ["warned", "safe"], ["forbidden_flag"])]:
            with self.subTest(day=day, flags=flags):
                state = engine.initial_state()
                state.update(day=day, flags=flags)
                self.assertEqual([r["code"] for r in engine.blockers(state, item)], expected)

    def test_decision_changes_availability_and_is_not_repeatable(self):
        state = engine.initial_state()
        state.update(day=3, flags=["warned"], resources={"gold": 12})
        state = engine.execute(state, {"action": "add_event", "event": gate_event()})
        followup = gate_event()
        followup.update(id="welcome", requiredFlags=["safe"], forbiddenFlags=[])
        state = engine.execute(state, {"action": "add_event", "event": followup})
        self.assertTrue(engine.view(state)["events"][1]["blockers"])
        before = copy.deepcopy(state)
        result = engine.execute(state, {"action": "resolve", "eventId": "gate", "choice": 0})
        self.assertEqual(state, before)
        self.assertEqual(result["resources"]["gold"], 7)
        self.assertEqual(engine.view(result)["events"][1]["blockers"], [])
        self.assertEqual(result["history"][-1]["outcomes"][0]["value"], -5)
        with self.assertRaises(DomainError):
            engine.execute(result, {"action": "resolve", "eventId": "gate", "choice": 0})

    def test_actor_stats_conditions_relationships_and_variables(self):
        item = gate_event("guard")
        item["conditions"] = [{"scope": "actors", "actorId": "guard", "key": "health", "comparison": "GreaterThan", "value": 0}]
        item["choices"][0]["outcomes"] += [
            {"type": "ModifyActorAttribute", "actorId": "guard", "targetId": "health", "value": -2},
            {"type": "SetRelationship", "actorId": "guard", "otherActorId": "hero", "targetId": "trust", "value": 8},
            {"type": "ModifyNarrativeVariable", "targetId": "danger", "value": -1},
            {"type": "SetNarrativeBoolTrue", "targetId": "met"},
            {"type": "RemoveFlag", "targetId": "warned"}]
        state = engine.initial_state()
        state.update(day=3, flags=["warned"])
        state = engine.execute(state, {"action": "add_event", "event": item})
        self.assertEqual(engine.view(state)["events"][0]["blockers"][0]["code"], "condition")
        state = engine.execute(state, {"action": "set_value", "scope": "actors", "actorId": "guard", "key": "health", "value": 10})
        result = engine.execute(state, {"action": "resolve", "eventId": "gate", "choice": 0})
        self.assertEqual(result["actors"]["guard"]["health"], 8)
        self.assertEqual(result["relationships"]["guard"]["hero"]["trust"], 8)
        self.assertEqual(result["variables"]["danger"], -1)
        self.assertTrue(result["bools"]["met"])
        self.assertNotIn("warned", result["flags"])

    def test_bad_commands_and_unimplemented_outcomes_fail_explicitly(self):
        commands = [[], {"action": []}, {"action": "set_value", "scope": [], "key": "x", "value": 1},
                    {"action": "set_value", "scope": "resources", "key": "x", "value": True},
                    {"action": "set_value", "scope": "resources", "key": "x", "value": 10**400},
                    {"action": "set_flags", "flags": "flag"}]
        for command in commands:
            with self.subTest(command=str(command)[:80]), self.assertRaises(DomainError):
                engine.execute({}, command)
        for bad in ({"type": "TriggerStoryArc", "targetId": "x"}, {"type": "AddFlag", "targetId": "x", "probability": .5}):
            with self.assertRaises(DomainError):
                engine.outcome(bad)
        item = gate_event()
        item["Probability"] = .5
        with self.assertRaises(DomainError):
            engine.event(item)
        item.pop("Probability")
        item["choices"][0]["nextEventId"] = "automatic"
        with self.assertRaises(DomainError):
            engine.event(item)

    def test_partial_outcome_failure_does_not_mutate_state(self):
        item = gate_event()
        item["choices"][0]["outcomes"] = [{"type": "AddFlag", "targetId": "changed"}, {"type": "ModifyResource", "targetId": "gold", "value": 1e12}]
        state = engine.initial_state()
        state.update(day=3, flags=["warned"], resources={"gold": 1})
        state = engine.execute(state, {"action": "add_event", "event": item})
        original = copy.deepcopy(state)
        with self.assertRaises(DomainError):
            engine.execute(state, {"action": "resolve", "eventId": "gate", "choice": 0})
        self.assertEqual(state, original)

    def test_time_advances_without_executing_events(self):
        state = engine.execute({}, {"action": "add_event", "event": gate_event()})
        result = engine.execute(state, {"action": "advance"})
        self.assertEqual(result["day"], 2)
        self.assertEqual(result["completed"], [])
        self.assertEqual(result["resources"], {})


class RuntimeAPITests(TestCase):
    def setUp(self):
        users = get_user_model().objects
        self.owner = users.create_user("runtime-owner")
        self.player = users.create_user("runtime-player")
        self.co_gm = users.create_user("runtime-gm")
        self.other = users.create_user("runtime-other")
        self.campaign = Campaign.objects.create(owner=self.owner, title="Gate")
        CampaignMember.objects.create(campaign=self.campaign, user=self.player, role="player")
        CampaignMember.objects.create(campaign=self.campaign, user=self.co_gm, role="gm")
        self.actor = WikiPage.objects.create(campaign=self.campaign, kind="npc", title="Guard")
        self.client = APIClient()
        self.client.force_authenticate(self.owner)
        self.path = f"/api/campaigns/{self.campaign.id}/runtime/"
        self.root = f"/api/campaigns/{self.campaign.id}/"

    def send(self, **command):
        self.campaign.refresh_from_db()
        return self.client.post(self.path, {"version": self.campaign.version, **command}, format="json")

    def test_gm_can_run_world_and_export_state(self):
        self.client.force_authenticate(self.co_gm)
        response = self.send(action="set_value", scope="actors", actorId=str(self.actor.id), key="health", value=10)
        self.assertEqual(response.status_code, 200, response.content)
        self.assertEqual(response.json()["actors"][str(self.actor.id)]["health"], 10)
        self.assertEqual(self.client.get(self.root + "export/").json()["runtime"]["actors"][str(self.actor.id)]["health"], 10)
        self.assertEqual(self.campaign.audit_events.get().action, "narrative.set_value")

    def test_private_runtime_never_enters_player_exports(self):
        self.send(action="set_flags", flags=["PRIVATE_RUNTIME_SENTINEL"])
        for user in (self.player, self.other, None):
            self.client.force_authenticate(user)
            for method in ("get", "post"):
                response = getattr(self.client, method)(self.path, {}, format="json")
                self.assertIn(response.status_code, (401, 403, 404))
                self.assertNotIn("PRIVATE_RUNTIME_SENTINEL", response.content.decode())
        self.client.force_authenticate(self.player)
        exported = self.client.get(self.root + "export/")
        self.assertEqual(exported.status_code, 200)
        self.assertNotIn("runtime", exported.json())
        self.client.force_authenticate(self.owner)
        member = self.campaign.members.get(user=self.player)
        preview = self.client.get(self.root + f"export/?preview={member.id}")
        self.assertNotIn("runtime", preview.json())

    def test_stale_double_submit_cannot_apply_twice(self):
        self.send(action="set_flags", flags=["warned"])
        self.send(action="advance")
        self.send(action="advance")
        self.send(action="add_event", event=gate_event())
        self.campaign.refresh_from_db()
        payload = {"version": self.campaign.version, "action": "resolve", "eventId": "gate", "choice": 0}
        self.assertEqual(self.client.post(self.path, payload, format="json").status_code, 200)
        self.assertEqual(self.client.post(self.path, payload, format="json").status_code, 409)
        self.campaign.refresh_from_db()
        self.assertEqual(self.campaign.runtime["resources"]["gold"], -5)
        self.assertEqual(self.send(action="resolve", eventId="gate", choice=0).status_code, 409)

    def test_invalid_actor_leaves_database_and_audit_unchanged(self):
        other = Campaign.objects.create(owner=self.other, title="Elsewhere")
        foreign = WikiPage.objects.create(campaign=other, kind="npc", title="Hidden")
        for item in (gate_event(str(foreign.id)), gate_event(str(uuid.uuid4()))):
            self.assertEqual(self.send(action="add_event", event=item).status_code, 400)
        self.campaign.refresh_from_db()
        self.assertEqual(self.campaign.version, 1)
        self.assertEqual(self.campaign.runtime, {})
        self.assertEqual(self.campaign.audit_events.count(), 0)

    def test_edit_future_event_but_not_completed_event(self):
        item = gate_event()
        item.update(MinTimeUnit=1, requiredFlags=[])
        self.assertEqual(self.send(action="add_event", event=item).status_code, 200)
        item["title"] = "Edited gate"
        self.assertEqual(self.send(action="update_event", event=item).status_code, 200)
        self.assertEqual(self.send(action="resolve", eventId="gate", choice=0).status_code, 200)
        self.assertEqual(self.send(action="update_event", event=item).status_code, 409)

    def test_used_character_cannot_be_deleted_or_converted_to_lore(self):
        self.send(action="add_event", event=gate_event(str(self.actor.id)))
        path = self.root + f"pages/{self.actor.id}/"
        self.assertEqual(self.client.delete(path, {"version": 1}, format="json").status_code, 409)
        self.assertEqual(self.client.put(path, {"version": 1, "title": "Guard", "kind": "lore"}, format="json").status_code, 409)

    def test_malformed_http_commands_return_domain_errors(self):
        for payload in ([], {"version": 1, "action": []},
                        {"version": 1, "action": "add_event", "event": {"id": "bad", "title": "Bad", "choices": [None]}},
                        {"version": True, "action": "advance"}):
            with self.subTest(payload=payload):
                response = self.client.post(self.path, payload, format="json")
                self.assertIn(response.status_code, (400, 409))
        self.campaign.refresh_from_db()
        self.assertEqual(self.campaign.runtime, {})

    def test_preview_matches_commit_without_mutating_state_version_or_audit(self):
        item = gate_event()
        item.update(MinTimeUnit=1, requiredFlags=[])
        self.send(action="set_value", scope="resources", key="gold", value=12)
        self.send(action="add_event", event=item)
        followup = gate_event()
        followup.update(id="welcome", MinTimeUnit=1, requiredFlags=["safe"], forbiddenFlags=[])
        self.send(action="add_event", event=followup)
        self.campaign.refresh_from_db()
        previous = copy.deepcopy(self.campaign.runtime)
        version = self.campaign.version
        audits = self.campaign.audit_events.count()
        payload = {"version": version, "action": "resolve", "eventId": "gate", "choice": 0}
        response = self.client.post(self.path + "preview/", payload, format="json")
        self.assertEqual(response.status_code, 200, response.content)
        preview = response.json()
        self.assertEqual(preview["resources"]["gold"], 7)
        self.assertEqual(preview["events"][1]["blockers"], [])
        self.campaign.refresh_from_db()
        self.assertEqual(self.campaign.runtime, previous)
        self.assertEqual(self.campaign.version, version)
        self.assertEqual(self.campaign.audit_events.count(), audits)
        saved = self.client.post(self.path, payload, format="json").json()
        self.assertGreater(saved.pop("version"), preview.pop("version"))
        self.assertEqual(saved, preview)
        self.assertEqual(self.client.post(self.path + "preview/", payload, format="json").status_code, 409)

    def test_preview_time_deadlines_and_private_access(self):
        item = gate_event()
        item.update(MinTimeUnit=1, MaxTimeUnit=1, requiredFlags=[])
        self.send(action="add_event", event=item)
        self.campaign.refresh_from_db()
        payload = {"version": self.campaign.version, "action": "advance"}
        response = self.client.post(self.path + "preview/", payload, format="json")
        self.assertEqual(response.status_code, 200)
        self.assertEqual(response.json()["events"][0]["blockers"][0]["code"], "expired")
        self.campaign.refresh_from_db()
        self.assertEqual(self.campaign.runtime["day"], 1)
        for user in (self.player, self.other, None):
            self.client.force_authenticate(user)
            response = self.client.post(self.path + "preview/", payload, format="json")
            self.assertIn(response.status_code, (401, 403, 404))
            self.assertNotIn(item["title"], response.content.decode())
