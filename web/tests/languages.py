"""Browser checks for locale detection, switching and separate demo drafts."""

from pathlib import Path
from playwright.sync_api import sync_playwright

with sync_playwright() as p:
    browser = p.chromium.launch(headless=True)
    page = browser.new_page(locale="en-GB", viewport={"width": 1440, "height": 1050})
    errors = []
    page.on("pageerror", lambda e: errors.append(str(e)))
    page.goto("http://localhost:8080/workshop", wait_until="networkidle")
    title = page.get_by_label("Adventure title", exact=True)
    title.wait_for()
    assert title.input_value() == "The Bell Beneath the Water"
    assert page.locator("html").get_attribute("lang") == "en"
    assert page.locator(".outline .section-label").inner_text().startswith("CONTENTS")
    assert page.locator(".inspector .section-label").inner_text().startswith("DETAILS")
    title.fill("My English draft")
    # Switch immediately, before the debounced local draft write.
    page.get_by_role("button", name="PL", exact=True).click()
    pl_title = page.get_by_label("Tytuł przygody", exact=True)
    pl_title.wait_for()
    assert pl_title.input_value() == "Dzwon pod wodą"
    pl_title.fill("Mój polski szkic")
    page.get_by_role("button", name="EN", exact=True).click()
    title.wait_for()
    assert title.input_value() == "My English draft"
    page.reload(wait_until="networkidle")
    title = page.get_by_label("Adventure title", exact=True)
    title.wait_for()
    assert title.input_value() == "My English draft"
    page.get_by_role("button", name="Check structure").click()
    assert "sources" in page.locator(".issues").inner_text()
    with page.expect_download() as download:
        page.get_by_role("button", name="Export Markdown").click()
    markdown = Path(download.value.path()).read_text()
    assert "GM material" in markdown and "GM secret" in markdown
    assert "The Bell Beneath the Water" in markdown
    page.screenshot(path="/tmp/narrative-framework-en.png", full_page=True)
    page.get_by_role("button", name="PL", exact=True).click()
    pl_title = page.get_by_label("Tytuł przygody", exact=True)
    pl_title.wait_for()
    assert pl_title.input_value() == "Mój polski szkic"
    page.get_by_role("button", name="Sprawdź strukturę").click()
    assert "Wskazówka" in page.locator(".issues").inner_text()
    page.set_viewport_size({"width": 390, "height": 844})
    assert page.evaluate("document.documentElement.scrollWidth <= window.innerWidth")
    page.screenshot(path="/tmp/narrative-framework-pl-mobile.png", full_page=True)
    # A fresh Polish browser starts in Polish, independently of the first context.
    fresh = browser.new_page(locale="pl-PL")
    fresh.goto("http://localhost:8080/workshop", wait_until="networkidle")
    fresh.get_by_label("Tytuł przygody", exact=True).wait_for()
    assert fresh.locator("html").get_attribute("lang") == "pl"
    assert not errors, errors
    browser.close()
print(
    "PASS: EN/PL detection, labels, instant switch with draft preservation, reload persistence, localized validation/Markdown and mobile layout"
)
