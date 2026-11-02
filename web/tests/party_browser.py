"""Two players see each other on the party page, share their job in the party and see it in the builder."""
import os
import secrets
import subprocess
import uuid
from playwright.sync_api import sync_playwright, expect
from browser_target import browser_args

base = os.environ.get("SITE_TEST_BASE", "http://localhost:8080").rstrip("/")
shell = ["docker", "compose", "-f", "web/compose.yaml", "exec", "-T", "api", "python", "manage.py", "shell"]
tag = uuid.uuid4().hex[:8]
password = secrets.token_urlsafe(24)
campaign = str(uuid.uuid4())
wiki_page = str(uuid.uuid4())
setup = f"""
import uuid
from django.contrib.auth import get_user_model
from stories.models import Campaign, CampaignMember, CampaignRuleset, CharacterDraft, WikiPage
from stories.test_party import pack, events
users = get_user_model().objects
gm = users.create_user('party-gm-{tag}', password={password!r})
c = Campaign.objects.create(id={campaign!r}, owner=gm, title='Party {tag}', appearance='cyberpunk')
CampaignRuleset.objects.create(campaign=c, document=pack())
WikiPage.objects.create(id={wiki_page!r}, campaign=c, title='Port', kind='location', body='Mgła.')
for name, hero, stats, skills in (('anna', None, None, None), ('bart', 'Tor', {{'SIL': 7, 'WOL': 4}}, {{'walka': 4}})):
    member = CampaignMember.objects.create(campaign=c, user=users.create_user(name + '-{tag}', password={password!r}), role='player',
                                           display_name='Bart' if hero else '')
    if hero:  # created first, so the newer Tor is on top of the tile
        CharacterDraft.objects.create(id=uuid.uuid4(), campaign=c, member=member, ruleset=pack(), method='punkty',
                                      events=events(name='Porzucony'))
    CharacterDraft.objects.create(id=uuid.uuid4(), campaign=c, member=member, ruleset=pack(), method='punkty',
                                  events=events(name=hero, stats=stats, skills=skills) if hero else [])
"""
subprocess.run(shell, input=setup, text=True, capture_output=True, check=True)


def login(browser, who, viewport=None):
    context = browser.new_context(viewport=viewport or {"width": 1280, "height": 1000})
    page = context.new_page()
    errors = []
    page.on("pageerror", lambda e: errors.append(str(e)))
    page.goto(base + "/campaigns/?lang=pl", wait_until="networkidle")
    page.get_by_role("link", name="Zaloguj się lub załóż konto").click()
    page.get_by_label("Login", exact=True).fill(f"{who}-{tag}")
    page.get_by_label("Hasło", exact=True).fill(password)
    page.locator(".account-panel").get_by_role("button", name="Zaloguj", exact=True).click()
    page.wait_for_url("**/campaigns/**")
    page.goto(base + f"/campaigns/?lang=pl&campaign={campaign}", wait_until="networkidle")
    return page, errors


with sync_playwright() as p:
    browser = p.chromium.launch(headless=True, args=browser_args())
    anna, anna_errors = login(browser, "anna")
    anna.get_by_role("button", name="Ekipa", exact=True).click()
    hung = anna.locator(".party-tile").filter(has_text="Bart")
    assert f"bart-{tag}" not in anna.locator(".party").inner_text(), "players never see other logins"
    expect(hung.locator(".party-card").first).to_contain_text("Tor")
    expect(hung.locator(".party-card").first).to_contain_text("Wojownik")
    expect(hung.locator(".party-skills")).to_contain_text("Walka 11 · Wyszkolony")
    drafts = hung.locator("details.party-drafts")
    assert drafts.get_attribute("open") is None, "other drafts start folded"
    expect(drafts.locator("summary")).to_have_text("Szkice (1)")
    assert hung.locator("input").count() == 0, "only your own tile is editable"
    mine = anna.locator(".party-tile").filter(has_text="Gracz 1")
    mine.get_by_label("Jak widzą cię inni w drużynie").fill("Anka")
    mine.get_by_role("button", name="Zapisz nazwę").click()
    mine = anna.locator(".party-tile").filter(has_text="Anka")
    expect(mine.locator(".party-account")).to_contain_text("Anka")
    mine.get_by_label("W drużynie odpowiadam za…").fill("Otwieram drzwi")
    mine.get_by_role("button", name="Zapisz", exact=True).click()
    expect(mine.locator(".party-focus")).to_have_text("„Otwieram drzwi”")

    anna.get_by_role("button", name="Tworzenie postaci", exact=True).click()
    anna.locator(".builder-drafts button").first.click()
    anna.locator(".builder-steps button").nth(0).click()
    expect(anna.locator(".builder-note").filter(has_text="W ekipie")).to_have_text("W ekipie: Wojownik (Tor)")

    bart, bart_errors = login(browser, "bart", {"width": 390, "height": 844})
    bart.get_by_role("button", name="Ekipa", exact=True).click()
    expect(bart.locator(".party-tile").filter(has_text="Anka").locator(".party-focus")).to_have_text("„Otwieram drzwi”")
    assert bart.evaluate("document.documentElement.scrollWidth <= innerWidth"), "no horizontal scroll on a phone"
    bart.screenshot(path="/tmp/narrative-party-mobile.png", full_page=True)
    tabs = bart.locator(".campaign-tabs button")  # A4: every player tab and the help button stay on screen
    for i in range(tabs.count()):
        box = tabs.nth(i).bounding_box()
        assert box and box["x"] >= 0 and box["x"] + box["width"] <= 390, (tabs.nth(i).inner_text(), box)

    gm, gm_errors = login(browser, "party-gm", {"width": 390, "height": 844})
    gm.goto(base + f"/campaigns/?lang=pl&campaign={campaign}&page={wiki_page}", wait_until="networkidle")
    active = gm.locator(".campaign-tabs button[aria-current=page]")
    expect(active).to_have_text("Materiały świata")
    box = active.bounding_box()  # A4: the GM's active tab is scrolled into view, even the last one
    assert box and box["x"] >= 0 and box["x"] + box["width"] <= 390, box
    assert not anna_errors and not bart_errors and not gm_errors, anna_errors + bart_errors + gm_errors
    browser.close()
print("PASS: party tiles, chosen names (no logins), own job sentence, folded drafts, builder hint, phone tabs on screen")
