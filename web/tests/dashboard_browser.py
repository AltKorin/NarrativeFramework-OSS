"""Workshop entry, authentication intent, blank creation, discovery and safe resume."""
import json
import os
import secrets
import subprocess
import uuid
from urllib.parse import parse_qs, urlparse
from playwright.sync_api import sync_playwright, expect
from browser_target import browser_args

base = os.environ.get("SITE_TEST_BASE", "http://localhost:8080").rstrip("/")
remote = os.environ.get("SITE_TEST_SSH")
shell = (["ssh", "-o", "BatchMode=yes", remote, "cd /opt/narrativeframework/web && docker compose exec -T api python manage.py shell"] if remote else ["docker", "compose", "-f", "web/compose.yaml", "exec", "-T", "api", "python", "manage.py", "shell"])
users = {lang:"dashboard-"+lang+"-"+uuid.uuid4().hex[:10] for lang in ("pl","en")}
password = secrets.token_urlsafe(24)
subprocess.run(shell,input=f"from django.contrib.auth import get_user_model\nfor name in {list(users.values())!r}:\n get_user_model().objects.create_user(name,password={password!r})\n",text=True,capture_output=True,check=True)
try:
    with sync_playwright() as p:
        browser=p.chromium.launch(headless=True,args=browser_args())
        for lang in ("pl","en"):
            t=lambda a,b: a if lang=="pl" else b
            context=browser.new_context(locale="pl-PL" if lang=="pl" else "en-GB",viewport={"width":1440,"height":1050})
            page=context.new_page()
            errors=[]
            page.on("pageerror",lambda e:errors.append(str(e)))
            page.goto(base+f"/workshop/?lang={lang}",wait_until="networkidle")
            page.get_by_role("heading",name=t("Twój warsztat RPG","Your RPG workshop"),exact=True).wait_for()
            assert page.locator(".react-flow").count()==0
            page.get_by_role("button",name=t("Nowa kampania","New campaign"),exact=True).click()
            panel=page.locator(".account-panel")
            panel.get_by_role("button",name=t("Mam już konto","I already have an account"),exact=True).click()
            page.get_by_label(t("Login","Username"),exact=True).fill(users[lang])
            page.get_by_label(t("Hasło","Password"),exact=True).fill(password)
            panel.get_by_role("button",name=t("Zaloguj","Log in"),exact=True).click()
            campaign_name=page.get_by_label(t("Nazwa kampanii","Campaign name"),exact=True)
            expect(campaign_name).to_be_focused()
            assert page.get_by_role("button",name=t("Utwórz kampanię","Create campaign"),exact=True).is_disabled()
            campaign_name.fill("My campaign "+lang)
            other = "en" if lang == "pl" else "pl"
            page.get_by_role("button",name=other.upper(),exact=True).click()
            expect(page.get_by_label("Campaign name" if other=="en" else "Nazwa kampanii",exact=True)).to_have_value("My campaign "+lang)
            assert parse_qs(urlparse(page.url).query)["lang"]==[other]
            page.get_by_role("button",name=lang.upper(),exact=True).click()
            expect(campaign_name).to_have_value("My campaign "+lang)
            page.get_by_role("button",name=t("Utwórz kampanię","Create campaign"),exact=True).click()
            page.wait_for_url("**/campaigns/**")
            page.locator(".wiki-start").get_by_role("button",name=t("Dodaj postać","Add a character"),exact=True).wait_for()
            campaign_id=parse_qs(urlparse(page.url).query)["campaign"][0]
            # Follow the real first-map flow from the newly created campaign.
            for kind,label,title in (("npc",t("Dodaj postać","Add a character"),"Ranger"),("location",t("Dodaj miejsce","Add a place"),"Citadel")):
                page.locator(".wiki-start").get_by_role("button",name=label,exact=True).click()
                page.get_by_label(t("Tytuł artykułu","Article title"),exact=True).fill(title)
                assert page.get_by_label(t("Kategoria","Category"),exact=True).input_value()==kind
                page.get_by_role("button",name=t("Zapisz artykuł","Save article"),exact=True).click()
                page.locator(".wiki-article-title").get_by_text(title,exact=True).wait_for()
            page.get_by_label(t("Od wpisu","From entry"),exact=True).select_option(label="Ranger")
            page.get_by_label(t("Do wpisu","To entry"),exact=True).select_option(label="Citadel")
            page.get_by_label(t("Relacja","Relationship"),exact=True).fill("guards")
            page.get_by_role("button",name=t("Połącz i pokaż mapę","Connect and show map"),exact=True).click()
            page.locator(".wiki-map .react-flow__edge").wait_for()
            page.get_by_role("link",name=t("Warsztat przygód","Adventure workshop"),exact=True).click()
            page.locator(".wh-lists").get_by_role("link",name="My campaign "+lang,exact=True).wait_for()
            # Header creation stays discoverable even when campaigns already exist.
            page.locator("header").get_by_role("link",name=t("Nowa kampania","New campaign"),exact=True).click()
            expect(page.get_by_label(t("Nazwa kampanii","Campaign name"),exact=True)).to_be_focused()
            page.get_by_role("button",name=t("Anuluj","Cancel"),exact=True).click()
            page.get_by_role("button",name=t("Nowa przygoda","New adventure"),exact=True).click()
            page.get_by_label(t("Nazwa przygody","Adventure name"),exact=True).fill("My adventure "+lang)
            page.get_by_role("button",name=t("Utwórz przygodę","Create adventure"),exact=True).click()
            page.get_by_label(t("Tytuł przygody","Adventure title"),exact=True).wait_for()
            expect(page.locator(".react-flow__node")).to_have_count(1)
            adventure_id=parse_qs(urlparse(page.url).query)["scenario"][0]
            page.locator(".inspector").get_by_label(t("Tytuł","Title"),exact=True).fill("UNSAVED_OPENING")
            page.locator("header").get_by_role("button",name=t("Pulpit","Dashboard"),exact=True).click()
            page.get_by_role("button",name=t("Kontynuuj edycję","Continue editing"),exact=True).click()
            assert page.locator(".inspector").get_by_label(t("Tytuł","Title"),exact=True).input_value()=="UNSAVED_OPENING"
            page.get_by_role("button",name=t("Zapisz zmiany","Save changes"),exact=True).click()
            page.get_by_text(t("Zapisano · wersja ","Saved · version ")+"2",exact=False).wait_for()
            page.reload(wait_until="networkidle")
            assert page.locator(".inspector").get_by_label(t("Tytuł","Title"),exact=True).input_value()=="UNSAVED_OPENING"
            page.locator("header").get_by_role("button",name=t("Pulpit","Dashboard"),exact=True).click()
            page.locator(".wh-lists").get_by_role("link",name="My adventure "+lang,exact=True).wait_for()
            page.route("**/api/scenarios/",lambda route: route.fulfill(status=503,content_type="application/json",body=json.dumps({"error":"Temporary test failure"})))
            page.get_by_role("button",name=t("Odśwież listy","Refresh lists"),exact=True).click()
            page.locator(".wh-lists [role=alert]").wait_for()
            assert page.locator(".wh-lists").get_by_role("link",name="My campaign "+lang,exact=True).count()==1
            page.unroute("**/api/scenarios/")
            page.get_by_role("button",name=t("Odśwież listy","Refresh lists"),exact=True).click()
            page.locator(".wh-lists").get_by_role("link",name="My adventure "+lang,exact=True).wait_for()
            for width in (390,768,1024,1440):
                page.set_viewport_size({"width":width,"height":1050})
                assert page.evaluate("document.documentElement.scrollWidth<=innerWidth"), width
            page.screenshot(path=f"/tmp/narrative-dashboard-{lang}-desktop.png",full_page=True)
            page.set_viewport_size({"width":390,"height":844})
            assert page.evaluate("document.documentElement.scrollWidth<=innerWidth")
            page.screenshot(path=f"/tmp/narrative-dashboard-{lang}-mobile.png",full_page=True)
            page.get_by_role("button",name=t("Otwórz rozgrywki indywidualne","Open individual play"),exact=True).click()
            page.locator(".plays").wait_for()
            page.locator("header").get_by_role("button",name=t("Pulpit","Dashboard"),exact=True).click()
            page.get_by_role("button",name=t("Otwórz demo przygody","Open adventure demo"),exact=True).click()
            expect(page.locator(".react-flow__node")).to_have_count(6)
            assert page.get_by_label(t("Tytuł przygody","Adventure title"),exact=True).input_value()!="My adventure "+lang
            page.locator("header").get_by_role("button",name=t("Pulpit","Dashboard"),exact=True).click()
            page.locator(".wh-lists").get_by_role("link",name="My adventure "+lang,exact=True).click()
            page.get_by_label(t("Tytuł przygody","Adventure title"),exact=True).wait_for()
            page.route("**/api/demo/**", lambda route: route.fulfill(status=503,content_type="application/json",body=json.dumps({"error":"Temporary demo failure"})))
            page.locator("header").get_by_role("button",name=users[lang],exact=False).click()
            page.get_by_role("button",name=t("Wyloguj","Log out"),exact=True).click()
            page.locator(".loading").wait_for()
            assert "My adventure "+lang not in page.locator("body").inner_text()
            assert "UNSAVED_OPENING" not in page.locator("body").inner_text()
            page.get_by_role("link",name=t("Wróć do pulpitu","Back to dashboard"),exact=True).wait_for()
            page.unroute("**/api/demo/**")
            page.get_by_role("link",name=t("Wróć do pulpitu","Back to dashboard"),exact=True).click()
            page.get_by_role("heading",name=t("Twój warsztat RPG","Your RPG workshop"),exact=True).wait_for()
            assert page.locator(".wh-lists").count()==0
            assert "My campaign "+lang not in page.locator("main").inner_text()
            assert "My adventure "+lang not in page.locator("main").inner_text()
            assert not errors,errors
            context.close()
        browser.close()
    print("PASS: PL/EN workshop dashboard, auth intent, new private campaign to first graph, discoverable creation, blank adventure, saved lists, in-memory draft resume, reload, partial error/retry, demo separation, mobile and logout isolation")
finally:
    subprocess.run(shell,input=f"from django.contrib.auth import get_user_model\nget_user_model().objects.filter(username__in={list(users.values())!r}).delete()\n",text=True,capture_output=True,check=True)
