Self-hosting is useful when you want to choose where campaigns live, when the application is updated and how backups are made. Narrative Framework runs from open source with Docker Compose. Your installation does not depend on the public test instance.

## What runs on your server

The stack includes Django and its API, PostgreSQL, Redis, a React frontend and Caddy. MCP runs as a separate service so its resource budget can be limited independently. This is still one server with some shared resources, not a guarantee against arbitrary agent traffic.

The browser provides an adventure graph, campaign wiki and individual-play panel. Unity is not required. Its original toolkit remains a historical component of the repository with a separate data format.

## Start locally

Get the [GitHub repository](https://github.com/AltKorin/NarrativeFramework-OSS), copy `web/.env.example` to `web/.env` and set two independent random secrets: `DJANGO_SECRET_KEY` and `POSTGRES_PASSWORD`. From the repository root, run:

```sh
docker compose -f web/compose.yaml up --build -d
```

The default URL is `http://localhost:8080`. The account-free demo keeps its draft in that browser only; an account allows server saves. The wiki requires login, and members join campaigns through invitations.

The complete [setup guide](/README.md) covers configuration and updates. Do not copy another instance's credentials or production settings onto your server.

## Before exposing it to the Internet

Configure your hostnames, CSRF origins and trusted proxy rules. Use HTTPS and secure cookies for public hosting. With Cloudflare Tunnel, the tunnel target replaces a direct DNS mapping to the container's private address.

PostgreSQL stores campaigns, publications, memberships and journals. A Docker volume survives container recreation but is not a backup. `docker compose down -v` deletes volumes. Keep database dumps private and test restoration on a separate instance.

## What open source gives you

The code uses the MIT license, with separate brand permissions. You can inspect authorization, develop features and host your own version. Dependencies and container images retain their own licenses.

JSON and Markdown exports help move material, but do not promise compatibility with every RPG tool. Kanka import and Unity-format conversion are not ready-made features in this edition.

## Where AI fits

Running the workshop needs no model-provider key. An external MCP client handles AI-led play. A personal author token can access GM material and should not be given to a player; an individual adventure has its own scoped credential.

If you only want to explore first, [open the public demo](/workshop/?lang=en). The [campaign wiki guide](/en/blog/rpg-campaign-wiki-without-spoilers/) explains preparing shared world knowledge.
