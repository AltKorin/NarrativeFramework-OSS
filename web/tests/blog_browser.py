"""Editorial routes, language counterparts, no-JS content and responsive navigation."""

import json
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
    page.get_by_role("button", name="EN", exact=True).click()
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
