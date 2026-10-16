"""Complete group wiki workflow through the actual proxy; disposable accounts only."""

import json
import os
import secrets
import subprocess
import uuid
from urllib.parse import urlparse, parse_qs
from playwright.sync_api import sync_playwright
from browser_target import browser_args

origin = os.environ.get("SITE_TEST_BASE", "http://localhost:8080").rstrip("/")
remote = os.environ.get("SITE_TEST_SSH")
shell = (
    [
        "ssh",
        "-o",
        "BatchMode=yes",
        remote,
        "cd /opt/narrativeframework/web && docker compose exec -T api python manage.py shell",
    ]
    if remote
    else [
        "docker",
        "compose",
        "-f",
        "web/compose.yaml",
        "exec",
        "-T",
        "api",
        "python",
        "manage.py",
        "shell",
    ]
)
suffix = uuid.uuid4().hex[:10]
names = {
    role: f"sharing-{role}-{suffix}"
    for role in ("owner", "alice", "bob", "gm", "stranger")
}
password = secrets.token_urlsafe(24)
subprocess.run(
    shell,
    input=f"from django.contrib.auth import get_user_model\nfor n in {list(names.values())!r}:\n get_user_model().objects.create_user(n,password={password!r})\n",
    text=True,
    capture_output=True,
    check=True,
)
errors = []


def req(page, path, method="GET", data=None):
    return page.evaluate(
        """async ({path,method,data})=>{
      const session=await (await fetch('/api/session/')).json();
      const r=await fetch('/api/'+path,{method,headers:{'Content-Type':'application/json','X-CSRFToken':session.csrf},...(data===null?{}:{body:JSON.stringify(data)})});
      return {status:r.status,body:await r.json()};
    }""",
        {"path": path, "method": method, "data": data},
    )


def login(page, who):
    page.goto(origin + "/workshop/?account=login&lang=en", wait_until="networkidle")
    credentials(page, who)


def credentials(page, who):
    page.get_by_label("Username", exact=True).fill(names[who])
    page.get_by_label("Password", exact=True).fill(password)
    page.locator(".account-panel").get_by_role(
        "button", name="Log in", exact=True
    ).click()


def idle(page):
    page.locator(".wiki-layout:not([inert])").wait_for()


