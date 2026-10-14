"""Consent and sanitized GA routing; intercepted Google script sends no real test hits."""

import json
import os
from playwright.sync_api import sync_playwright
from browser_target import browser_args

origin = os.environ.get("SITE_TEST_BASE", "http://localhost:8080").rstrip("/")
from urllib.parse import urlparse

host = urlparse(origin).hostname
config = {
    "landingMeasurementId": "G-0QV757EYZ7",
    "appMeasurementId": "G-HELGJT6EL4",
    "appManualOnlyConfirmed": False,
    "allowedHosts": [host],
}
key = "nf-analytics-consent-v1"


def instrument(page, settings):
    calls = []
    errors = []
    page.on("pageerror", lambda e: errors.append(str(e)))
    page.route(
        "**/analytics-config.json",
        lambda route: route.fulfill(
            content_type="application/json", body=json.dumps(settings)
        ),
    )

    def tag(route):
        calls.append(route.request.url)
        route.fulfill(
            content_type="application/javascript",
            body="window.__nfGoogleLoaded = true;",
        )

    page.route("https://www.googletagmanager.com/gtag/js?**", tag)
    page.route("https://**.google-analytics.com/**", lambda route: route.abort())
    return calls, errors


def commands(page):
    return page.evaluate("()=> JSON.parse(JSON.stringify((window.dataLayer||[]).map(x=>Array.from(x))))")


with sync_playwright() as p:
    browser = p.chromium.launch(headless=True, args=browser_args())
    page = browser.new_page(locale="en-GB", viewport={"width": 1440, "height": 1000})
    calls, errors = instrument(page, config)
    page.goto(
        origin + "/en/?campaign=PRIVATE_QUERY#invite=SECRET_TOKEN",
        wait_until="networkidle",
    )
    page.get_by_role("button", name="No analytics", exact=True).wait_for()
    assert not calls and not commands(page)
    page.get_by_role("button", name="No analytics", exact=True).click()
    page.reload(wait_until="networkidle")
    assert not calls and not page.locator("#nf-consent").is_visible()
    page.get_by_role("button", name="Analytics settings", exact=True).click()
    page.get_by_role("button", name="Allow analytics", exact=True).click()
    page.wait_for_function("() => window.__nfGoogleLoaded===true")
    assert len(calls) == 1 and "G-0QV757EYZ7" in calls[0]
    data = commands(page)
    options = next(c[2] for c in data if c[0] == "config")
    assert (
        options["send_page_view"] is False and options["allow_google_signals"] is False
    )
    assert (
        options["page_referrer"] == ""
        and "PRIVATE_QUERY" not in json.dumps(data)
        and "SECRET_TOKEN" not in json.dumps(data)
    )
    assert (
        data[0][0:2] == ["consent", "default"]
        and data[0][2]["analytics_storage"] == "denied"
    )
    assert (
        next(c[2] for c in data if c[0:2] == ["consent", "update"])["ad_storage"]
        == "denied"
    )
    page.get_by_role("link", name="GitHub", exact=False).first.wait_for()
    page.evaluate(
        "document.querySelector('[data-analytics=github_click]').addEventListener('click', e=>e.preventDefault())"
    )
    page.locator("[data-analytics=github_click]").first.dispatch_event("click")
    assert any(c[0:2] == ["event", "github_click"] for c in commands(page))
    page.context.add_cookies([{"name": "_ga", "value": "test-cookie", "url": origin}])
    page.get_by_role("button", name="Analytics settings", exact=True).click()
    page.get_by_role("button", name="No analytics", exact=True).click()
    page.wait_for_load_state("networkidle")
    assert len(calls) == 1 and not page.locator("#nf-google-tag").count()
    assert not any(x["name"].startswith("_ga") for x in page.context.cookies())
    assert not errors, errors

    # Application stream stays completely off until enhanced-measurement confirmation.
    blocked = browser.new_page(locale="en-GB")
    blocked_calls, _ = instrument(blocked, config)
    blocked.goto(
        origin + "/campaigns/?campaign=PRIVATE_CAMPAIGN#invite=PRIVATE_TOKEN",
        wait_until="networkidle",
    )
    assert not blocked_calls and not blocked.locator("#nf-consent").count()
    app = browser.new_page(locale="en-GB", viewport={"width": 390, "height": 844})
    app_calls, app_errors = instrument(app, {**config, "appManualOnlyConfirmed": True})
    app.goto(
        origin + "/campaigns/?campaign=PRIVATE_CAMPAIGN#invite=PRIVATE_TOKEN",
        wait_until="networkidle",
    )
    app.get_by_role("button", name="Allow analytics", exact=True).click()
    app.wait_for_function("() => window.__nfGoogleLoaded===true")
    assert len(app_calls) == 1 and "G-HELGJT6EL4" in app_calls[0]
    records = commands(app)
    payload = json.dumps(records)
    assert "PRIVATE_CAMPAIGN" not in payload and "PRIVATE_TOKEN" not in payload
    app_context = next(c[2] for c in records if c[0] == "config")
    assert app_context["page_location"] == origin + "/campaigns/"
    assert app_context["page_title"] == "Narrative Framework | Campaign wiki"
    assert app.evaluate("document.documentElement.scrollWidth<=innerWidth")
    app.screenshot(path="/tmp/narrative-analytics-mobile.png", full_page=True)
    assert not app_errors, app_errors
    browser.close()
print(
    "PASS: default no requests, decline persistence, consent-gated landing/app IDs, disabled app guard, sanitized URL/context, ad denial, withdrawal/cookie removal and mobile"
)
