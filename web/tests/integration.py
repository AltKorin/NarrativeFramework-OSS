"""Run inside the API container against its real PostgreSQL and the MCP service."""

import os, sys, uuid, asyncio, copy, json

os.environ.setdefault("DJANGO_SETTINGS_MODULE", "config.settings")
import django

django.setup()
from django.contrib.auth import get_user_model
from rest_framework.authtoken.models import Token
from stories.models import Scenario
from stories.demo import DOCUMENT
import httpx
from mcp import ClientSession
from mcp.client.streamable_http import streamablehttp_client
from redis.asyncio import Redis

suffix = uuid.uuid4().hex[:10]
alice = get_user_model().objects.create_user(
    "integration-" + suffix, password=uuid.uuid4().hex
)
bob = get_user_model().objects.create_user("other-" + suffix, password=uuid.uuid4().hex)
token = Token.objects.create(user=alice).key
other = Token.objects.create(user=bob).key
scenario = Scenario.objects.create(owner=alice, title="Integration", document=DOCUMENT)
scenario_id = str(scenario.pk)
from stories.mcp_server import ADMIT


async def run():
    url = "http://mcp:8001/mcp"
    async with httpx.AsyncClient(timeout=15) as client:
        r = await client.post(url, json={})
        assert r.status_code == 401, r.text
        r = await client.post(
            url, headers={"Authorization": "Bearer " + "0" * 40}, json={}
        )
        assert r.status_code == 401
        r = await client.post(
            url, headers={"Authorization": "Bearer " + token}, content=b"x" * 300001
        )
        assert r.status_code == 413, r.text
    async with streamablehttp_client(
        url, headers={"Authorization": "Bearer " + token}
    ) as (read, write, _):
        async with ClientSession(read, write) as session:
            await session.initialize()
            tools = await session.list_tools()
            assert {t.name for t in tools.tools} == {
                "list_scenarios",
                "read_scenario",
                "save_scenario",
                "validate_scenario",
                "list_campaigns",
                "search_wiki",
                "read_wiki_page",
                "save_wiki_page",
            }
            result = await session.call_tool(
                "read_scenario", {"scenario_id": scenario_id}
            )
            assert not result.isError, result
            result = await session.call_tool(
                "save_scenario",
                {
                    "scenario_id": scenario_id,
                    "expected_version": 1,
                    "title": "MCP changed",
                    "document": DOCUMENT,
                },
            )
            assert not result.isError, result
            stale = await session.call_tool(
                "save_scenario",
                {
                    "scenario_id": scenario_id,
                    "expected_version": 1,
                    "title": "Lost update",
                    "document": DOCUMENT,
                },
            )
            assert stale.isError, stale
            checks = await session.call_tool(
                "validate_scenario", {"scenario_id": scenario_id}
            )
            assert not checks.isError
    async with streamablehttp_client(
        url, headers={"Authorization": "Bearer " + other}
    ) as (read, write, _):
        async with ClientSession(read, write) as session:
            await session.initialize()
            result = await session.call_tool(
                "read_scenario", {"scenario_id": scenario_id}
            )
            assert result.isError, result
    redis = Redis.from_url(os.environ["REDIS_URL"])
    import time

    prefix = "integration:" + suffix
    keys = [prefix + ":rate", prefix + ":leases", prefix + ":global"]
    admitted = await asyncio.gather(
        *[redis.eval(ADMIT, 3, *keys, time.time(), str(i), 60, 2, 8) for i in range(12)]
    )
    assert sum(admitted) == 2, admitted
    await redis.delete(*keys)
    # Real HTTP 429, using this test token's rate key only.
    import hashlib

    rate = f"mcp:rate:{hashlib.sha256(token.encode()).hexdigest()}:{int(time.time() // 60)}"
    await redis.set(rate, 100, ex=60)
    async with httpx.AsyncClient() as client:
        r = await client.post(
            url, headers={"Authorization": "Bearer " + token}, json={}
        )
        assert r.status_code == 429, r.text
    await redis.delete(rate)
    await redis.aclose()
    print(
        "PASS: real MCP initialize/tools/read/save/conflict/ownership/413/429 and atomic Redis concurrency admission"
    )


try:
    asyncio.run(run())
    scenario.refresh_from_db()
    assert scenario.title == "MCP changed" and scenario.version == 2
finally:
    alice.delete()
    bob.delete()
