"""Run against the isolated test stack; simulate hostile imports and lost responses."""

import json, os, secrets, subprocess, uuid
from playwright.sync_api import sync_playwright

url = os.environ.get("ADVERSARIAL_BROWSER_URL", "http://localhost:18080")
project = os.environ.get("ADVERSARIAL_PROJECT", "narrative-adversarial")
username = "browser-attack-" + uuid.uuid4().hex[:12]
password = secrets.token_urlsafe(30)
shell = [
    "docker",
    "compose",
    "-p",
    project,
    "-f",
    "web/compose.yaml",
    "exec",
    "-T",
    "api",
    "python",
    "manage.py",
    "shell",
]
subprocess.run(
    shell,
    input=f"from django.contrib.auth import get_user_model\nget_user_model().objects.create_user({username!r},password={password!r})\n",
    text=True,
    capture_output=True,
    check=True,
)
failures = []


def login(page):
    page.locator("header").get_by_role("button", name="Log in", exact=False).click()
    page.get_by_label("Username", exact=True).fill(username)
    page.get_by_label("Password", exact=True).fill(password)
    page.locator(".account-panel").get_by_role(
        "button", name="Log in", exact=True
    ).click()
    page.locator("header").get_by_role("button", name=username).wait_for()


def hostile_import(page):
    fixture = page.request.get(url + "/api/demo/?lang=en").json()
    fixture["title"] = "Malformed import"
    fixture["document"]["nodes"][3]["kind"] = ["npc"]
    dialogs = []

    def dialog(d):
        dialogs.append(d.message)
        d.accept()

    page.on("dialog", dialog)
    page.locator("input[type=file]").set_input_files(
        {
            "name": "attack.json",
            "mimeType": "application/json",
            "buffer": json.dumps(fixture).encode(),
        }
    )
    page.wait_for_timeout(350)
    assert not dialogs, "Malformed node kind reached import confirmation"
    assert (
        page.get_by_label("Adventure title", exact=True).input_value()
        != "Malformed import"
    )
    assert page.locator(".notice").is_visible()


def lost_creation_ack(page, edit_after=False, reload_after=False):
    login(page)
    before = len(page.request.get(url + "/api/scenarios/").json())
    dropped = []

    def lose_ack(route):
        if route.request.method == "POST" and not dropped:
            response = route.fetch()
            assert response.status == 201
            dropped.append(response.json()["id"])
            route.abort("failed")
        else:
            route.continue_()

    page.route("**/api/scenarios/", lose_ack)
    page.get_by_role("button", name="Save to account", exact=True).click()
    page.locator(".workbench:not([inert])").wait_for()
    assert len(dropped) == 1
    page.unroute("**/api/scenarios/", lose_ack)
    if reload_after:
        page.reload(wait_until="networkidle")
        page.get_by_label("Adventure title", exact=True).wait_for()
    if edit_after:
        page.get_by_label("Adventure title", exact=True).fill(
            "Edited after lost acknowledgement"
        )
    page.get_by_role("button", name="Save to account", exact=True).click()
    page.locator(".workbench:not([inert])").wait_for()
    records = page.request.get(url + "/api/scenarios/").json()
    assert len(records) == before + 1, "Retry created a duplicate private adventure"
    if edit_after:
        assert (
            page.get_by_label("Adventure title", exact=True).input_value()
            == "Edited after lost acknowledgement"
        ), "Conflict discarded local edits"
        assert page.get_by_role(
            "button", name="Save to account", exact=True
        ).is_visible()
    else:
        assert page.get_by_role("button", name="Save changes", exact=True).is_visible()


def logout_keeps_local_draft(page):
    login(page)
    page.get_by_label("Adventure title", exact=True).fill("Local draft survives logout")
    page.get_by_role("button", name="Log out", exact=True).click()
    page.locator("header").get_by_role("button", name="Log in", exact=False).wait_for()
    assert (
        page.get_by_label("Adventure title", exact=True).input_value()
        == "Local draft survives logout"
    ), "Logout replaced the local draft"
    page.wait_for_timeout(350)
    page.reload(wait_until="networkidle")
    assert (
        page.get_by_label("Adventure title", exact=True).input_value()
        == "Local draft survives logout"
    )


def html_is_text(page):
    fixture = page.request.get(url + "/api/demo/?lang=en").json()
    fixture["document"]["nodes"][0]["title"] = '<img src=x onerror="alert(1)">'
    fixture["document"]["nodes"][0]["text"] = "<script>alert(2)</script>"
    alerts = []

    def dialog(d):
        if d.type == "confirm":
            d.accept()
        else:
            alerts.append(d.message)
            d.dismiss()

    page.on("dialog", dialog)
    page.locator("input[type=file]").set_input_files(
        {
            "name": "xss.json",
            "mimeType": "application/json",
            "buffer": json.dumps(fixture).encode(),
        }
    )
    page.wait_for_timeout(350)
    assert page.locator(".workbench script,.workbench img").count() == 0
    assert not alerts
    assert (
        page.locator(".inspector").get_by_label("Title", exact=True).input_value()
        == fixture["document"]["nodes"][0]["title"]
    )


try:
    with sync_playwright() as p:
        browser = p.chromium.launch(headless=True)
        for name, check in [
            ("hostile import", hostile_import),
            ("lost create acknowledgement", lost_creation_ack),
            (
                "changed retry after lost acknowledgement",
                lambda page: lost_creation_ack(page, True),
            ),
            (
                "lost acknowledgement across reload",
                lambda page: lost_creation_ack(page, reload_after=True),
            ),
            ("logout retains local draft", logout_keeps_local_draft),
            ("HTML remains text", html_is_text),
        ]:
            page = browser.new_page(
                locale="en-GB", viewport={"width": 1440, "height": 1000}
            )
            errors = []
            page.on("pageerror", lambda e: errors.append(str(e)))
            try:
                page.goto(url + "/workshop", wait_until="networkidle")
                page.get_by_label("Adventure title", exact=True).wait_for()
                check(page)
                assert not errors, errors
                print("PASS:", name)
            except Exception as error:
                failures.append(name)
                print("FAIL:", name, type(error).__name__, str(error).splitlines()[0])
            finally:
                page.close()
        browser.close()
finally:
    subprocess.run(
        shell,
        input=f"from django.contrib.auth import get_user_model\nget_user_model().objects.filter(username={username!r}).delete()\n",
        text=True,
        capture_output=True,
        check=True,
    )
if failures:
    raise SystemExit(f"{len(failures)} adversarial browser checks failed")
