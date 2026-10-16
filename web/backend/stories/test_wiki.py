"""Campaign ownership, race protection and explicit snapshot boundaries."""

import copy
import uuid
from concurrent.futures import ThreadPoolExecutor
from threading import Barrier
from unittest import skipUnless
from unittest.mock import patch

from django.contrib.auth import get_user_model
from django.core.cache import cache
from django.db import close_old_connections, connection
from django.test import TestCase, TransactionTestCase
from rest_framework.test import APIClient

from . import wiki
from .demo import DOCUMENT
from .domain_errors import DomainError
from .models import Campaign, Scenario, WikiPage, WikiRelation


class WikiTests(TestCase):
    def setUp(self):
        cache.clear()
        self.owner = get_user_model().objects.create_user("wiki-owner")
        self.other = get_user_model().objects.create_user("wiki-other")
        self.client = APIClient()
        self.client.force_authenticate(self.owner)
        self.campaign = Campaign.objects.create(owner=self.owner, title="The coast")
        self.page = WikiPage.objects.create(
            campaign=self.campaign,
            title="Mira",
            kind="npc",
            body="## Cartographer\n\nKnows the harbor.",
            gm_notes="PRIVATE_WIKI_SENTINEL",
        )
        self.second = WikiPage.objects.create(
            campaign=self.campaign, title="Harbor", kind="location"
        )
        self.root = f"/api/campaigns/{self.campaign.id}/"

    def test_character_and_timeline_pages_round_trip(self):
        for kind in ("npc", "pc", "timeline"):
            with self.subTest(kind=kind):
                body = "| When / attribute | Value |\n| --- | --- |\n| Day 3 / Health | 12 |"
                response = self.client.post(
                    self.root + "pages/",
                    {"id": str(uuid.uuid4()), "title": kind, "kind": kind,
                     "body": body, "gm_notes": "Secret trigger"},
                    format="json",
                )
                self.assertEqual(response.status_code, 201, response.content)
                page_id = response.json()["id"]
                saved = self.client.get(self.root + f"pages/{page_id}/").json()
                self.assertEqual(saved["kind"], kind)
                self.assertEqual(saved["body"], body)
                self.assertEqual(saved["gm_notes"], "Secret trigger")

    def test_anonymous_cannot_list_or_read_wiki(self):
        client = APIClient()
        for path in (
            "/api/campaigns/",
            self.root,
            self.root + f"pages/{self.page.id}/",
            self.root + "export/",
        ):
            self.assertIn(client.get(path).status_code, (401, 403))

    def test_other_owner_cannot_read_mutate_export_or_link(self):
        self.client.force_authenticate(self.other)
        self.assertEqual(self.client.get("/api/campaigns/").json(), [])
        for path in (
            self.root,
            self.root + f"pages/{self.page.id}/",
            self.root + "pages/?q=Mira",
            self.root + "export/",
        ):
            self.assertEqual(self.client.get(path).status_code, 404)
        self.assertEqual(
            self.client.put(
                self.root + f"pages/{self.page.id}/",
                {"title": "Changed", "version": 1},
                format="json",
            ).status_code,
            404,
        )
        self.assertEqual(
            self.client.post(
                self.root + "relations/",
                {
                    "id": str(uuid.uuid4()),
                    "source": str(self.page.id),
                    "target": str(self.second.id),
                    "label": "lives in",
                },
                format="json",
            ).status_code,
            404,
        )

    def test_creation_replays_are_bound_to_content_and_owner(self):
        body = {"id": str(uuid.uuid4()), "title": "New world"}
        self.assertEqual(
            self.client.post("/api/campaigns/", body, format="json").status_code, 201
        )
        self.assertEqual(
            self.client.post("/api/campaigns/", body, format="json").status_code, 200
        )
        self.assertEqual(
            self.client.post(
                "/api/campaigns/", {**body, "title": "Changed"}, format="json"
            ).status_code,
            409,
        )
        self.client.force_authenticate(self.other)
        self.assertEqual(
            self.client.post("/api/campaigns/", body, format="json").status_code, 409
        )

    def test_page_replay_after_edit_returns_current_content(self):
        body = {
            "id": str(uuid.uuid4()),
            "title": "Lighthouse",
            "kind": "location",
            "body": "Before",
        }
        first = self.client.post(self.root + "pages/", body, format="json")
        self.assertEqual(first.status_code, 201)
        changed = self.client.put(
            self.root + f"pages/{body['id']}/",
            {**body, "body": "After", "version": 1},
            format="json",
        )
        self.assertEqual(changed.status_code, 200)
        replay = self.client.post(self.root + "pages/", body, format="json")
        self.assertEqual(replay.status_code, 200)
        self.assertEqual(replay.json()["body"], "After")
        self.assertEqual(
            self.client.post(
                self.root + "pages/", {**body, "body": "Different"}, format="json"
            ).status_code,
            409,
        )

    def test_stale_or_boolean_version_cannot_overwrite_or_delete(self):
        path = self.root + f"pages/{self.page.id}/"
        body = {
            "title": "Edited",
            "kind": "npc",
            "body": "New",
            "gm_notes": "Still private",
            "version": 1,
        }
        self.assertEqual(self.client.put(path, body, format="json").status_code, 200)
        for version in (1, True, None):
            self.assertEqual(
                self.client.put(
                    path, {**body, "version": version}, format="json"
                ).status_code,
                409,
            )
            self.assertEqual(
                self.client.delete(
                    path, {"version": version}, format="json"
                ).status_code,
                409,
            )
        self.page.refresh_from_db()
        self.assertEqual(self.page.body, "New")
        self.assertEqual(self.page.version, 2)

    def test_malformed_pages_fail_without_writes(self):
        bodies = [
            [],
            {"title": []},
            {"title": ""},
            {"title": "Bad\0title"},
            {"title": "Bad", "body": {}},
            {"title": "Bad", "kind": []},
            {"title": "Bad", "gm_notes": "x" * 12001},
        ]
        for body in bodies:
            cache.clear()
            data = {**body, "id": str(uuid.uuid4())} if isinstance(body, dict) else body
            with self.subTest(type=type(body).__name__):
                self.assertEqual(
                    self.client.post(
                        self.root + "pages/", data, format="json"
                    ).status_code,
                    400,
                )
        self.assertEqual(self.campaign.pages.count(), 2)

    def test_search_indexes_body_but_does_not_return_private_notes(self):
        response = self.client.get(self.root + "pages/?q=harbor")
        self.assertEqual(len(response.json()), 2)
        self.assertNotIn("gm_notes", response.json()[0])
        self.assertEqual(
            self.client.get(self.root + "pages/?q=PRIVATE_WIKI_SENTINEL").json(), []
        )
        self.assertEqual(self.client.get(self.root + "pages/?q=%00").status_code, 400)

    def test_cross_campaign_and_self_relations_are_rejected(self):
        other_campaign = Campaign.objects.create(owner=self.owner, title="Elsewhere")
        foreign = WikiPage.objects.create(campaign=other_campaign, title="Other harbor")
        body = {
            "id": str(uuid.uuid4()),
            "source": str(self.page.id),
            "target": str(foreign.id),
            "label": "travels to",
        }
        self.assertEqual(
            self.client.post(self.root + "relations/", body, format="json").status_code,
            404,
        )
        self.assertEqual(
            self.client.post(
                self.root + "relations/",
                {**body, "target": str(self.page.id)},
                format="json",
            ).status_code,
            400,
        )
        self.assertFalse(WikiRelation.objects.exists())

    def test_relationship_replay_backlinks_and_deletion(self):
        body = {
            "id": str(uuid.uuid4()),
            "source": str(self.page.id),
            "target": str(self.second.id),
            "label": "lives in",
        }
        self.assertEqual(
            self.client.post(self.root + "relations/", body, format="json").status_code,
            201,
        )
        self.assertEqual(
            self.client.post(self.root + "relations/", body, format="json").status_code,
            200,
        )
        self.assertEqual(
            self.client.post(
                self.root + "relations/",
                {**body, "id": str(uuid.uuid4())},
                format="json",
            ).status_code,
            200,
        )
        self.assertEqual(
            self.client.post(
                self.root + "relations/", {**body, "label": "Changed"}, format="json"
            ).status_code,
            409,
        )
        detail = self.client.get(self.root + f"pages/{self.second.id}/").json()
        self.assertEqual(detail["relations"][0]["source"], str(self.page.id))
        self.assertEqual(WikiRelation.objects.count(), 1)
        self.assertEqual(
            self.client.delete(self.root + f"relations/{body['id']}/").status_code, 200
        )
        self.assertEqual(WikiRelation.objects.count(), 0)

    def test_export_contains_owned_private_material_and_source_format(self):
        response = self.client.get(self.root + "export/")
        self.assertEqual(response.status_code, 200)
        data = response.json()
        self.assertEqual(data["schema"], "narrative-wiki/2")
        self.assertIn("PRIVATE_WIKI_SENTINEL", response.content.decode())
        self.assertIn("attachment", response["Content-Disposition"])

    def test_wiki_copy_is_versioned_snapshot_with_notes_only_in_secret(self):
        scenario = Scenario.objects.create(
            owner=self.owner, title="Adventure", document=copy.deepcopy(DOCUMENT)
        )
        path = self.root + f"pages/{self.page.id}/use/"
        data = {"scenario_id": str(scenario.id), "version": 1, "source_version": 1}
        response = self.client.post(path, data, format="json")
        self.assertEqual(response.status_code, 200)
        node = response.json()["document"]["nodes"][-1]
        self.assertEqual(node["secret"], "PRIVATE_WIKI_SENTINEL")
        self.assertNotIn("PRIVATE_WIKI_SENTINEL", node["text"])
        self.assertEqual(node["wiki_origin"]["version"], 1)
        self.page.body = "Updated world"
        self.page.save()
        scenario.refresh_from_db()
        self.assertNotEqual(scenario.document["nodes"][-1]["text"], self.page.body)
        self.assertEqual(
            self.client.post(path, {**data, "version": 2}, format="json").status_code,
            409,
        )
        self.page.delete()
        scenario.refresh_from_db()
        self.assertEqual(scenario.document["nodes"][-1]["title"], "Mira")

    def test_wiki_copy_cannot_target_foreign_demo_or_stale_scenario(self):
        for owner, demo, version, expected in (
            (self.other, False, 1, 404),
            (self.owner, True, 1, 404),
            (self.owner, False, 2, 409),
        ):
            scenario = Scenario.objects.create(
                owner=owner,
                title="Target",
                document=copy.deepcopy(DOCUMENT),
                is_demo=demo,
            )
            r = self.client.post(
                self.root + f"pages/{self.page.id}/use/",
                {
                    "scenario_id": str(scenario.id),
                    "version": version,
                    "source_version": 1,
                },
                format="json",
            )
            self.assertEqual(r.status_code, expected)
            scenario.refresh_from_db()
            self.assertEqual(scenario.version, 1)
            self.assertEqual(len(scenario.document["nodes"]), len(DOCUMENT["nodes"]))

    def test_copy_rejects_source_changes_since_the_author_read_it(self):
        scenario = Scenario.objects.create(
            owner=self.owner, title="Snapshot", document=copy.deepcopy(DOCUMENT)
        )
        wiki.save_page(
            self.owner,
            self.campaign.id,
            self.page.id,
            {"version": 1, "title": "Mira", "body": "Unreviewed change"},
        )
        response = self.client.post(
            self.root + f"pages/{self.page.id}/use/",
            {"scenario_id": str(scenario.id), "version": 1, "source_version": 1},
            format="json",
        )
        self.assertEqual(response.status_code, 409)
        scenario.refresh_from_db()
        self.assertEqual(scenario.version, 1)
        self.assertEqual(scenario.document, DOCUMENT)

    def test_campaign_settings_and_delete_require_current_owner_version(self):
        self.assertEqual(
            self.client.put(
                self.root,
                {"version": 1, "title": "Renamed", "summary": "New lore"},
                format="json",
            ).status_code,
            200,
        )
        persisted = self.client.get(self.root).json()
        self.assertEqual(persisted["title"], "Renamed")
        self.assertEqual(persisted["summary"], "New lore")
        self.assertEqual(
            self.client.delete(self.root, {"version": 1}, format="json").status_code,
            409,
        )
        self.client.force_authenticate(self.other)
        self.assertEqual(
            self.client.delete(self.root, {"version": 2}, format="json").status_code,
            404,
        )
        self.client.force_authenticate(self.owner)
        self.assertEqual(
            self.client.delete(self.root, {"version": 2}, format="json").status_code,
            200,
        )
        self.assertFalse(WikiPage.objects.filter(campaign_id=self.campaign.id).exists())
        self.assertFalse(Campaign.objects.filter(pk=self.campaign.id).exists())

    def test_page_delete_removes_relationships_without_touching_other_pages(self):
        WikiRelation.objects.create(
            campaign=self.campaign, source=self.page, target=self.second, label="knows"
        )
        self.assertEqual(
            self.client.delete(
                self.root + f"pages/{self.page.id}/", {"version": 1}, format="json"
            ).status_code,
            200,
        )
        self.assertFalse(WikiRelation.objects.exists())
        self.assertTrue(WikiPage.objects.filter(pk=self.second.id).exists())


