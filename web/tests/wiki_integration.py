"""Run inside API container: owner-only campaign wiki through real author MCP."""

import asyncio
import json
import os
import uuid

os.environ.setdefault("DJANGO_SETTINGS_MODULE", "config.settings")
import django

django.setup()
from django.contrib.auth import get_user_model
from rest_framework.authtoken.models import Token
from stories.models import Campaign, WikiPage, CampaignMember
from mcp import ClientSession
from mcp.client.streamable_http import streamablehttp_client
from asgiref.sync import sync_to_async

suffix = uuid.uuid4().hex[:10]
owner = get_user_model().objects.create_user("wiki-mcp-" + suffix)
other = get_user_model().objects.create_user("wiki-other-" + suffix)
gm = get_user_model().objects.create_user("wiki-gm-" + suffix)
c = Campaign.objects.create(owner=owner, title="Shared world")
CampaignMember.objects.create(campaign=c, user=gm, role="gm")
CampaignMember.objects.create(campaign=c, user=other, role="player")
gm_token = Token.objects.create(user=gm).key
p = WikiPage.objects.create(
    campaign=c,
    title="Mira",
    kind="npc",
    body="A cartographer",
    gm_notes="PRIVATE_WIKI_MCP_SENTINEL",
)
owner_token = Token.objects.create(user=owner).key
other_token = Token.objects.create(user=other).key


def data(result):
    assert not result.isError, result
    structured = result.structuredContent
    if isinstance(structured, dict):
        return structured["result"] if set(structured) == {"result"} else structured
    return json.loads(next(x.text for x in result.content if x.type == "text"))


def reset_mcp_rate(token):
    """Test-only: the full GM workflow exceeds one token's 60 calls/minute; production limits stay."""
    import hashlib
    from redis import Redis
    connection = Redis.from_url(os.environ.get("REDIS_URL", "redis://localhost:6379/0"))
    keys = list(connection.scan_iter(match=f"mcp:rate:{hashlib.sha256(token.encode()).hexdigest()}:*"))
    if keys:
        connection.delete(*keys)


