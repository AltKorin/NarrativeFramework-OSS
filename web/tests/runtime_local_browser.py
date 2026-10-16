"""Local runtime UI regression; use a migrated development/test database.

Run with the same DJANGO_SETTINGS_MODULE as the local backend. SITE_TEST_BASE
defaults to http://127.0.0.1:5173; configure backend CSRF_TRUSTED_ORIGINS for it.
BROWSER_EXECUTABLE_PATH can point to an existing Chromium installation.
"""
import os, sys, uuid, tempfile
from pathlib import Path
from urllib.parse import urlparse
sys.path.insert(0, str(Path(__file__).resolve().parents[1] / 'backend'))
os.environ.setdefault('DJANGO_SETTINGS_MODULE', 'config.settings')
os.environ.setdefault('DEBUG', '1')
origin = os.environ.get('SITE_TEST_BASE', 'http://127.0.0.1:5173').rstrip('/')
artifacts = Path(tempfile.mkdtemp(prefix='narrative-runtime-'))
import django
django.setup()
from django.contrib.auth import get_user_model
from django.test import Client
from stories.models import Campaign, WikiPage
from playwright.sync_api import sync_playwright
user = get_user_model().objects.create_user('runtime-browser-' + uuid.uuid4().hex[:8])
campaign = Campaign.objects.create(owner=user, title='The gate campaign')
WikiPage.objects.create(campaign=campaign, kind='npc', title='Guard')
WikiPage.objects.create(campaign=campaign, kind='pc', title='Hero')
client = Client()
client.force_login(user)
try:
    with sync_playwright() as p:
        browser = p.chromium.launch(headless=True, **({'executable_path': os.environ['BROWSER_EXECUTABLE_PATH']} if os.environ.get('BROWSER_EXECUTABLE_PATH') else {}))
        context = browser.new_context(viewport={'width':1440, 'height':1000})
        context.add_cookies([{'name':'sessionid','value':client.cookies['sessionid'].value,'domain':urlparse(origin).hostname,'path':'/'}])
        page = context.new_page()
        errors=[]
        page.on('pageerror', lambda e: errors.append(str(e)))
        from runtime_flow import runtime_flow
        try:
            runtime_flow(page, origin, str(campaign.id), artifacts)
        except Exception:
            page.screenshot(path=str(artifacts / 'failure.png'), full_page=True)
            print('Failure screenshot:', artifacts, flush=True)
            raise
        assert not errors, errors
        print('Screenshots:', artifacts)
        browser.close()
finally:
    user.delete()
    from django.contrib.sessions.models import Session
    Session.objects.filter(session_key=client.cookies["sessionid"].value).delete()
