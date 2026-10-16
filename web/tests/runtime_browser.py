"""Runtime browser regression on a disposable Compose instance or explicit SSH target."""
import json, os, secrets, subprocess, uuid, tempfile
from browser_target import browser_args
from pathlib import Path
from playwright.sync_api import sync_playwright
origin = os.environ.get('SITE_TEST_BASE', 'http://localhost:8080').rstrip('/')
remote = os.environ.get('SITE_TEST_SSH')
artifacts = Path(tempfile.mkdtemp(prefix='narrative-runtime-public-'))
suffix = uuid.uuid4().hex[:10]
owner, player = 'runtime-owner-' + suffix, 'runtime-player-' + suffix
password = secrets.token_urlsafe(24)
shell = (['ssh', '-o', 'BatchMode=yes', remote, 'cd /opt/narrativeframework/web && docker compose exec -T api python manage.py shell'] if remote else ['docker', 'compose', '-f', 'web/compose.yaml', 'exec', '-T', 'api', 'python', 'manage.py', 'shell'])
setup = f"from django.contrib.auth import get_user_model\nfrom stories.models import Campaign, CampaignMember, WikiPage\na=get_user_model().objects.create_user({owner!r},password={password!r})\nb=get_user_model().objects.create_user({player!r},password={password!r})\nc=Campaign.objects.create(owner=a,title='Runtime HTTPS verification')\nCampaignMember.objects.create(campaign=c,user=b,role='player')\nWikiPage.objects.create(campaign=c,kind='npc',title='Guard')\nWikiPage.objects.create(campaign=c,kind='pc',title='Hero')\nimport json\nprint(json.dumps({{'campaign':str(c.id)}}))\n"
result=subprocess.run(shell,input=setup,text=True,capture_output=True,check=True)
campaign_id=json.loads(result.stdout.strip().splitlines()[-1])['campaign']
def login(page, username):
    page.goto(origin + '/workshop/?account=login&lang=en',wait_until='networkidle')
    page.get_by_label('Username',exact=True).fill(username)
    page.get_by_label('Password',exact=True).fill(password)
    page.locator('.account-panel').get_by_role('button',name='Log in',exact=True).click()
    page.locator('header').get_by_role('button',name=username,exact=True).wait_for()
try:
    with sync_playwright() as p:
        browser=p.chromium.launch(headless=True,args=browser_args(),**({'executable_path': os.environ['BROWSER_EXECUTABLE_PATH']} if os.environ.get('BROWSER_EXECUTABLE_PATH') else {}))
        context=browser.new_context(viewport={'width':1440,'height':1000})
        page=context.new_page()
        errors=[]
        page.on('pageerror',lambda e: errors.append(str(e)))
        login(page,owner)
        from runtime_flow import runtime_flow
        runtime_flow(page, origin, campaign_id, artifacts)
        assert not errors, errors
        public=browser.new_page(viewport={'width':1440,'height':1000})
        login(public,player)
        public.goto(f'{origin}/campaigns/?lang=en&campaign={campaign_id}',wait_until='networkidle')
        assert public.get_by_role('region',name='Run campaign').count() == 0
        result=public.evaluate("""async (id)=>{const r=await fetch('/api/campaigns/'+id+'/runtime/');return {status:r.status,body:await r.json()}}""",campaign_id)
        assert result['status']==404,result
        exported=public.evaluate("""async(id)=>await (await fetch('/api/campaigns/'+id+'/export/')).json()""",campaign_id)
        assert 'runtime' not in exported,exported
        assert not errors,errors
        print('PASS runtime browser: runtime workflow, player isolation and export filtering')
        print('Screenshots:',artifacts)
        browser.close()
finally:
    cleanup=f"from django.contrib.auth import get_user_model\nget_user_model().objects.filter(username__in={[owner,player]!r}).delete()\n"
    subprocess.run(shell,input=cleanup,text=True,capture_output=True,check=True)
