"""Landing and public registration through the actual Docker proxy.

Run from the repo root. Creates one disposable account and removes it in finally.
"""

import os
import secrets
import subprocess
import uuid
from playwright.sync_api import sync_playwright
from browser_target import browser_args

origin = os.environ.get("SITE_TEST_BASE", "http://localhost:8080").rstrip("/")
remote = os.environ.get("SITE_TEST_SSH")
cleanup_command = (
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
username = "site-test-" + uuid.uuid4().hex[:12]
password = secrets.token_urlsafe(24)
errors = []
try:
    with sync_playwright() as p:
        browser = p.chromium.launch(headless=True, args=browser_args())
        page = browser.new_page(
            locale="pl-PL", viewport={"width": 1440, "height": 1000}
        )
        page.on("pageerror", lambda error: errors.append(str(error)))
        for lang in ("pl", "en"):
            response = page.goto(f"{origin}/{lang}/", wait_until="networkidle")
            assert response and response.ok
            assert f'<html lang="{lang}"' in response.text()
            expected_heading = "Każda decyzja" if lang == "pl" else "Every choice"
            assert expected_heading in response.text()
            assert "<h1>" in response.text(), (
                "Landing must be readable without client JS"
            )
            assert page.locator("h1").count() == 1
            assert page.locator("html").get_attribute("lang") == lang
            assert (
                page.locator("a[href='https://www.buymeacoffee.com/StolemCentrumGier']")
                .inner_text()
                .startswith("Oil to my machine")
            )
            assert page.locator(".nf-hero-art img").evaluate(
                "el => el.complete && el.naturalWidth > 0"
            )
            assert page.locator(".nf-product img").evaluate(
                "el => el.complete && el.naturalWidth > 0"
            )
            assert page.evaluate("document.documentElement.scrollWidth <= innerWidth")
            assert page.evaluate(
                "async path => (await fetch(path)).ok",
                f"/docs/{lang}/individual-play.md",
            )
            page.screenshot(
                path=f"/tmp/narrative-site-{lang}-desktop.png", full_page=True
            )
        page.get_by_role("button", name="Absent character", exact=True).click()
        assert (
            "While the party was away"
            in page.locator(".nf-example-content").inner_text()
        )
        page.get_by_role("button", name="Solo", exact=True).click()
        assert "One character" in page.locator(".nf-example-content").inner_text()
        page.get_by_role("button", name="PL", exact=True).click()
        assert page.url.endswith("/pl/")
        assert "Każda decyzja" in page.locator("h1").inner_text()
        page.set_viewport_size({"width": 390, "height": 844})
        page.goto(f"{origin}/pl/", wait_until="networkidle")
        page.screenshot(path="/tmp/narrative-site-mobile-hero.png")
        page.get_by_role("button", name="Nawigacja", exact=True).click()
        page.get_by_role("link", name="Historia", exact=True).click()
        assert page.locator(".nf-menu").get_attribute("aria-expanded") == "false"
        assert page.evaluate("document.documentElement.scrollWidth <= innerWidth")
        page.screenshot(path="/tmp/narrative-site-pl-mobile.png", full_page=True)
        page.set_viewport_size({"width": 1440, "height": 1000})
        page.goto(f"{origin}/pl/", wait_until="networkidle")
        page.screenshot(path="/tmp/narrative-site-hero.png")
        page.get_by_role("link", name="Załóż konto", exact=False).click()
        page.get_by_label("Powtórz hasło", exact=True).wait_for()
        assert "/workshop" in page.url
        page.get_by_label("Login", exact=True).fill(username)
        page.get_by_label("Hasło", exact=True).fill(password)
        page.get_by_label("Powtórz hasło", exact=True).fill(password)
        with page.expect_response(
            lambda r: r.url.endswith("/api/register/") and r.request.method == "POST"
        ) as registration:
            page.locator(".account-panel").get_by_role(
                "button", name="Załóż konto", exact=True
            ).click()
        assert registration.value.status == 201
        if origin.startswith("https://"):
            cookies = {cookie["name"]: cookie for cookie in page.context.cookies()}
            assert cookies["sessionid"]["secure"] and cookies["sessionid"]["httpOnly"]
            assert cookies["csrftoken"]["secure"]
        page.locator("header").get_by_role("button", name=username).wait_for()
        page.get_by_role("heading", name="Twój warsztat RPG").wait_for()
        page.get_by_role("button", name="Otwórz demo przygody", exact=True).click()
        with page.expect_response(
            lambda r: r.url.endswith("/api/scenarios/") and r.request.method == "POST"
        ) as creation:
            page.get_by_role("button", name="Zapisz na koncie", exact=True).click()
        assert creation.value.status == 201, (
            creation.value.status,
            creation.value.text(),
        )
        page.get_by_role("button", name="Zapisz zmiany", exact=True).wait_for()
        page.reload(wait_until="networkidle")
        page.locator("header").get_by_role("button", name=username).wait_for()
        if not page.locator(".account-panel").is_visible():
            page.locator("header").get_by_role("button", name=username).click()
        page.get_by_role("button", name="Wyloguj", exact=True).click()
        page.locator("header").get_by_role(
            "button", name="Zaloguj się", exact=False
        ).wait_for()
        assert not errors, errors
        browser.close()
    print(
        "PASS: prerendered PL/EN, support link, images, examples, mobile navigation, public signup, private save, session persistence and logout"
    )
finally:
    subprocess.run(
        cleanup_command,
        input=f"from django.contrib.auth import get_user_model\nget_user_model().objects.filter(username={username!r}).delete()\n",
        text=True,
        capture_output=True,
        check=True,
    )