try:
    with sync_playwright() as p:
        browser = p.chromium.launch(headless=True, args=browser_args())
        owner = browser.new_page(
            locale="en-GB", viewport={"width": 1440, "height": 1100}
        )
        owner.on("pageerror", lambda e: errors.append(str(e)))
        login(owner, "owner")
        owner.locator("header").get_by_role("button", name=names["owner"]).wait_for()
        owner.goto(origin + "/campaigns/?lang=en", wait_until="networkidle")
        owner.get_by_label("New campaign name", exact=True).fill(
            "Shared northern coast"
        )
        owner.get_by_role("button", name="Create campaign", exact=True).click()
        owner.get_by_role("button", name="World material", exact=True).click()
        idle(owner)
        owner.get_by_role("button", name="New article", exact=True).wait_for()
        cid = parse_qs(urlparse(owner.url).query)["campaign"][0]
        root = f"campaigns/{cid}/"
        owner.get_by_text("Members and invitations", exact=True).click()

        def invite(role="player"):
            owner.get_by_label("Invitation role", exact=True).select_option(role)
            if role == "gm":
                owner.once("dialog", lambda d: d.accept())
            with owner.expect_response(
                lambda r: r.url.endswith("/invitations/") and r.request.method == "POST"
            ) as response:
                owner.get_by_role(
                    "button", name="Create invitation", exact=True
                ).click()
            assert response.value.status == 200
            idle(owner)
            field = owner.get_by_label("Copy invitation link", exact=True)
            field.wait_for()
            return field.input_value()

        alice = browser.new_page(locale="en-GB")
        alice.on("pageerror", lambda e: errors.append(str(e)))
        alice.goto(invite(), wait_until="networkidle")
        alice.get_by_role(
            "link", name="Log in or create an account", exact=True
        ).click()
        credentials(alice, "alice")
        alice.get_by_role("button", name="Accept invitation", exact=True).wait_for()
        alice.get_by_role("button", name="Accept invitation", exact=True).click()
        alice.get_by_role("button", name="Leave campaign", exact=True).wait_for()
        idle(alice)
        assert not alice.get_by_role("button", name="New article", exact=True).count()

        bob = browser.new_page(locale="en-GB")
        login(bob, "bob")
        bob.locator("header").get_by_role("button", name=names["bob"]).wait_for()
        bob.goto(invite(), wait_until="networkidle")
        bob.get_by_role("button", name="Accept invitation", exact=True).click()
        idle(bob)

        gm = browser.new_page(locale="en-GB")
        login(gm, "gm")
        gm.locator("header").get_by_role("button", name=names["gm"]).wait_for()
        gm.goto(invite("gm"), wait_until="networkidle")
        gm.get_by_role("button", name="Accept invitation", exact=True).click()
        gm.get_by_role("button", name="World material", exact=True).click()
        gm.get_by_role("button", name="New article", exact=True).wait_for()
        idle(gm)
        members = req(owner, root + "members/")["body"]["members"]
        member_ids = {m["username"]: m["id"] for m in members}

        def article(title, body, notes=""):
            x = req(
                owner,
                root + "pages/",
                "POST",
                {
                    "id": str(uuid.uuid4()),
                    "title": title,
                    "kind": "lore",
                    "body": body,
                    "gm_notes": notes,
                },
            )
            assert x["status"] == 201, x
            return x["body"]

        port = article("Port", "Published harbor text", "SECRET_GM_BROWSER")
        guild = article("Guild", "Cartographers")
        debt = article("ALICE_PRIVATE_DEBT", "Private debt")
        hidden = article("HIDDEN_MAYOR", "Never published")
        owner.goto(
            origin + f"/campaigns/?campaign={cid}&page={port['id']}&lang=en",
            wait_until="networkidle",
        )
        owner.get_by_text("Player publication", exact=True).click()
        publication = owner.locator(".wiki-content .wiki-publication").first
        publication.get_by_label("Audience", exact=True).select_option("all")
        publication.get_by_role(
            "button", name="Publish for players", exact=True
        ).click()
        idle(owner)
        owner.get_by_text("Published version:", exact=False).wait_for()

        def publish(obj, audience="all", recipients=None, relation=False):
            path = (
                root
                + ("relations/" if relation else "pages/")
                + obj["id"]
                + "/publication/"
            )
            r = req(
                owner,
                path,
                "POST",
                {
                    "version": obj["version"],
                    "audience": audience,
                    "recipients": recipients or [],
                },
            )
            assert r["status"] == 200, r
            return r["body"]

        guild = publish(guild)
        owner.goto(
            origin + f"/campaigns/?campaign={cid}&page={debt['id']}&lang=en",
            wait_until="networkidle",
        )
        owner.get_by_text("Player publication", exact=True).click()
        owner.locator(".wiki-content .wiki-publication").get_by_label(
            "Audience", exact=True
        ).select_option("selected")
        owner.get_by_role("checkbox", name=names["alice"], exact=True).check()
        owner.get_by_role("button", name="Publish for players", exact=True).click()
        idle(owner)
        owner.get_by_text("Published version:", exact=False).wait_for()
        debt = req(owner, root + f"pages/{debt['id']}/")["body"]
        relation = req(
            owner,
            root + "relations/",
            "POST",
            {
                "id": str(uuid.uuid4()),
                "source": port["id"],
                "target": guild["id"],
                "label": "HIDDEN_RELATION",
            },
        )["body"]
        current = req(owner, root)["body"]
        relation = next(r for r in current["relations"] if r["id"] == relation["id"])

        def view(page, pid):
            page.goto(
                origin + f"/campaigns/?campaign={cid}&page={pid}&lang=en",
                wait_until="networkidle",
            )
            page.locator(".wiki-article-title").wait_for()
            idle(page)

        view(alice, port["id"])
        view(bob, port["id"])
        assert "ALICE_PRIVATE_DEBT" in alice.locator(".wiki-index").inner_text()
        for who in (alice, bob):
            text = who.locator("body").inner_text()
            assert (
                "SECRET_GM_BROWSER" not in text
                and "HIDDEN_MAYOR" not in text
                and "HIDDEN_RELATION" not in text
            )
            assert not who.get_by_role("button", name="Edit", exact=True).count()
        assert "ALICE_PRIVATE_DEBT" not in bob.locator("body").inner_text()
        assert req(bob, root + f"pages/{debt['id']}/")["status"] == 404
        assert not req(alice, root + "pages/?q=SECRET_GM_BROWSER")["body"]
        with alice.expect_download() as download:
            alice.get_by_role(
                "button", name="Export shared material", exact=True
            ).click()
        exported = json.loads(open(download.value.path()).read())
        assert exported["schema"] == "narrative-wiki-player/1"
        assert "SECRET_GM_BROWSER" not in str(exported) and "HIDDEN_MAYOR" not in str(
            exported
        )
        relation = publish(relation, relation=True)
        alice.reload(wait_until="networkidle")
        alice.locator(".wiki-relations").get_by_role(
            "button", name="→ Guild", exact=True
        ).wait_for()
        alice.get_by_role("button", name="Relationship map", exact=True).click()
        alice.locator(".wiki-map .react-flow__node").first.wait_for()
        assert alice.locator(".wiki-map .react-flow__edge").count() == 1

        current = req(owner, root + f"pages/{port['id']}/")["body"]
        assert (
            req(
                owner,
                root + f"pages/{port['id']}/",
                "PUT",
                {**current, "body": "UNPUBLISHED_CHANGE", "title": "Port draft title"},
            )["status"]
            == 200
        )
        alice.reload(wait_until="networkidle")
        assert "UNPUBLISHED_CHANGE" not in alice.locator("body").inner_text()
        assert (
            req(alice, root + f"pages/{port['id']}/")["body"]["body"]
            == "Published harbor text"
        )
        view(owner, port["id"])
        owner.get_by_label("Preview as player", exact=True).select_option(
            member_ids[names["bob"]]
        )
        idle(owner)
        owner.get_by_text("Published harbor text", exact=True).wait_for()
        assert "ALICE_PRIVATE_DEBT" not in owner.locator("body").inner_text()
        assert "SECRET_GM_BROWSER" not in owner.locator(".wiki-content").inner_text()
        owner.screenshot(path="/tmp/narrative-sharing-preview.png", full_page=True)
        owner.get_by_label("Preview as player", exact=True).select_option("")
        idle(owner)

        view(gm, port["id"])
        assert (
            req(gm, root + f"pages/{port['id']}/")["body"]["gm_notes"]
            == "SECRET_GM_BROWSER"
        )
        state = req(owner, root)["body"]
        assert (
            req(
                owner,
                root + f"members/{member_ids[names['gm']]}/",
                "PATCH",
                {"version": state["version"], "role": "player"},
            )["status"]
            == 200
        )
        gm.evaluate("window.dispatchEvent(new Event('focus'))")
        gm.get_by_role("button", name="Edit", exact=True).wait_for(state="hidden")
        assert "SECRET_GM_BROWSER" not in gm.locator("body").inner_text()
        assert "UNPUBLISHED_CHANGE" not in gm.locator("body").inner_text()

        alice.get_by_role("button", name="Articles", exact=True).click()
        alice.get_by_label("Search titles and content", exact=True).fill(
            "ALICE_PRIVATE_DEBT"
        )
        alice.locator(".wiki-index").get_by_role(
            "button", name="ALICE_PRIVATE_DEBT", exact=True
        ).wait_for()
        alice.wait_for_timeout(350)
        publish(debt, "gm")
        alice.evaluate("window.dispatchEvent(new Event('focus'))")
        alice.locator(".wiki-index").get_by_role(
            "button", name="ALICE_PRIVATE_DEBT", exact=True
        ).wait_for(state="hidden")
        alice.get_by_label("Search titles and content", exact=True).fill("")
        alice.get_by_role("button", name="PL", exact=True).click()
        alice.set_viewport_size({"width": 390, "height": 844})
        assert alice.evaluate("document.documentElement.scrollWidth<=innerWidth")
        alice.screenshot(
            path="/tmp/narrative-sharing-player-mobile.png", full_page=True
        )
        assert (
            req(owner, root + f"members/{member_ids[names['alice']]}/", "DELETE")[
                "status"
            ]
            == 200
        )
        alice.evaluate("window.dispatchEvent(new Event('focus'))")
        alice.locator(".wiki-article-title").wait_for(state="hidden")
        assert req(alice, root + "export/")["status"] == 404
        assert req(bob, root)["status"] == 200
        stranger = browser.new_page(locale="en-GB")
        login(stranger, "stranger")
        stranger.locator("header").get_by_role(
            "button", name=names["stranger"]
        ).wait_for()
        assert req(stranger, root)["status"] == 404
        assert not errors, errors
        browser.close()
    print(
        "PASS: UI invitations/login return, co-GM/player roles, publication, individual audiences, hidden graph/search/export, GM preview, draft isolation, demotion/revocation, PL/EN and mobile"
    )
finally:
    subprocess.run(
        shell,
        input=f"from django.contrib.auth import get_user_model\nget_user_model().objects.filter(username__in={list(names.values())!r}).delete()\n",
        text=True,
        capture_output=True,
        check=True,
    )
