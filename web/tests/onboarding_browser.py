"""A novice creates their own campaign, entries and map through the real UI/API."""
import os
import secrets
import subprocess
import uuid
from playwright.sync_api import sync_playwright, expect
from browser_target import browser_args

base = os.environ.get("SITE_TEST_BASE", "http://localhost:8080").rstrip("/")
remote = os.environ.get("SITE_TEST_SSH")
shell = (["ssh", "-o", "BatchMode=yes", remote, "cd /opt/narrativeframework/web && docker compose exec -T api python manage.py shell"] if remote else ["docker", "compose", "-f", "web/compose.yaml", "exec", "-T", "api", "python", "manage.py", "shell"])
username = "onboarding-" + uuid.uuid4().hex[:10]
password = secrets.token_urlsafe(24)
subprocess.run(shell, input=f"from django.contrib.auth import get_user_model\nget_user_model().objects.create_user({(username+'-en')!r},password={password!r})\nget_user_model().objects.create_user({(username+'-pl')!r},password={password!r})\n", text=True, capture_output=True, check=True)
try:
    with sync_playwright() as p:
        browser = p.chromium.launch(headless=True, args=browser_args())
        for lang in ("en", "pl"):
            pl = lang == "pl"
            t = lambda a,b: a if pl else b
            context = browser.new_context(viewport={"width": 1440, "height": 1100})
            page = context.new_page()
            errors = []
            page.on("pageerror", lambda e: errors.append(str(e)))
            page.goto(base + f"/campaigns/?lang={lang}", wait_until="networkidle")
            page.get_by_role("link", name=t("Zaloguj się lub załóż konto", "Log in or create an account")).click()
            page.get_by_label(t("Login", "Username"), exact=True).fill(username + "-" + lang)
            page.get_by_label(t("Hasło", "Password"), exact=True).fill(password)
            page.locator(".account-panel").get_by_role("button", name=t("Zaloguj", "Log in"), exact=True).click()
            page.wait_for_url("**/campaigns/**")
            page.get_by_role("button", name=t("Nazwij swoją kampanię", "Name your campaign")).click()
            field = page.get_by_label(t("Nazwa nowej kampanii", "New campaign name"))
            assert field.evaluate("el=>el===document.activeElement")
            field.fill("Shadows " + lang)
            page.get_by_role("button", name=t("Utwórz kampanię", "Create campaign"), exact=True).click()
            for kind, title, label in (("npc", "Mira", t("Dodaj postać", "Add a character")), ("location", "The inn", t("Dodaj miejsce", "Add a place"))):
                page.locator(".wiki-start").get_by_role("button", name=label, exact=True).click()
                titlefield = page.get_by_label(t("Tytuł artykułu", "Article title"), exact=True)
                expect(titlefield).to_be_focused()
                assert page.get_by_label(t("Kategoria", "Category"), exact=True).input_value() == kind
                assert page.get_by_role("button", name=t("Zapisz artykuł", "Save article"), exact=True).is_disabled()
                titlefield.fill(title)
                page.get_by_label(t("Treść (Markdown)", "Content (Markdown)"), exact=True).fill("One sentence is enough.")
                page.get_by_role("button", name=t("Zapisz artykuł", "Save article"), exact=True).click()
                page.locator(".wiki-article-title").get_by_text(title, exact=True).wait_for()
            page.reload(wait_until="networkidle")
            guide = page.locator(".wiki-start")
            guide.get_by_role("heading", name=t("Połącz dwa wpisy", "Connect two entries")).wait_for()
            source = page.get_by_label(t("Od wpisu", "From entry"), exact=True)
            source.select_option(label="Mira")
            page.get_by_label(t("Relacja", "Relationship"), exact=True).fill("works at")
            page.get_by_label(t("Do wpisu", "To entry"), exact=True).select_option(label="The inn")
            page.get_by_role("button", name=t("Połącz i pokaż mapę", "Connect and show map")).click()
            page.locator(".wiki-map .react-flow__edge").wait_for()
            assert page.locator(".wiki-map .react-flow__node").count() == 2
            assert "works at" in page.locator(".wiki-map").inner_text()
            assert source.input_value() != page.get_by_label(t("Do wpisu", "To entry"), exact=True).input_value()
            page.set_viewport_size({"width": 390, "height": 844})
            assert page.evaluate("document.documentElement.scrollWidth <= innerWidth")
            page.screenshot(path=f"/tmp/narrative-onboarding-{lang}-mobile.png", full_page=True)
            page.get_by_role("button", name=t("Ukryj wskazówki", "Hide tips")).click()
            page.get_by_role("button", name=t("Pokaż pierwsze kroki", "Show first steps")).click()
            page.locator(".wiki-map .react-flow__node").filter(has_text="Mira").click()
            page.locator(".wiki-article-title").get_by_text("Mira", exact=True).wait_for()
            assert not errors, errors
            context.close()
        browser.close()
    print("PASS: PL/EN account return, campaign name focus, blank title guard, typed entries, reload progress, custom directed connection, saved graph, mobile, hide/reopen and map-to-article")
finally:
    subprocess.run(shell, input=f"from django.contrib.auth import get_user_model\nget_user_model().objects.filter(username__in={([username+'-en',username+'-pl'])!r}).delete()\n", text=True, capture_output=True, check=True)
