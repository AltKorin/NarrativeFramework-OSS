"""Editorial routes, language counterparts, no-JS content and responsive navigation."""

import json
import struct
import os
from pathlib import Path
from playwright.sync_api import sync_playwright
from browser_target import browser_args

base = os.environ.get("SITE_TEST_BASE", "http://localhost:8080").rstrip("/")
posts = json.loads(Path("web/frontend/src/content/posts.json").read_text())
errors = []
with sync_playwright() as p:
    browser = p.chromium.launch(headless=True, args=browser_args())
    page = browser.new_page(locale="pl-PL", viewport={"width": 1440, "height": 1100})
    page.on("pageerror", lambda e: errors.append(str(e)))
    page.goto(base + "/pl/blog/", wait_until="networkidle")
    decline = page.get_by_role("button", name="Bez analityki", exact=True)
    if decline.count():
        decline.click()
    assert (
        page.get_by_role("heading", level=1).inner_text()
        == "Świat rośnie między sesjami."
    )
    assert page.locator(".nf-blog-feature img").evaluate(
        "el=>el.complete&&el.naturalWidth>0"
    )
    assert page.evaluate("document.documentElement.scrollWidth<=innerWidth")
    page.screenshot(path="/tmp/narrative-blog-index.png", full_page=True)
    for post in posts:
        for lang in ("pl", "en"):
            page.goto(
                base + f"/{lang}/blog/{post[lang]['slug']}/", wait_until="networkidle"
            )
            assert (
                page.get_by_role("heading", level=1).inner_text() == post[lang]["title"]
            )
            assert page.locator(".nf-post-body").inner_text()
            for illustration in page.locator(".nf-post-body img").all():
                illustration.scroll_into_view_if_needed()
                illustration.evaluate("el => el.decode()")
                assert illustration.evaluate("el=>el.naturalWidth>0 && el.naturalHeight>0")
                assert illustration.get_attribute("alt")
            target = page.locator(".nf-post-toc a").first
            target.click()
            assert page.evaluate(
                "document.getElementById(location.hash.slice(1)) !== null"
            )
            assert page.locator(".nf-post-body h2").count() >= 4
            other = "en" if lang == "pl" else "pl"
            assert (
                page.locator(".nf-languages")
                .get_by_role("link", name=other.upper(), exact=True)
                .get_attribute("href")
                == f"/{other}/blog/{post[other]['slug']}/"
            )
            page.set_viewport_size({"width": 390, "height": 844})
            assert page.evaluate("document.documentElement.scrollWidth<=innerWidth"), (
                post["id"],
                lang,
            )
            page.set_viewport_size({"width": 1440, "height": 1100})
    page.goto(
        base + "/pl/blog/" + posts[0]["pl"]["slug"] + "/", wait_until="networkidle"
    )
    page.screenshot(path="/tmp/narrative-blog-article.png", full_page=True)
    page.set_viewport_size({"width": 390, "height": 844})
    page.screenshot(path="/tmp/narrative-blog-mobile.png", full_page=True)
    for width in (390, 768, 1024, 1440):
        page.set_viewport_size({"width": width, "height": 1000})
        page.goto(base + "/pl/", wait_until="networkidle")
        assert page.evaluate("document.documentElement.scrollWidth<=innerWidth"), width
    for illustration in page.locator("#campaign-guides img").all():
        illustration.scroll_into_view_if_needed()
        illustration.evaluate("el => el.decode()")
        assert illustration.get_attribute("src").endswith(("-pl.png", "-pl.webp"))
        assert illustration.get_attribute("alt")
    page.locator("#campaign-guides").screenshot(path="/tmp/narrative-infographics-desktop.png")
    page.set_viewport_size({"width": 390, "height": 844})
    page.locator("#campaign-guides").screenshot(path="/tmp/narrative-infographics-mobile.png")
    page.get_by_role("button", name="EN", exact=True).click()
    for illustration in page.locator("#campaign-guides img").all():
        illustration.scroll_into_view_if_needed()
        illustration.evaluate("el => el.decode()")
        assert illustration.get_attribute("src").endswith(("-en.png", "-en.webp"))
    for i, link in enumerate(page.locator("#campaign-guides a[download]").all()):
        with page.expect_download() as download_info:
            link.click()
        download = download_info.value
        path = Path(f"/tmp/narrative-social-download-{i}.png")
        download.save_as(path)
        raw = path.read_bytes()
        assert raw[:8] == b"\x89PNG\r\n\x1a\n"
        assert struct.unpack(">II", raw[16:24]) == (1080, 1350)
    assert (
        page.locator("link[rel=canonical]").get_attribute("href")
        == "https://narrativeframework.com/en/"
    )
    static = browser.new_context(java_script_enabled=False).new_page()
    static.goto(
        base + "/en/blog/" + posts[0]["en"]["slug"] + "/", wait_until="networkidle"
    )
    assert (
        static.get_by_role("heading", level=1).inner_text() == posts[0]["en"]["title"]
    )
    assert (
        static.locator(".nf-post-body")
        .inner_text()
        .startswith("A useful world description")
    )
    assert not errors, errors
    browser.close()
print(
    "PASS: six readable PL/EN guides, counterpart links, headings/TOC, real image, no-JS article and responsive blog/landing"
)
