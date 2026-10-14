# Narrative Framework public website

[Polski](../pl/website.md) · [Setup](../../README.md)

The website presents the web RPG workshop. Unity appears only in the project's origin story. There is no Unity product pitch or claim of a built-in AI model.

## Routes and links

- `/`, `/pl/`, `/en/`: bilingual public website.
- `/workshop/`: editor, anonymous demo and account panel.
- `/workshop/?account=register&lang=en`: open the registration form.
- `/mcp`, `/play/mcp`, `/api/`: existing backend contracts.
- `/privacy.html`, `/cookies.html`: original existing legal pages, preserved without editorial changes.

Edit `web/frontend/public/site-config.json` to set the application, repository and support URLs. `repositoryUrl: null` displays an honest preparation notice until the public repository exists. Support points to `https://www.buymeacoffee.com/StolemCentrumGier` with the supplied label “Oil to my machine”. No third-party button script is loaded.

The default application link is `/workshop`. A separate deployment could use `https://app.narrativeframework.com/workshop/`; this is a proposed address, not a verified deployment. Configure Django hosts, trusted CSRF origins, MCP hosts/origins and HTTPS on that deployment too. Rebuild after link changes so prerendered HTML also contains the current configuration.

## Public test accounts

`ALLOW_REGISTRATION=1` enables signup; `0` closes it. `/api/session/` exposes this capability so the UI can hide signup when disabled.

`POST /api/register/` requires anonymous CSRF protection and `username`, `password`, `password_confirm`. Usernames accept 3–32 ASCII letters/digits/dots/underscores/hyphens. Passwords require at least 12 characters (maximum 4096) and Django's similarity/common/numeric password checks. Invalid input returns 400, duplicate names 409, missing CSRF 403. A separate per-IP throttle allows five attempts/hour. Success creates an ordinary user and session, returning 201. Extra privilege fields are ignored.

No email or automated password recovery is implemented; the form explains this. Administrators can use `docker compose exec api python manage.py changepassword USERNAME`. Important adventures can be exported. The original legal pages have been preserved; account/data and hosting changes need to be reconciled with those documents when public hosting is finalized.

## Build and verification

From repository root:

```sh
python3 web/scripts/sync-site-docs.py
npm --prefix web/frontend ci
npm --prefix web/frontend run build
docker compose -f web/compose.yaml up --build -d
docker compose -f web/compose.yaml exec -T api python manage.py test stories
PLAYWRIGHT_BROWSERS_PATH=/tmp/narrative-browsers web/.venv/bin/python web/tests/site_browser.py
```

Optional Playwright setup is in [verification](../verification.md). The browser test creates a disposable account through the real signup form, saves a private adventure and deletes the account in `finally`.

Landing and editor are separate lazy bundles. A build-time SSR pass emits complete static PL/EN HTML, descriptions, canonical/hreflang and Open Graph metadata. No Node server is deployed. Caddy serves the site and proxies API/MCP. Optional analytics requires consent; application measurement is separate and disabled by default. Remote fonts and donation scripts are not loaded. The harbor illustration is AI-generated; the editor image is a real local demo screenshot.

## Reference deployment with Cloudflare Tunnel

The public website is https://narrativeframework.com and the workshop is https://app.narrativeframework.com/workshop/. The reference deployment uses a dedicated Debian 13 LXC, Docker Compose and a systemd cloudflared service. PostgreSQL has a separate persistent volume; no development database or test accounts were transferred.

The tunnel terminates public HTTPS and forwards to Caddy inside the guest. Set `SECURE_COOKIES=1`. Configure `TRUSTED_PROXY_CIDRS` with only the actual local proxy addresses, including the Docker gateway when the host connector uses a published Docker port. Caddy uses strict right-to-left XFF parsing, rejects spoofing from untrusted peers and forwards the resolved client IP to the API. This keeps registration limits per visitor rather than per tunnel.

`PUBLIC_HOSTS` lists the public names whose proxy-forwarded HTTP requests must redirect to HTTPS. Configure your own hostnames and proxy CIDRs for another installation.

Tunnel credentials/configuration remain on the server, outside source and Docker images. The connector starts on boot. The old apex/www DNS records were backed up before cutover; mail and unrelated subdomains were not modified. Preserve instance-specific `.env` and `site-config.json` values on source upgrades. `web/scripts/package-web.py` excludes local secrets, databases, dependencies and private deployment notes.

Target-host and public HTTPS results are in [verification](../verification.md). For newly propagated DNS, tests accept `SITE_TEST_RESOLVE_IP` with a verified public Cloudflare IP. This changes only the test process resolver, still validates TLS for the real hostname and never changes system DNS or hosts.

A concrete guest-preparation walkthrough is available in [Polish](../pl/lxc-setup.md).


[Blog / SEO / Analytics](publishing-and-analytics.md)
