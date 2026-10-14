"""Run from repo root against local Compose. No model/provider credentials needed."""

import asyncio
from concurrent.futures import ThreadPoolExecutor
import json
import os
import secrets
import subprocess
import uuid
from pathlib import Path

from mcp import ClientSession
from mcp.client.streamable_http import streamablehttp_client
from playwright.sync_api import sync_playwright
from browser_target import browser_args

origin = os.environ.get("SITE_TEST_BASE", "http://localhost:8080").rstrip("/")
remote = os.environ.get("SITE_TEST_SSH")
suffix = uuid.uuid4().hex[:12]
gm = "browser-gm-" + suffix
player = "browser-player-" + suffix
password = secrets.token_urlsafe(30)
private_note = "PRIVATE_BROWSER_GM_NOTE"
base = [
    "docker",
    "compose",
    "-f",
    "web/compose.yaml",
    "exec",
    "-T",
    "api",
    "python",
    "manage.py",
    "shell",
]
if remote:
    base = [
        "ssh",
        "-o",
        "BatchMode=yes",
        remote,
        "cd /opt/narrativeframework/web && docker compose exec -T api python manage.py shell",
    ]
setup = f"from django.contrib.auth import get_user_model\nfor name in {repr([gm, player])}:\n get_user_model().objects.create_user(name,password={password!r})\n"
subprocess.run(base, input=setup, text=True, capture_output=True, check=True)


def login(page, username):
    page.goto(origin + "/workshop", wait_until="networkidle")
    page.locator("header").get_by_role("button", name="Log in", exact=False).click()
    page.get_by_label("Username", exact=True).fill(username)
    page.get_by_label("Password", exact=True).fill(password)
    page.locator(".account-panel").get_by_role(
        "button", name="Log in", exact=True
    ).click()
    page.locator("header").get_by_role("button", name=username).wait_for()


async def play_with_agent(token):
    async with streamablehttp_client(
        origin + "/play/mcp", headers={"Authorization": "Bearer " + token}
    ) as (read, write, _):
        async with ClientSession(read, write) as client:
            await client.initialize()
            result = await client.call_tool("get_play_context", {})
            assert not result.isError, result
            context = json.loads(result.content[0].text)
            assert private_note not in str(context)
            assert {n["id"] for n in context["materials"]["nodes"]} == {
                "harbor",
                "mira",
            }
            body = {
                "operation_id": str(uuid.uuid4()),
                "expected_version": context["version"],
                "player_action": "I help Mira search the pier.",
                "narration": "Mira offers to remember your help.",
                "memory_markdown": "# Current state\nArin helped Mira.\n\n# Pending\nMira as a contact needs human GM approval.",
                "current_scene": "harbor",
                "consequences": [
                    {
                        "category": "relationship",
                        "description": "Mira becomes a contact.",
                    }
                ],
            }
            result = await client.call_tool("commit_play_turn", body)
            assert not result.isError, result
            receipt = json.loads(result.content[0].text)
            assert receipt["consequences"][0]["status"] == "pending"
            duplicate = await client.call_tool("commit_play_turn", body)
            assert json.loads(duplicate.content[0].text)["replayed"]
            result = await client.call_tool(
                "finish_play_session",
                {
                    "operation_id": str(uuid.uuid4()),
                    "expected_version": receipt["version"],
                    "summary": "Arin returns to the party at dawn. The proposed contact awaits the GM.",
                },
            )
            assert not result.isError, result


