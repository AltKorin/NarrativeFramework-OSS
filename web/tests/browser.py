import json, secrets, subprocess, uuid
from pathlib import Path
from playwright.sync_api import sync_playwright

username = "browser-review-" + uuid.uuid4().hex[:12]
password = secrets.token_urlsafe(30)
code = (
    "from django.contrib.auth import get_user_model\nget_user_model().objects.create_user("
    + repr(username)
    + ",password="
    + repr(password)
    + ")\n"
)
subprocess.run(
    [
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
    ],
    input=code,
    text=True,
    check=True,
    capture_output=True,
)
try:
    with sync_playwright() as p:
        browser = p.chromium.launch(headless=True)
        page = browser.new_page(
            locale="pl-PL", viewport={"width": 1440, "height": 1100}
        )
        errors = []
        page.on("pageerror", lambda error: errors.append(str(error)))
        page.goto("http://localhost:8080/workshop?view=editor", wait_until="networkidle")
        page.get_by_label("Tytuł przygody").wait_for()
        assert page.locator(".react-flow__node").count() == 6
        page.get_by_label("Tytuł przygody").fill("Testowa przygoda")
        page.wait_for_timeout(400)
        page.reload(wait_until="networkidle")
        assert page.get_by_label("Tytuł przygody").input_value() == "Testowa przygoda"
        first = page.locator(".react-flow__node").first
        before = first.bounding_box()
        assert before
        page.mouse.move(before["x"] + 40, before["y"] + 35)
        page.mouse.down()
        page.mouse.move(before["x"] + 80, before["y"] + 65, steps=8)
        page.mouse.up()
        after = first.bounding_box()
        assert after and after["x"] > before["x"] + 20
        page.get_by_role("button", name="Sprawdź strukturę").click()
        assert page.locator(".issues").is_visible()
        page.get_by_role("button", name="Zaloguj się", exact=False).first.click()
        page.get_by_label("Login", exact=True).fill(username)
        page.get_by_label("Hasło", exact=True).fill(password)
        page.get_by_role("button", name="Zaloguj", exact=True).click()
        page.get_by_role("button", name=username).wait_for()
        page.get_by_role("button", name="Zapisz na koncie").click()
        page.get_by_role("button", name="Zapisz zmiany").wait_for()
        page.locator(".workbench:not([inert])").wait_for()
        page.locator(".inspector").get_by_label("Tytuł", exact=True).fill(
            "Port po zmroku"
        )
        page.get_by_role("button", name="Zapisz zmiany").click()
        page.get_by_text("Zapisano · wersja 2").wait_for()
        page.locator(".workbench:not([inert])").wait_for()
        page.get_by_role("button", name="Sprawdź strukturę").click()
        page.locator(".issues").wait_for()
        with page.expect_download() as d:
            page.get_by_role("button", name="Eksport Markdown").click()
        assert "Port po zmroku" in Path(d.value.path()).read_text()
        page.get_by_role("button", name="EN", exact=True).click()
        page.get_by_label("Adventure title", exact=True).wait_for()
        assert (
            page.get_by_label("Adventure title", exact=True).input_value()
            == "Testowa przygoda"
        )
        assert (
            page.locator(".inspector").get_by_label("Title", exact=True).input_value()
            == "Port po zmroku"
        )
        page.get_by_role("button", name="Check structure").click()
        page.locator(".issues").wait_for()
        assert "sources" in page.locator(".issues").inner_text()
        page.get_by_role("button", name="PL", exact=True).click()
        page.get_by_label("Tytuł przygody", exact=True).wait_for()
        # Restore seed demo for the visual capture in a separate anonymous context.
        demo = browser.new_page(
            locale="pl-PL", viewport={"width": 1440, "height": 1100}
        )
        demo.goto("http://localhost:8080/workshop?view=editor", wait_until="networkidle")
        demo.get_by_label("Tytuł przygody").wait_for()
        demo.screenshot(path="/tmp/narrative-workshop-desktop.png", full_page=True)
        demo.set_viewport_size({"width": 390, "height": 844})
        demo.screenshot(path="/tmp/narrative-workshop-mobile.png", full_page=True)
        assert demo.evaluate(
            "document.documentElement.scrollWidth <= window.innerWidth"
        ), "Mobile horizontal overflow"
        assert not errors, errors
        browser.close()
    print(
        "PASS: browser demo editing/reload, login, private create/save, validation, Markdown download, desktop/mobile layout; no page errors"
    )
finally:
    subprocess.run(
        [
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
        ],
        input="from django.contrib.auth import get_user_model\nget_user_model().objects.filter(username="
        + repr(username)
        + ").delete()\n",
        text=True,
        check=True,
        capture_output=True,
    )
