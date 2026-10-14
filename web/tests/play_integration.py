"""Run inside API container: real player MCP, scoped isolation and human review."""

import asyncio
import copy
import hashlib
import json
import os
import uuid

os.environ.setdefault("DJANGO_SETTINGS_MODULE", "config.settings")
import django

django.setup()
from asgiref.sync import sync_to_async
from django.contrib.auth import get_user_model
from rest_framework.authtoken.models import Token
from rest_framework.test import APIClient
from stories.models import Scenario, PlaySession, PlayEvent
from stories.demo import DOCUMENT_EN
from stories import play
from mcp import ClientSession
from mcp.client.streamable_http import streamablehttp_client
import httpx

suffix = uuid.uuid4().hex[:12]
gm = get_user_model().objects.create_user("play-gm-" + suffix)
player = get_user_model().objects.create_user("play-player-" + suffix)
other = get_user_model().objects.create_user("play-other-" + suffix)
source = copy.deepcopy(DOCUMENT_EN)
source["nodes"][0]["secret"] = "SECRET_MUST_NOT_CROSS_BOUNDARY"
scenario = Scenario.objects.create(owner=gm, title="Absent scout", document=source)
params = {
    "id": str(uuid.uuid4()),
    "scenario_id": str(scenario.id),
    "source_version": 1,
    "player_username": player.username,
    "mode": "absence",
    "language": "en",
    "character": "Arin the scout",
    "brief": "Help Mira before dawn.",
    "instructions": "Ask the player what Arin does.",
    "boundaries": "No permanent harm or rare rewards.",
    "return_condition": "Return to the party at dawn.",
    "gm_notes": "PRIVATE_GM_NOTES_SENTINEL",
    "published_node_ids": ["harbor", "mira"],
    "auto_approve": [],
}
assignment = play.create_play(gm, params)
play_id = assignment["id"]
raw = play.issue_access(player, play_id)["token"]
author = Token.objects.create(user=gm).key
base = os.environ.get("PLAY_TEST_BASE", "http://mcp:8001")
headers = {"Authorization": "Bearer " + raw}
if os.environ.get("PLAY_TEST_HOST"):
    headers["Host"] = os.environ["PLAY_TEST_HOST"]


def value(result):
    assert not result.isError, result
    return json.loads(result.content[0].text)


