# Dependencies, assets and provenance

The root MIT license covers this repository's original code/documentation. External dependencies and container images retain their own licenses; they are downloaded during installation/build rather than relicensed by this repository. Lockfiles record the release's resolved Python and JavaScript dependencies, including transitive packages. Preserve upstream notices when redistributing dependency code, binaries or images.

## Main application dependencies

| Dependency | Upstream license metadata |
| --- | --- |
| React, React DOM, React Flow (`@xyflow/react`) | MIT |
| react-markdown, remark-gfm, Vite | MIT |
| TypeScript | Apache-2.0 |
| Django | BSD-3-Clause |
| Django REST framework | BSD |
| psycopg | LGPL-3.0-only |
| Uvicorn, HTTPX | BSD-3-Clause |
| Python MCP SDK, redis-py | MIT |

This is a navigation aid, not a complete transitive license inventory. Authoritative texts ship with each upstream distribution. Docker Compose also pulls PostgreSQL, Redis, Python, Node and Caddy images: their programs, base systems and bundled components have separate upstream terms. In particular, redis-py's license is not a statement about the Redis server image's license.

## Repository assets

- `web/frontend/public/images/workshop.png` is a screenshot of this application's fictional demonstration scenario.
- `web/frontend/public/images/harbor.jpg` and `harbor.webp` are AI-generated demonstration artwork (two encodings of the same illustration).
- The original Unity demonstration illustrations under `Assets/NarrativeFrameworkForUnity/Data/Illustrations` and `Images` are AI-generated and provided as-is for demonstration.
- Brand names and logos are subject to the brand statement in `BRANDING.md`.
- Kanka is mentioned as an existing tool and possible future import source. No Kanka code or assets are included.

The historical Unity code depends on Unity and TextMeshPro in a user's Unity installation. Those products are not bundled here and are governed by their own terms. Legacy optional AI integrations require the user's provider account/key; the web workshop does not bundle a model or require provider credentials.

## Polski

Zależności i obrazy kontenerów zachowują własne licencje; MIT projektu ich nie zastępuje. Wersje znajdują się w lockfile'ach. Zrzut edytora przedstawia fikcyjne demo, ilustracje portu i oryginalne ilustracje demonstracyjne Unity powstały z pomocą AI. Kod ani materiały Kanki nie są dołączone. Unity i TextMeshPro pozostają zewnętrznymi zależnościami części historycznej.