@skipUnless(connection.vendor == "postgresql", "Requires PostgreSQL row locks")
class WikiConcurrencyTests(TransactionTestCase):
    def setUp(self):
        self.a = get_user_model().objects.create_user("wiki-race-a")
        self.b = get_user_model().objects.create_user("wiki-race-b")

    def test_same_campaign_id_across_owners_never_raises_database_error(self):
        barrier = Barrier(2)
        identifier = str(uuid.uuid4())
        original = Campaign.objects.create

        def collide(*args, **kwargs):
            barrier.wait(timeout=10)
            return original(*args, **kwargs)

        def worker(user):
            close_old_connections()
            try:
                wiki.create_campaign(user, {"id": identifier, "title": "Race"})
                return 201
            except DomainError as e:
                return e.status
            finally:
                close_old_connections()

        with (
            patch.object(Campaign.objects, "create", side_effect=collide),
            ThreadPoolExecutor(max_workers=2) as pool,
        ):
            results = list(pool.map(worker, [self.a, self.b]))
        self.assertEqual(sorted(results), [201, 409])
        self.assertEqual(Campaign.objects.count(), 1)

    def test_same_page_id_across_campaigns_never_raises_database_error(self):
        campaigns = [
            Campaign.objects.create(owner=self.a, title="A"),
            Campaign.objects.create(owner=self.b, title="B"),
        ]
        barrier = Barrier(2)
        identifier = str(uuid.uuid4())
        original = WikiPage.objects.create

        def collide(*args, **kwargs):
            barrier.wait(timeout=10)
            return original(*args, **kwargs)

        def worker(pair):
            user, c = pair
            close_old_connections()
            try:
                wiki.create_page(user, c.id, {"id": identifier, "title": "Shared ID"})
                return 201
            except DomainError as e:
                return e.status
            finally:
                close_old_connections()

        with (
            patch.object(WikiPage.objects, "create", side_effect=collide),
            ThreadPoolExecutor(max_workers=2) as pool,
        ):
            results = list(pool.map(worker, zip([self.a, self.b], campaigns)))
        self.assertEqual(sorted(results), [201, 409])
        self.assertEqual(WikiPage.objects.count(), 1)

    def test_concurrent_edit_has_one_winner(self):
        c = Campaign.objects.create(owner=self.a, title="A")
        p = WikiPage.objects.create(campaign=c, title="Page")
        barrier = Barrier(2)

        def worker(title):
            close_old_connections()
            barrier.wait(timeout=10)
            try:
                wiki.save_page(self.a, c.id, p.id, {"title": title, "version": 1})
                return 200
            except DomainError as e:
                return e.status
            finally:
                close_old_connections()

        with ThreadPoolExecutor(max_workers=2) as pool:
            results = list(pool.map(worker, ["One", "Two"]))
        self.assertEqual(sorted(results), [200, 409])
        p.refresh_from_db()
        self.assertEqual(p.version, 2)