async def run():
    async with httpx.AsyncClient() as client:
        for url, credential in [
            (base + "/mcp", raw),
            (base + "/play/mcp", author),
            (base + "/play/mcp", "0" * 40),
        ]:
            h = {**headers, "Authorization": "Bearer " + credential}
            r = await client.post(url, headers=h, json={})
            assert r.status_code == 401, (url, r.status_code, r.text)
    async with streamablehttp_client(base + "/play/mcp", headers=headers) as (
        read,
        write,
        _,
    ):
        async with ClientSession(read, write) as client:
            await client.initialize()
            tools = await client.list_tools()
            assert {t.name for t in tools.tools} == {
                "get_play_context",
                "read_play_journal",
                "search_play_memory",
                "commit_play_turn",
                "finish_play_session",
            }
            context = value(await client.call_tool("get_play_context", {}))
            assert context["version"] == 1
            assert set(n["id"] for n in context["materials"]["nodes"]) == {
                "harbor",
                "mira",
            }
            assert "SECRET_MUST_NOT_CROSS_BOUNDARY" not in str(context)
            assert "PRIVATE_GM_NOTES_SENTINEL" not in str(context)
            payload = {
                "operation_id": str(uuid.uuid4()),
                "expected_version": 1,
                "player_action": "I help Mira search the pier.",
                "narration": "Mira offers to remember your help.",
                "memory_markdown": "# Current state\nArin helped Mira.\n\n# Pending\nMira as a contact needs GM approval.",
                "current_scene": "harbor",
                "consequences": [
                    {
                        "category": "relationship",
                        "description": "Mira becomes a contact.",
                    }
                ],
            }
            unknown = await client.call_tool(
                "read_scenario", {"scenario_id": str(scenario.id)}
            )
            assert unknown.isError, "Player catalog exposed an author tool"
            assert (
                await client.call_tool(
                    "read_wiki_page",
                    {"campaign_id": str(uuid.uuid4()), "page_id": str(uuid.uuid4())},
                )
            ).isError, "Player catalog exposed the private campaign wiki"
            retarget = await client.call_tool(
                "get_play_context", {"session_id": str(uuid.uuid4()), "gm": gm.pk}
            )
            assert retarget.isError or value(retarget)["id"] == play_id, (
                "Extra arguments changed scoped assignment"
            )
            forged = {
                **payload,
                "operation_id": str(uuid.uuid4()),
                "consequences": [
                    {
                        "category": "relationship",
                        "description": "Injected approval",
                        "status": "approved",
                    }
                ],
            }
            assert (await client.call_tool("commit_play_turn", forged)).isError
            unpublished = {
                **payload,
                "operation_id": str(uuid.uuid4()),
                "current_scene": "salt",
            }
            assert (await client.call_tool("commit_play_turn", unpublished)).isError
            assert value(await client.call_tool("get_play_context", {}))["version"] == 1
            result = value(await client.call_tool("commit_play_turn", payload))
            assert (
                result["version"] == 2
                and result["consequences"][0]["status"] == "pending"
            )
            effect_id = result["consequences"][0]["id"]
            replay = value(await client.call_tool("commit_play_turn", payload))
            assert (
                replay["replayed"] and replay["consequences"] == result["consequences"]
            )
            bad = await client.call_tool(
                "commit_play_turn", {**payload, "narration": "Changed"}
            )
            assert bad.isError
            stale = await client.call_tool(
                "commit_play_turn", {**payload, "operation_id": str(uuid.uuid4())}
            )
            assert stale.isError
            search = value(
                await client.call_tool("search_play_memory", {"query": "Mira"})
            )
            assert search["memory_excerpt"] and len(search["journal_matches"]) == 1
            page = value(
                await client.call_tool("read_play_journal", {"offset": 0, "limit": 1})
            )
            assert page["total"] == 1
            finish = {
                "operation_id": str(uuid.uuid4()),
                "expected_version": 2,
                "summary": "Arin returns at dawn. A new contact awaits the GM.",
            }
            result = value(await client.call_tool("finish_play_session", finish))
            assert (
                result["status"] == "completed" and result["pending_consequences"] == 1
            )
            assert value(await client.call_tool("finish_play_session", finish))[
                "replayed"
            ]
            new_turn = await client.call_tool(
                "commit_play_turn",
                {**payload, "operation_id": str(uuid.uuid4()), "expected_version": 3},
            )
            assert new_turn.isError
    # A fresh MCP conversation resumes the completed state without a transcript.
    async with streamablehttp_client(base + "/play/mcp", headers=headers) as (
        read,
        write,
        _,
    ):
        async with ClientSession(read, write) as client:
            await client.initialize()
            context = value(await client.call_tool("get_play_context", {}))
            assert (
                context["status"] == "completed"
                and "Arin helped Mira" in context["memory_markdown"]
            )
    return effect_id


try:
    effect_id = asyncio.run(run())
    web = APIClient(HTTP_HOST="localhost")
    web.force_authenticate(player)
    body = {
        "operation_id": str(uuid.uuid4()),
        "expected_version": 3,
        "decisions": [
            {"id": effect_id, "decision": "approved", "note": "Fits the campaign."}
        ],
    }
    assert (
        web.post(f"/api/plays/{play_id}/review/", body, format="json").status_code
        == 404
    )
    assert (
        "PRIVATE_GM_NOTES_SENTINEL"
        not in web.get(f"/api/plays/{play_id}/report/?lang=en").content.decode()
    )
    web.force_authenticate(gm)
    assert (
        web.post(f"/api/plays/{play_id}/review/", body, format="json").status_code
        == 200
    )
    replay = web.post(f"/api/plays/{play_id}/review/", body, format="json")
    assert replay.status_code == 200 and replay.data["replayed"]
    assert (
        "PRIVATE_GM_NOTES_SENTINEL"
        in web.get(f"/api/plays/{play_id}/report/?lang=en").content.decode()
    )
    web.force_authenticate(other)
    assert web.get(f"/api/plays/{play_id}/").status_code == 404
    assert PlayEvent.objects.filter(session_id=play_id, kind="turn").count() == 1
    assert PlaySession.objects.get(pk=play_id).version == 4
    scenario.refresh_from_db()
    assert scenario.version == 1
    play.issue_access(player, play_id, revoke=True)

    async def revoked():
        async with httpx.AsyncClient() as client:
            r = await client.post(base + "/play/mcp", headers=headers, json={})
            assert r.status_code == 401

    asyncio.run(revoked())
    print(
        "PASS: real player MCP catalog, endpoint isolation, secret filtering, turn retries/conflicts, fresh-client resume, finish, human review, role-filtered reports and revocation"
    )
finally:
    gm.delete()
    player.delete()
    other.delete()
