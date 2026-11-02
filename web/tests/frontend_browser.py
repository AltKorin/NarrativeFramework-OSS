"""Player entry and gallery regressions. Fictional, disposable local/CI accounts."""
import base64
import os
import secrets
import subprocess
import uuid
from playwright.sync_api import sync_playwright, expect
from browser_target import browser_args

base = os.environ.get('SITE_TEST_BASE', 'http://localhost:8080').rstrip('/')
shell = ['docker', 'compose', '-f', 'web/compose.yaml', 'exec', '-T', 'api', 'python', 'manage.py', 'shell']
tag = uuid.uuid4().hex[:10]
password = secrets.token_urlsafe(24)
cid = str(uuid.uuid4())

def db(code):
    result = subprocess.run(shell, input=code, text=True, capture_output=True)
    assert result.returncode == 0, result.stderr


def login(browser, role, width=390):
    context = browser.new_context(viewport={'width': width, 'height': 844 if width == 390 else 1000})
    page = context.new_page()
    page.goto(base + '/campaigns/?lang=pl', wait_until='networkidle')
    page.get_by_role('link', name='Zaloguj się lub załóż konto').click()
    page.get_by_label('Login', exact=True).fill(f'frontend-{role}-{tag}')
    page.get_by_label('Hasło', exact=True).fill(password)
    page.locator('.account-panel').get_by_role('button', name='Zaloguj', exact=True).click()
    page.wait_for_url('**/campaigns/**')
    page.goto(base + f'/campaigns/?lang=pl&campaign={cid}', wait_until='networkidle')
    return page


def click(page, name):
    page.get_by_role('button', name=name, exact=True).click()
    page.wait_for_load_state('networkidle')


def settings(page):
    click(page, 'Materiały świata')
    if not page.get_by_label('Nazwa kampanii', exact=True).is_visible():
        page.locator('.wiki-settings summary').click()


def upload(page):
    click(page, 'Galeria')
    page.locator('input[type=file]').set_input_files({
        'name': 'sample.png', 'mimeType': 'image/png',
        'buffer': base64.b64decode('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jRZkAAAAASUVORK5CYII=')})
    page.get_by_label('Podpis', exact=True).fill('Latarnia')
    page.get_by_label('Pokaż graczom w galerii', exact=True).check()
    click(page, 'Dodaj obraz')
    expect(page.get_by_role('status')).to_have_text('Obraz dodany.')


db(f"""
from django.contrib.auth import get_user_model
from stories.models import Campaign, CampaignMember
users = get_user_model().objects
gm = users.create_user('frontend-gm-{tag}', password={password!r})
player = users.create_user('frontend-player-{tag}', password={password!r})
c = Campaign.objects.create(id={cid!r}, owner=gm, title='Latarnia', summary='Wasza wyprawa zaczyna się przy latarni.')
CampaignMember.objects.create(campaign=c, user=player, role='player')
""")
try:
    with sync_playwright() as p:
        browser = p.chromium.launch(headless=True, args=browser_args())
        gm = login(browser, 'gm')
        player = login(browser, 'player')
        click(player, 'Galeria')
        expect(player.get_by_text('Prowadzący nie udostępnił jeszcze obrazów.', exact=True)).to_be_visible()
        upload(gm)
        click(player, 'Odśwież galerię')
        expect(player.get_by_role('button', name='Powiększ: Latarnia', exact=True)).to_be_visible()
        click(gm, 'Ukryj przed graczami')
        click(player, 'Odśwież galerię')
        expect(player.get_by_role('button', name='Powiększ: Latarnia', exact=True)).to_have_count(0)
        expect(player.get_by_role('status')).to_have_text('Galeria odświeżona.')
        settings(gm)
        gm.locator('textarea[name=summary]').fill('Wyprawa rusza jutro.')
        click(gm, 'Zapisz ustawienia')
        expect(gm.get_by_role('status')).to_contain_text('Zapisano ustawienia kampanii.')
        # A genuine concurrent edit must still be rejected, never silently overwritten.
        db(f"from stories.models import Campaign\nfrom django.db.models import F\nCampaign.objects.filter(pk={cid!r}).update(summary='Zmiana drugiego MG', version=F('version')+1)")
        gm.locator('textarea[name=summary]').fill('Stara wersja')
        click(gm, 'Zapisz ustawienia')
        expect(gm.get_by_role('status')).to_contain_text('Kampania zmieniła się na serwerze')
        click(gm, 'Odśwież kampanię')
        expect(gm.locator('[data-tour=tab-materials]')).to_have_attribute('aria-current', 'page')
        expect(gm.locator('textarea[name=summary]')).to_have_value('Zmiana drugiego MG')
        browser.close()
    print('PASS: gallery mutations refresh campaign version; real conflicts remain; reload preserves settings view')
finally:
    db(f"from django.contrib.auth import get_user_model\nget_user_model().objects.filter(username__in={['frontend-gm-'+tag, 'frontend-player-'+tag]!r}).delete()")