async def author_workflow(client):
    """Everything a GM builds, from an empty account state to deletion, through author MCP only."""
    call = lambda name, args: client.call_tool(name, args)
    cid = str(uuid.uuid4())
    created = data(await call("create_campaign", {"campaign_id": cid, "title": "MCP world", "summary": "Test"}))
    assert created["id"] == cid
    assert data(await call("create_campaign", {"campaign_id": cid, "title": "MCP world", "summary": "Test"}))["id"] == cid
    assert (await call("create_campaign", {"campaign_id": cid, "title": "Other", "summary": ""})).isError
    assert (await call("create_campaign", {"campaign_id": str(uuid.uuid4()), "title": "  "})).isError
    npc, place = str(uuid.uuid4()), str(uuid.uuid4())
    page = {"campaign_id": cid, "page_id": npc, "title": "Teo", "kind": "npc", "body": "Ripperdoc", "gm_notes": "secret"}
    assert data(await call("create_wiki_page", page))["version"] == 1
    assert data(await call("create_wiki_page", page))["id"] == npc  # identical retry
    assert (await call("create_wiki_page", {**page, "body": "changed"})).isError
    assert (await call("create_wiki_page", {**page, "page_id": str(uuid.uuid4()), "kind": "spaceship"})).isError
    await call("create_wiki_page", {"campaign_id": cid, "page_id": place, "title": "Clinic", "kind": "location"})
    rid = str(uuid.uuid4())
    relation = {"campaign_id": cid, "relation_id": rid, "source": npc, "target": place, "label": "works in"}
    assert data(await call("create_wiki_relation", relation))["id"] == rid
    assert (await call("create_wiki_relation", {**relation, "relation_id": str(uuid.uuid4()), "target": npc})).isError
    published = data(await call("publish_wiki_page",
                                {"campaign_id": cid, "page_id": npc, "expected_version": 1, "audience": "all"}))
    assert published["published"]["body"] == "Ripperdoc" and "gm_notes" not in published["published"]
    assert (await call("publish_wiki_page",
                       {"campaign_id": cid, "page_id": npc, "expected_version": 1, "audience": "all"})).isError
    assert (await call("publish_wiki_page",
                       {"campaign_id": cid, "page_id": place, "expected_version": 1, "audience": "everyone"})).isError
    data(await call("publish_wiki_relation",
                    {"campaign_id": cid, "relation_id": rid, "expected_version": 1, "audience": "all"}))
    detail = data(await call("read_campaign", {"campaign_id": cid}))
    assert {x["id"] for x in detail["pages"]} == {npc, place} and len(detail["relations"]) == 1
    runtime = data(await call("read_campaign_runtime", {"campaign_id": cid}))
    event = {"id": "first", "title": "First job", "choices": [
        {"text": "Done", "outcomes": [{"type": "AddFlag", "targetId": "done"}]}], "InvolvedActors": [npc]}
    command = {"campaign_id": cid, "expected_version": runtime["version"],
               "command": {"action": "add_event", "event": event}}
    preview = data(await call("run_campaign_command", {**command, "preview": True}))
    assert preview["version"] == runtime["version"] and preview["events"][0]["id"] == "first"
    assert data(await call("read_campaign_runtime", {"campaign_id": cid}))["events"] == []
    saved = data(await call("run_campaign_command", command))
    assert saved["version"] == runtime["version"] + 1
    assert (await call("run_campaign_command", command)).isError  # stale version
    foreign = {**event, "id": "foreign", "InvolvedActors": [str(uuid.uuid4())]}
    assert (await call("run_campaign_command", {**command, "expected_version": saved["version"],
                                                "command": {"action": "add_event", "event": foreign}})).isError
    resolved = data(await call("run_campaign_command", {"campaign_id": cid, "expected_version": saved["version"],
                                                        "command": {"action": "resolve", "eventId": "first", "choice": 0}}))
    assert "done" in resolved["flags"]
    # Engine characters cannot be deleted from under the runtime; plain pages can.
    page_now = data(await call("read_wiki_page", {"campaign_id": cid, "page_id": npc}))
    assert (await call("delete_wiki_page", {"campaign_id": cid, "page_id": npc,
                                            "expected_version": page_now["version"]})).isError
    sid = str(uuid.uuid4())
    scenario = data(await call("create_scenario", {"scenario_id": sid, "title": "Job one",
                                                   "document": {"schema": "narrative-web/1", "start": "start", "edges": [],
                                                                "nodes": [{"id": "start", "kind": "scene", "title": "Start"}]}}))
    copied = data(await call("copy_wiki_page_to_scenario", {
        "campaign_id": cid, "page_id": place, "source_version": 1, "scenario_id": sid,
        "scenario_version": scenario["version"]}))
    assert any(n["id"] == "wiki-" + place for n in copied["document"]["nodes"])
    from stories.test_rulesets import mini
    assert (await call("set_campaign_ruleset", {"campaign_id": cid, "expected_revision": 0,
                                               "document": {**mini(), "schema": "x"}})).isError
    ruleset = data(await call("set_campaign_ruleset", {"campaign_id": cid, "expected_revision": 0, "document": mini()}))
    assert ruleset["revision"] == 1
    assert data(await call("read_campaign_ruleset", {"campaign_id": cid}))["document"]["id"] == "mini"
    did = str(uuid.uuid4())
    draft = data(await call("create_character_draft", {"campaign_id": cid, "draft_id": did, "method": "pelna"}))
    for command in ({"action": "choose_role", "role": "wojownik"}, {"action": "set_stats", "values": {"SIL": 5, "WOL": 5}},
                    {"action": "roll_table", "table": "dom"}, {"action": "set_name", "name": "Strażnik"}, {"action": "submit"}):
        draft = data(await call("run_character_command", {"campaign_id": cid, "draft_id": did,
                                                          "expected_version": draft["version"], "command": command}))
    assert draft["status"] == "approved" and draft["page"]
    assert data(await call("read_wiki_page", {"campaign_id": cid, "page_id": draft["page"]}))["kind"] == "pc"
    assert [d["status"] for d in data(await call("list_character_drafts", {"campaign_id": cid}))] == ["approved"]
    assert (await call("delete_character_draft", {"campaign_id": cid, "draft_id": did,
                                                  "expected_version": draft["version"]})).isError
    await sync_to_async(reset_mcp_rate)(owner_token)
    import base64, io
    from PIL import Image
    raw = io.BytesIO()
    Image.new("RGB", (8, 6), (10, 200, 90)).save(raw, "PNG")
    iid = str(uuid.uuid4())
    uploaded = data(await call("upload_campaign_image", {"campaign_id": cid, "image_id": iid, "caption": "Neon",
                                                          "data_base64": base64.b64encode(raw.getvalue()).decode()}))
    assert (uploaded["id"], uploaded["gallery"], uploaded["width"]) == (iid, "gm", 8)
    assert (await call("upload_campaign_image", {"campaign_id": cid, "image_id": str(uuid.uuid4()), "data_base64": "@@@"})).isError
    assert data(await call("update_campaign_image", {"campaign_id": cid, "image_id": iid, "gallery": "players"}))["gallery"] == "players"
    assert data(await call("set_campaign_cover", {"campaign_id": cid, "image_id": iid}))["cover"] == iid
    page_now = data(await call("read_wiki_page", {"campaign_id": cid, "page_id": place}))
    assert data(await call("set_page_image", {"campaign_id": cid, "page_id": place, "expected_version": page_now["version"],
                                              "image_id": iid}))["image"] == iid
    listed = data(await call("list_campaign_images", {"campaign_id": cid}))
    assert listed[0]["usage"]["cover"] and listed[0]["usage"]["pages"] == [place]
    assert data(await call("delete_campaign_image", {"campaign_id": cid, "image_id": iid}))["detached"] == {"cover": True, "pages": 1}
    data(await call("delete_wiki_relation", {"campaign_id": cid, "relation_id": rid}))
    place_version = data(await call("read_wiki_page", {"campaign_id": cid, "page_id": place}))["version"]
    assert data(await call("delete_wiki_page", {"campaign_id": cid, "page_id": place, "expected_version": place_version}))["deleted"]
    detail = data(await call("read_campaign", {"campaign_id": cid}))
    renamed = data(await call("update_campaign", {"campaign_id": cid, "expected_version": detail["version"],
                                                  "title": "Renamed", "appearance": "cyberpunk"}))
    assert (renamed["title"], renamed["appearance"], renamed["page_template"]) == ("Renamed", "cyberpunk", "fantasy")
    assert (await call("update_campaign", {"campaign_id": cid, "expected_version": detail["version"] + 1,
                                           "title": "Renamed", "appearance": "steampunk"})).isError
    assert (await call("delete_campaign", {"campaign_id": cid, "expected_version": detail["version"]})).isError
    assert data(await call("delete_campaign", {"campaign_id": cid, "expected_version": detail["version"] + 1}))["deleted"]
    template = {"schema": "narrative-campaign-template/1", "title": "Imported", "pages": [
        {"id": "hero", "kind": "pc", "title": "Hero"}], "initial": {"actors": {"hero": {"HP": 40}}}}
    imported = data(await call("import_campaign_template", {"campaign_id": str(uuid.uuid4()), "document": template}))
    assert imported["title"] == "Imported"
    assert (await call("import_campaign_template", {"campaign_id": str(uuid.uuid4()),
                                                    "document": {**template, "schema": "x"}})).isError
    await sync_to_async(lambda: Campaign.objects.filter(pk=imported["id"]).delete())()
    from stories.models import Scenario
    await sync_to_async(lambda: Scenario.objects.filter(pk=sid).delete())()