try:
    with sync_playwright() as p:
        browser = p.chromium.launch(headless=True, args=browser_args())
        errors = []
        gm_page = browser.new_page(
            locale="en-GB", viewport={"width": 1440, "height": 1050}
        )
        gm_page.on("pageerror", lambda error: errors.append(str(error)))
        login(gm_page, gm)
        gm_page.get_by_role("button", name="Save to account", exact=True).click()
        gm_page.get_by_role("button", name="Save changes", exact=True).wait_for()
        gm_page.locator(".workbench:not([inert])").wait_for()
        gm_page.get_by_role("button", name="Assign this adventure", exact=True).click()
        form = gm_page.locator(".play-form")
        form.get_by_role("button", name="Use example briefing").click()
        form.get_by_label("Player username", exact=True).fill(player)
        form.get_by_label("Private GM notes", exact=True).fill(private_note)
        form.get_by_role(
            "checkbox", name="Mira, the Keeper’s Daughter", exact=True
        ).check()
        gm_page.screenshot(path="/tmp/narrative-play-assignment.png", full_page=True)
        with gm_page.expect_response(
            lambda r: r.url.endswith("/api/plays/") and r.request.method == "POST"
        ) as response:
            form.get_by_role("button", name="Create assignment", exact=True).click()
        created = response.value.json()
        assert response.value.status == 201, created
        play_id = created["id"]
        gm_page.locator(".play-detail").wait_for()
        assert (
            gm_page.locator(".play-detail")
            .get_by_text("Private GM notes", exact=True)
            .count()
            == 1
        )
        player_page = browser.new_page(
            locale="en-GB", viewport={"width": 1440, "height": 1050}
        )
        player_page.on("pageerror", lambda error: errors.append(str(error)))
        login(player_page, player)
        player_page.get_by_label("Choose an activity", exact=True).select_option(
            play_id
        )
        player_page.locator(".play-detail").wait_for()
        assert (
            player_page.locator(".play-detail")
            .get_by_text("Private GM notes", exact=True)
            .count()
            == 0
        )
        assert private_note not in player_page.locator(".plays").inner_text()
        player_page.locator(".play-access").get_by_text(
            "Connect your agent", exact=True
        ).click()
        player_page.get_by_role(
            "button", name="Generate / rotate play token", exact=True
        ).click()
        token_field = player_page.get_by_label(
            "Private play token — copy now", exact=False
        )
        token_field.wait_for()
        token = token_field.input_value()
        assert len(token) == 40
        assert token not in player_page.evaluate("JSON.stringify(localStorage)")
        with ThreadPoolExecutor(max_workers=1) as worker:
            worker.submit(lambda: asyncio.run(play_with_agent(token))).result(
                timeout=45
            )
        gm_page.locator(".play-selector").get_by_role(
            "button", name="Refresh", exact=True
        ).click()
        gm_page.get_by_label(
            "Decision note (visible to the player)", exact=True
        ).wait_for()
        gm_page.get_by_label("Decision note (visible to the player)", exact=True).fill(
            "Fits the campaign; Mira remembers Arin."
        )
        gm_page.get_by_role("button", name="Approve", exact=True).click()
        gm_page.get_by_text("Decision saved.", exact=True).wait_for()
        assert gm_page.locator(".effect-status").inner_text() == "Approved"
        with gm_page.expect_download() as download:
            gm_page.get_by_role("button", name="Download Markdown report").click()
        assert private_note in Path(download.value.path()).read_text()
        gm_page.screenshot(path="/tmp/narrative-play-gm-report.png", full_page=True)
        player_page.locator(".play-selector").get_by_role(
            "button", name="Refresh", exact=True
        ).click()
        player_page.get_by_text(
            "Fits the campaign; Mira remembers Arin.", exact=True
        ).wait_for()
        assert (
            player_page.get_by_role("button", name="Approve", exact=True).count() == 0
        )
        with player_page.expect_download() as download:
            player_page.get_by_role("button", name="Download Markdown report").click()
        report = Path(download.value.path()).read_text()
        assert private_note not in report and "Arin helped Mira" in report
        player_page.get_by_role("button", name="PL", exact=True).click()
        player_page.get_by_role(
            "button", name="Pobierz raport Markdown", exact=True
        ).wait_for()
        assert player_page.locator(".effect-status").inner_text() == "Zatwierdzona"
        player_page.set_viewport_size({"width": 390, "height": 844})
        assert player_page.evaluate(
            "document.documentElement.scrollWidth <= window.innerWidth"
        )
        # Hide credentials before producing a screenshot.
        player_page.locator(".play-access").get_by_text(
            "Podłącz swojego agenta", exact=True
        ).click()
        player_page.screenshot(
            path="/tmp/narrative-play-player-pl-mobile.png", full_page=True
        )
        assert not errors, errors
        browser.close()
    print(
        "PASS: GM assignment UI, player login/scoped token, real MCP play, GM approval, role-filtered downloads, PL/EN and mobile layout"
    )
finally:
    cleanup = f"from django.contrib.auth import get_user_model\nget_user_model().objects.filter(username__in={repr([gm, player])}).delete()\n"
    subprocess.run(base, input=cleanup, text=True, capture_output=True, check=True)
