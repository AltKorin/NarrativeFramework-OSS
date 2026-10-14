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
from stories.models import Campaign, WikiPage
from mcp import ClientSession
from mcp.client.streamable_http import streamablehttp_client
from asgiref.sync import sync_to_async

suffix = uuid.uuid4().hex[:10]
owner = get_user_model().objects.create_user("wiki-mcp-" + suffix)
other = get_user_model().objects.create_user("wiki-other-" + suffix)
c = Campaign.objects.create(owner=owner, title="Shared world")
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
    assert (
        await sync_to_async(lambda: WikiPage.objects.get(pk=p.id).body)()
        == "Updated by the author"
    )
    print(
        "PASS: real wiki MCP discovery/search/read/edit/version conflict and foreign-owner isolation"
    )


try:
    asyncio.run(run())
finally:
    get_user_model().objects.filter(pk__in=[owner.pk, other.pk]).delete()