async def run():
    async with streamablehttp_client(
        "http://mcp:8001/mcp", headers={"Authorization": "Bearer " + owner_token}
    ) as (read, write, _):
        async with ClientSession(read, write) as client:
            await client.initialize()
            names = {t.name for t in (await client.list_tools()).tools}
            assert {
                "list_campaigns",
                "search_wiki",
                "read_wiki_page",
                "save_wiki_page",
            } <= names
            campaigns = data(await client.call_tool("list_campaigns", {}))
            assert any(x["id"] == str(c.id) for x in campaigns)
            hits = data(
                await client.call_tool(
                    "search_wiki", {"campaign_id": str(c.id), "query": "cartographer"}
                )
            )
            assert hits[0]["id"] == str(p.id) and "gm_notes" not in hits[0]
            page = data(
                await client.call_tool(
                    "read_wiki_page", {"campaign_id": str(c.id), "page_id": str(p.id)}
                )
            )
            assert page["gm_notes"] == "PRIVATE_WIKI_MCP_SENTINEL"
            request = {
                "campaign_id": str(c.id),
                "page_id": str(p.id),
                "expected_version": 1,
                "title": "Mira",
                "kind": "npc",
                "body": "Updated by the author",
                "gm_notes": page["gm_notes"],
            }
            assert (
                data(await client.call_tool("save_wiki_page", request))["version"] == 2
            )
            assert (await client.call_tool("save_wiki_page", request)).isError
            await author_workflow(client)
    async with streamablehttp_client(
        "http://mcp:8001/mcp", headers={"Authorization": "Bearer " + other_token}
    ) as (read, write, _):
        async with ClientSession(read, write) as client:
            await client.initialize()
            assert (
                await client.call_tool(
                    "read_wiki_page", {"campaign_id": str(c.id), "page_id": str(p.id)}
                )
            ).isError
            assert data(await client.call_tool("list_campaigns", {})) == []
            # A player membership never reaches author mutations or the GM projection.
            for name, args in [
                ("read_campaign", {"campaign_id": str(c.id)}),
                ("read_campaign_runtime", {"campaign_id": str(c.id)}),
                ("create_wiki_page", {"campaign_id": str(c.id), "page_id": str(uuid.uuid4()),
                                      "title": "Intruder", "kind": "lore"}),
                ("delete_wiki_page", {"campaign_id": str(c.id), "page_id": str(p.id), "expected_version": 2}),
                ("publish_wiki_page", {"campaign_id": str(c.id), "page_id": str(p.id),
                                       "expected_version": 2, "audience": "all"}),
                ("run_campaign_command", {"campaign_id": str(c.id), "expected_version": 0,
                                          "command": {"action": "advance"}}),
                ("delete_campaign", {"campaign_id": str(c.id), "expected_version": 0}),
                ("set_campaign_ruleset", {"campaign_id": str(c.id), "expected_revision": 0, "document": {}}),
                ("read_campaign_ruleset", {"campaign_id": str(c.id)}),
                ("list_character_drafts", {"campaign_id": str(c.id)}),
                ("list_campaign_images", {"campaign_id": str(c.id)}),
                ("upload_campaign_image", {"campaign_id": str(c.id), "image_id": str(uuid.uuid4()), "data_base64": "AA=="}),
                ("create_character_draft", {"campaign_id": str(c.id), "draft_id": str(uuid.uuid4()), "method": "x"}),
            ]:
                assert (await client.call_tool(name, args)).isError, name
    async with streamablehttp_client(
        "http://mcp:8001/mcp", headers={"Authorization": "Bearer " + gm_token}
    ) as (read, write, _):
        async with ClientSession(read, write) as client:
            await client.initialize()
            assert (
                data(
                    await client.call_tool(
                        "read_wiki_page",
                        {"campaign_id": str(c.id), "page_id": str(p.id)},
                    )
                )["gm_notes"]
                == "PRIVATE_WIKI_MCP_SENTINEL"
            )
            await sync_to_async(
                lambda: CampaignMember.objects.filter(campaign=c, user=gm).update(
                    role="player"
                )
            )()
            assert (
                await client.call_tool(
                    "read_wiki_page", {"campaign_id": str(c.id), "page_id": str(p.id)}
                )
            ).isError
            assert (
                await client.call_tool(
                    "search_wiki", {"campaign_id": str(c.id), "query": "Mira"}
                )
            ).isError
            assert (await client.call_tool("save_wiki_page", request)).isError
    assert (
        await sync_to_async(lambda: WikiPage.objects.get(pk=p.id).body)()
        == "Updated by the author"
    )
    print(
        "PASS: real wiki MCP discovery/search/read/edit/version conflict, player exclusion, co-GM access and immediate demotion"
    )


try:
    asyncio.run(run())
finally:
    get_user_model().objects.filter(pk__in=[owner.pk, other.pk, gm.pk]).delete()
