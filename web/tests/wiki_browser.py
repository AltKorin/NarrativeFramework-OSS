"""Campaign wiki UI, private ownership and adventure snapshots through a real proxy."""

import json
import os
import re
import secrets
import subprocess
import uuid
from urllib.parse import urlparse, parse_qs
from playwright.sync_api import sync_playwright
from browser_target import browser_args

origin = os.environ.get("SITE_TEST_BASE", "http://localhost:8080").rstrip("/")
remote = os.environ.get("SITE_TEST_SSH")
shell = (
    [
        "ssh",
        "-o",
        "BatchMode=yes",
        remote,
        "cd /opt/narrativeframework/web && docker compose exec -T api python manage.py shell",
    ]
    if remote
    else [
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
)
suffix = uuid.uuid4().hex[:10]
owner = "wiki-browser-" + suffix
other = "wiki-outsider-" + suffix
password = secrets.token_urlsafe(24)
scenario_id = str(uuid.uuid4())
setup = f"from django.contrib.auth import get_user_model\nfrom stories.models import Scenario\nfrom stories.demo import DOCUMENT_EN\na=get_user_model().objects.create_user({owner!r},password={password!r})\nget_user_model().objects.create_user({other!r},password={password!r})\nScenario.objects.create(id={scenario_id!r},owner=a,title='Browser wiki adventure',document=DOCUMENT_EN)\n"
subprocess.run(shell, input=setup, text=True, capture_output=True, check=True)
errors = []


def request(page, url, method="GET", data=None, headers=None):
    return page.evaluate(
        """async ({url, method, data, headers}) => {
      const response = await fetch(url, {method, headers: {"Content-Type":"application/json", ...headers}, ...(data===null ? {} : {body:JSON.stringify(data)})});
      return {status:response.status, body:await response.json()};
    }""",
        {"url": url, "method": method, "data": data, "headers": headers or {}},
    )


def login(page, username):
    page.goto(origin + "/workshop/?account=login&lang=en", wait_until="networkidle")
    page.get_by_label("Username", exact=True).fill(username)
    page.get_by_label("Password", exact=True).fill(password)
    page.locator(".account-panel").get_by_role(
        "button", name="Log in", exact=True
    ).click()
    page.locator("header").get_by_role("button", name=username).wait_for()


def new_page(page, title, kind, body, notes):
    page.get_by_role("button", name="New article", exact=True).click()
    page.get_by_label("Article title", exact=True).fill(title)
    page.get_by_label("Category", exact=True).select_option(kind)
    page.get_by_label("Content (Markdown)", exact=True).fill(body)
    page.get_by_label("Private GM notes", exact=True).fill(notes)
    page.get_by_role("button", name="Save article", exact=True).click()
    page.locator(".wiki-article-title").get_by_text(title, exact=True).wait_for()
    page.locator(".wiki-layout:not([inert])").wait_for()
    return parse_qs(urlparse(page.url).query)["page"][0]


try:
    with sync_playwright() as p:
        browser = p.chromium.launch(headless=True, args=browser_args())
        page = browser.new_page(
            locale="en-GB", viewport={"width": 1440, "height": 1100}
        )
        page.on("pageerror", lambda e: errors.append(str(e)))
        page.goto(origin + "/campaigns/?lang=en", wait_until="networkidle")
        page.get_by_role("link", name="Log in or create an account").wait_for()
        login(page, owner)
        page.get_by_role("link", name="Campaign wiki", exact=True).click()
        page.get_by_label("New campaign name", exact=True).fill("The northern coast")
        page.get_by_role("button", name="Create campaign", exact=True).click()
        page.get_by_role("button", name="New article", exact=True).wait_for()
        page.locator(".wiki-layout:not([inert])").wait_for()
        campaign_id = parse_qs(urlparse(page.url).query)["campaign"][0]
        port = new_page(
            page,
            "Porttown",
            "location",
            '## Harbor\n\nA **shelter** for travelers.\n\n<script>window.__wikiXss=true</script>\n<img src=x onerror="window.__wikiXss=true">',
            "PRIVATE_PORT_NOTE",
        )
        assert page.locator(".wiki-markdown strong").inner_text() == "shelter"
        assert page.locator(".wiki-markdown script, .wiki-markdown img").count() == 0
        assert not page.evaluate("window.__wikiXss===true")
        mira = new_page(
            page,
            "Mira",
            "npc",
            "## Cartographer\n\nSells old maps.",
            "PRIVATE_WIKI_BROWSER_SENTINEL",
        )
        page.get_by_label("Link to article", exact=True).select_option(port)
        page.get_by_label("Relationship label", exact=True).fill("lives in")
        page.get_by_role("button", name="Add relationship", exact=True).click()
        page.locator(".wiki-relations").get_by_role(
            "button", name=re.compile("Porttown")
        ).click()
        page.locator(".wiki-article-title").get_by_text(
            "Porttown", exact=True
        ).wait_for()
        assert "Mira" in page.locator(".wiki-relations").inner_text()
        page.get_by_role("button", name="Relationship map", exact=True).click()
        page.locator(".wiki-map .react-flow__node").first.wait_for()
        assert page.locator(".wiki-map .react-flow__node").count() == 2
        assert page.locator(".wiki-map .react-flow__edge").count() == 1
        page.locator(".wiki-map .react-flow__node").filter(has_text="Mira").click()
        page.locator(".wiki-article-title").get_by_text("Mira", exact=True).wait_for()
        page.locator(".wiki-layout:not([inert])").wait_for()
        page.get_by_label("Search titles and content", exact=True).fill("shelter")
        page.locator(".wiki-index").get_by_role(
            "button", name="Porttown", exact=True
        ).wait_for()
        page.wait_for_timeout(350)
        assert (
            page.locator(".wiki-index")
            .get_by_role("button", name="Mira", exact=True)
            .count()
            == 0
        )
        page.get_by_label("Search titles and content", exact=True).fill("")
        page.locator(".wiki-layout:not([inert])").wait_for()
        page.get_by_role("button", name="Edit", exact=True).click()
        page.get_by_label("Content (Markdown)", exact=True).fill("LOCAL_DRAFT_SENTINEL")
        page.get_by_role("button", name="PL", exact=True).click()
        assert (
            page.get_by_label("Treść (Markdown)", exact=True).input_value()
            == "LOCAL_DRAFT_SENTINEL"
        )
        page.get_by_role("button", name="EN", exact=True).click()
        api = f"{origin}/api/campaigns/{campaign_id}/pages/{mira}/"
        current = request(page, api)["body"]
        csrf = request(page, origin + "/api/session/")["body"]["csrf"]
        updated = request(
            page,
            api,
            "PUT",
            data={**current, "body": "SERVER_EDIT"},
            headers={"X-CSRFToken": csrf},
        )
        assert updated["status"] == 200
        with page.expect_response(
            lambda r: r.url == api and r.request.method == "PUT"
        ) as conflict:
            page.get_by_role("button", name="Save article", exact=True).click()
        assert conflict.value.status == 409
        assert (
            page.get_by_label("Content (Markdown)", exact=True).input_value()
            == "LOCAL_DRAFT_SENTINEL"
        )
        page.once("dialog", lambda dialog: dialog.accept())
        page.get_by_role("button", name="Reload", exact=True).click()
        page.locator(".wiki-markdown").get_by_text("SERVER_EDIT", exact=True).wait_for()
        page.locator(".wiki-layout:not([inert])").wait_for()
        page.get_by_label("Target adventure", exact=True).select_option(scenario_id)
        page.get_by_role("button", name="Copy to adventure", exact=True).click()
        page.get_by_role("link", name=re.compile("Open adventure")).wait_for()
        with page.expect_download() as download:
            page.get_by_role("button", name="Export campaign (GM)", exact=True).click()
        data = json.loads(open(download.value.path()).read())
        assert (
            data["schema"] == "narrative-wiki/2"
            and len(data["pages"]) == 2
            and len(data["relations"]) == 1
        )
        assert any(
            p["gm_notes"] == "PRIVATE_WIKI_BROWSER_SENTINEL" for p in data["pages"]
        )
        page.screenshot(path="/tmp/narrative-wiki-desktop.png", full_page=True)
        page.get_by_role("link", name=re.compile("Open adventure")).click()
        page.get_by_label("Adventure title", exact=True).wait_for()
        assert (
            page.get_by_label("Adventure title", exact=True).input_value()
            == "Browser wiki adventure"
        )
        page.locator(".outline").get_by_role("button", name="Mira", exact=True).click()
        assert (
            page.locator(".inspector")
            .get_by_label("Game master secret", exact=True)
            .input_value()
            == "PRIVATE_WIKI_BROWSER_SENTINEL"
        )
        scenario = request(page, f"{origin}/api/scenarios/{scenario_id}/")["body"]
        copied = [
            n
            for n in scenario["document"]["nodes"]
            if n.get("wiki_origin", {}).get("page_id") == mira
        ]
        assert len(copied) == 1 and copied[0]["text"] == "SERVER_EDIT"
        page.goto(
            f"{origin}/campaigns/?campaign={campaign_id}&page={mira}&lang=en",
            wait_until="networkidle",
        )
        page.locator(".wiki-article-title").get_by_text("Mira", exact=True).wait_for()
        page.set_viewport_size({"width": 390, "height": 844})
        assert page.evaluate("document.documentElement.scrollWidth<=innerWidth")
        page.get_by_role("button", name="PL", exact=True).click()
        page.screenshot(path="/tmp/narrative-wiki-pl-mobile.png", full_page=True)
        stranger = browser.new_page(locale="en-GB")
        login(stranger, other)
        assert request(stranger, api)["status"] == 404
        assert (
            request(stranger, f"{origin}/api/campaigns/{campaign_id}/export/")["status"]
            == 404
        )
        stranger.goto(
            f"{origin}/campaigns/?campaign={campaign_id}&page={mira}&lang=en",
            wait_until="networkidle",
        )
        assert (
            "PRIVATE_WIKI_BROWSER_SENTINEL" not in stranger.locator("body").inner_text()
        )
        assert not errors, errors
        browser.close()
    print(
        "PASS: campaign/wiki editing, safe Markdown, search, links/backlinks/graph, PL/EN drafts, stale-write protection, export, adventure snapshot, foreign-owner isolation and mobile layout"
    )
except Exception:
    if "page" in globals():
        try:
            page.screenshot(path="/tmp/narrative-wiki-failure.png", full_page=True)
            print("Wiki notice:", page.locator(".wiki-notice").all_text_contents())
        except Exception:
            pass
    raise
finally:
    subprocess.run(
        shell,
        input=f"from django.contrib.auth import get_user_model\nget_user_model().objects.filter(username__in={[owner, other]!r}).delete()\n",
        text=True,
        capture_output=True,
        check=True,
    )
