"""Separate MCP process. Bounded requests, atomic Redis admission and owner isolation."""

import os, asyncio, hashlib, uuid, json
from contextvars import ContextVar
from contextlib import asynccontextmanager
from starlette.applications import Starlette
from starlette.routing import Mount

os.environ.setdefault("DJANGO_SETTINGS_MODULE", "config.settings")
import django

django.setup()
from asgiref.sync import sync_to_async
from django.db import close_old_connections, OperationalError, InterfaceError


async def db_call(fn, *args):
    def invoke():
        close_old_connections()
        try:
            return fn(*args)
        except (OperationalError, InterfaceError):
            raise DomainError(
                "Service dependency unavailable; retry later", 503
            ) from None
        finally:
            close_old_connections()

    return await sync_to_async(invoke)()


from rest_framework.authtoken.models import Token
from mcp.server.fastmcp import FastMCP
from mcp.server.transport_security import TransportSecuritySettings
from redis.asyncio import Redis
from .models import Scenario
from . import play, wiki, wiki_sharing
from .transport import bounded_json
from .domain import serialize, save_document, inspect_document, DomainError
from .domain import create_scenario as create_scenario_document

BODY_TIMEOUT_SECONDS = 10
TOOL_TIMEOUT_SECONDS = 25

principal = ContextVar("principal")
mcp = FastMCP(
    "Narrative Framework",
    stateless_http=True,
    json_response=True,
    transport_security=TransportSecuritySettings(
        enable_dns_rebinding_protection=True,
        allowed_hosts=os.environ.get(
            "MCP_ALLOWED_HOSTS", "localhost:*,127.0.0.1:*,mcp:*"
        ).split(","),
        allowed_origins=[
            x for x in os.environ.get("MCP_ALLOWED_ORIGINS", "").split(",") if x
        ],
    ),
)


@mcp.tool()
async def list_scenarios() -> list[dict]:
    """List your scenarios with IDs and versions; does not download their full content."""
    return await db_call(
        lambda: list(
            Scenario.objects.filter(owner=principal.get(), is_demo=False).values(
                "id", "title", "version"
            )
        )
    )


@mcp.tool()
async def read_scenario(scenario_id: str) -> dict:
    """Read one owned scenario, up to 250 nodes. Includes GM secrets."""

    def read():
        try:
            s = Scenario.objects.get(
                pk=uuid.UUID(scenario_id), owner=principal.get(), is_demo=False
            )
        except (Scenario.DoesNotExist, ValueError):
            raise ValueError("Scenario not found")
        return serialize(s)

    return await db_call(read)


@mcp.tool()
async def save_scenario(
    scenario_id: str, expected_version: int, title: str, document: dict
) -> dict:
    """Atomically save a complete bounded scenario. Read first and supply its version. On conflict reload; never retry blindly. Identical retry after success returns a conflict, without duplicate writes."""
    try:
        return await db_call(
            save_document,
            principal.get(),
            uuid.UUID(scenario_id),
            expected_version,
            title,
            document,
        )
    except DomainError as e:
        raise ValueError(f"{e.status}: {e}") from e


@mcp.tool()
async def validate_scenario(scenario_id: str, language: str = "en") -> dict:
    """Check scene reachability and clue-source coverage. These are structural hints, not a proof of narrative quality."""
    s = await read_scenario(scenario_id)
    return {
        "version": s["version"],
        "issues": inspect_document(s["document"], "pl" if language == "pl" else "en"),
    }


# Wiki tools belong only to the author catalog, never to scoped player tokens.
async def wiki_call(fn, *args):
    try:
        user = principal.get()
        if fn in (wiki.list_campaigns, wiki.search_pages, wiki.read_page):
            return await db_call(lambda: fn(user, *args, author_only=True))
        return await db_call(fn, user, *args)
    except DomainError as error:
        raise ValueError(f"{error.status}: {error}") from error


@mcp.tool()
async def list_campaigns() -> list[dict]:
    """List owned or co-managed campaigns. Player memberships do not grant author access."""
    return await wiki_call(wiki.list_campaigns)


@mcp.tool()
async def search_wiki(campaign_id: str, query: str = "") -> list[dict]:
    """Search owned/co-managed campaign draft titles/body; at most 100 metadata entries, no full bodies."""
    return await wiki_call(wiki.search_pages, campaign_id, query)


@mcp.tool()
async def read_wiki_page(campaign_id: str, page_id: str) -> dict:
    """Read one owned/co-managed campaign draft article, private GM notes and explicit relations. GM-only."""
    return await wiki_call(wiki.read_page, campaign_id, page_id)


@mcp.tool()
async def save_wiki_page(
    campaign_id: str,
    page_id: str,
    expected_version: int,
    title: str,
    kind: str,
    body: str,
    gm_notes: str = "",
) -> dict:
    """Save an existing owned/co-managed draft article with expected version. Includes/replaces private notes; read first. On conflict, reconcile before retrying."""
    return await wiki_call(
        wiki.save_page,
        campaign_id,
        page_id,
        {
            "version": expected_version,
            "title": title,
            "kind": kind,
            "body": body,
            "gm_notes": gm_notes,
        },
    )


def gm_campaign_detail(user, campaign_id):
    # campaign_detail also serves players; author tools must never fall back to a player projection.
    wiki.campaign_for(user, campaign_id)
    return wiki.campaign_detail(user, campaign_id)


def runtime_command(user, campaign_id, data, preview):
    from . import campaign_runtime

    return campaign_runtime.command(user, campaign_id, data, preview=preview)


def runtime_read(user, campaign_id):
    from . import campaign_runtime

    return campaign_runtime.read(user, campaign_id)


def import_template(user, data):
    from . import campaign_templates

    return campaign_templates.create(user, data)[0]


@mcp.tool()
async def create_campaign(campaign_id: str, title: str, summary: str = "") -> dict:
    """Create an owned campaign. Generate a new UUID for campaign_id; an identical retry returns the same campaign. The summary is visible to all members — keep secrets out."""
    result = await wiki_call(wiki.create_campaign, {"id": campaign_id, "title": title, "summary": summary})
    return result[0]


@mcp.tool()
async def import_campaign_template(campaign_id: str, document: dict) -> dict:
    """Create a private campaign with pages, relations and engine state from a narrative-campaign-template/1 document (max 100 pages, 450 KB). Generate a new UUID for campaign_id."""
    return await wiki_call(import_template, {"id": campaign_id, "document": document})


@mcp.tool()
async def read_campaign(campaign_id: str) -> dict:
    """Read campaign metadata, version, page index (no bodies) and all relations. GM-only; use read_wiki_page for bodies."""
    return await wiki_call(gm_campaign_detail, campaign_id)


@mcp.tool()
async def update_campaign(campaign_id: str, expected_version: int, title: str, summary: str = "",
                          appearance: str | None = None, page_template: str | None = None) -> dict:
    """Rename or re-describe an owned campaign, optionally switching appearance/page_template (fantasy or cyberpunk; omitted = unchanged). Owner-only; supply the current campaign version."""
    data = {"version": expected_version, "title": title, "summary": summary}
    data.update({k: v for k, v in (("appearance", appearance), ("page_template", page_template)) if v is not None})
    return await wiki_call(wiki.update_campaign, campaign_id, data)


@mcp.tool()
async def delete_campaign(campaign_id: str, expected_version: int) -> dict:
    """Permanently delete an owned campaign with all pages, relations, publications and members. Owner-only; irreversible — confirm with the user first."""
    return await wiki_call(wiki.delete_campaign, campaign_id, {"version": expected_version})


@mcp.tool()
async def create_wiki_page(
    campaign_id: str, page_id: str, title: str, kind: str, body: str = "", gm_notes: str = ""
) -> dict:
    """Create a GM-only draft page. Generate a new UUID for page_id. kind: pc, npc, location, organization, item, lore, journal, scene, clue, timeline. Markdown body up to 12000 chars; gm_notes are never published."""
    result = await wiki_call(wiki.create_page, campaign_id,
                             {"id": page_id, "title": title, "kind": kind, "body": body, "gm_notes": gm_notes})
    return result[0]


@mcp.tool()
async def delete_wiki_page(campaign_id: str, page_id: str, expected_version: int) -> dict:
    """Delete a page and its relations. Rejected while the campaign engine uses the page as a character."""
    return await wiki_call(wiki.delete_page, campaign_id, page_id, expected_version)


@mcp.tool()
async def publish_wiki_page(
    campaign_id: str, page_id: str, expected_version: int, audience: str, recipients: list[str] = []
) -> dict:
    """Snapshot the saved page for players. audience: all, selected (recipients = player membership UUIDs) or gm (withdraw). GM notes are never copied; check the body for secrets first."""
    return await wiki_call(wiki_sharing.publish, campaign_id, page_id,
                           {"version": expected_version, "audience": audience, "recipients": recipients})


@mcp.tool()
async def create_wiki_relation(campaign_id: str, relation_id: str, source: str, target: str, label: str) -> dict:
    """Link two saved pages with a label (max 80 chars). Generate a new UUID for relation_id. New relations are GM-only."""
    result = await wiki_call(wiki.create_relation, campaign_id,
                             {"id": relation_id, "source": source, "target": target, "label": label})
    return result[0]


@mcp.tool()
async def delete_wiki_relation(campaign_id: str, relation_id: str) -> dict:
    """Delete a relation between pages."""
    return await wiki_call(wiki.delete_relation, campaign_id, relation_id)


@mcp.tool()
async def publish_wiki_relation(
    campaign_id: str, relation_id: str, expected_version: int, audience: str, recipients: list[str] = []
) -> dict:
    """Publish a relation label; players see it only if both pages are also published to them. audience: all, selected or gm."""
    return await wiki_call(wiki_sharing.publish, campaign_id, relation_id,
                           {"version": expected_version, "audience": audience, "recipients": recipients}, True)


@mcp.tool()
async def read_campaign_runtime(campaign_id: str) -> dict:
    """Read the campaign engine: day, flags, resources, variables, actor stats, relationships, events with blockers and history."""
    return await wiki_call(runtime_read, campaign_id)


@mcp.tool()
async def run_campaign_command(campaign_id: str, expected_version: int, command: dict, preview: bool = False) -> dict:
    """Change the campaign engine with one command; preview=true shows the result without saving. command.action: add_event/update_event {event}, set_value {scope: resources|variables|actors, key, value, actorId?}, set_flags {flags}, advance, resolve {eventId, choice}. Actor IDs are wiki page IDs of pc/npc/organization pages."""
    return await wiki_call(runtime_command, campaign_id, {**command, "version": expected_version}, preview)


@mcp.tool()
async def create_scenario(scenario_id: str, title: str, document: dict) -> dict:
    """Create an owned adventure (scenario graph). Generate a new UUID; an identical retry returns the same scenario."""
    try:
        result = await db_call(create_scenario_document, principal.get(), uuid.UUID(scenario_id), title, document)
    except DomainError as e:
        raise ValueError(f"{e.status}: {e}") from e
    return result[0]


@mcp.tool()
async def copy_wiki_page_to_scenario(
    campaign_id: str, page_id: str, source_version: int, scenario_id: str, scenario_version: int
) -> dict:
    """Copy a campaign page into an owned adventure as an independent node (GM notes become the node secret)."""
    return await wiki_call(wiki.import_to_scenario, campaign_id, page_id,
                           {"source_version": source_version, "scenario_id": scenario_id, "version": scenario_version})


def characters_call(name):
    def call(user, campaign_id, *args):
        from . import characters

        # The character service also serves players; author tools stay GM-only.
        wiki.campaign_for(user, campaign_id)
        return getattr(characters, name)(user, campaign_id, *args)

    return call


@mcp.tool()
async def set_campaign_ruleset(campaign_id: str, expected_revision: int, document: dict) -> dict:
    """Upload or replace the campaign's narrative-ruleset/1 pack (stats, skills, formulas, pools, catalog, tables, roles, methods). Use expected_revision 0 for the first upload. The whole pack is rejected on any error. Commercial rules stay campaign-private."""
    return await wiki_call(characters_call("set_ruleset"), campaign_id, {"version": expected_revision, "document": document})


@mcp.tool()
async def read_campaign_ruleset(campaign_id: str) -> dict:
    """Read the campaign's ruleset pack and revision."""
    return await wiki_call(characters_call("read_ruleset"), campaign_id)


@mcp.tool()
async def delete_campaign_ruleset(campaign_id: str, expected_revision: int) -> dict:
    """Remove the campaign's ruleset. Drafts in progress keep their own copy and can be finished."""
    return await wiki_call(characters_call("delete_ruleset"), campaign_id, {"version": expected_revision})


@mcp.tool()
async def list_character_drafts(campaign_id: str) -> list[dict]:
    """List all character drafts of a campaign with status (draft, submitted, returned, approved)."""
    return await wiki_call(characters_call("list_drafts"), campaign_id)


@mcp.tool()
async def read_character_draft(campaign_id: str, draft_id: str) -> dict:
    """Read a draft: computed sheet, remaining problems, chosen items, dice log and event history."""
    return await wiki_call(characters_call("read_draft"), campaign_id, draft_id)


@mcp.tool()
async def create_character_draft(campaign_id: str, draft_id: str, method: str) -> dict:
    """Start a GM character (NPC/pregen) with a ruleset method. Generate a new UUID. GM characters are approved on submit."""
    result = await wiki_call(characters_call("create_draft"), campaign_id, {"id": draft_id, "method": method})
    return result[0]


@mcp.tool()
async def run_character_command(campaign_id: str, draft_id: str, expected_version: int, command: dict) -> dict:
    """Apply one action to a draft. Builder actions (own GM drafts): choose_role {role}, choose_template {index}, set_stats {values}, roll_stats, set_skills {values}, set_gear {items}, set_items {items}, roll_table {table}, choose_table {table, entry}, set_name {name, look}, submit. Review actions (any draft): approve, return {note}, grant_reroll {target: "stats" or table id}. Dice are rolled by the server."""
    return await wiki_call(characters_call("command"), campaign_id, draft_id, {**command, "version": expected_version})


@mcp.tool()
async def delete_character_draft(campaign_id: str, draft_id: str, expected_version: int) -> dict:
    """Delete a draft that is not approved yet."""
    return await wiki_call(characters_call("delete_draft"), campaign_id, draft_id, {"version": expected_version})


# A separate tool catalog and scoped credentials keep player clients out of author tools.
play_scope = ContextVar("play_scope")
play_mcp = FastMCP(
    "Narrative Framework — Individual Play",
    instructions=play.AGENT_CONTRACT,
    stateless_http=True,
    json_response=True,
    transport_security=mcp.settings.transport_security,
)


async def play_call(fn, *args):
    session_id, digest = play_scope.get()
    try:
        return await db_call(fn, principal.get(), session_id, digest, *args)
    except DomainError as error:
        raise ValueError(f"{error.status}: {error}") from error


@play_mcp.tool()
async def get_play_context() -> dict:
    """Resume the single assigned adventure authorized by this token. Includes published materials, current Markdown memory, five recent events and up to twenty recent consequences. No author secrets. Follow agent_contract; pending effects need the human GM."""
    return await play_call(play.play_context)


@play_mcp.tool()
async def read_play_journal(offset: int = 0, limit: int = 10) -> dict:
    """Read an ordered page of play events (limit 1–20). Omit private author/GM notes."""

    def read(user, session_id, digest, offset, limit):
        return play.journal(play.check_grant(user, session_id, digest), offset, limit)

    return await play_call(read, offset, limit)


@play_mcp.tool()
async def search_play_memory(query: str) -> dict:
    """Search the current Markdown memory and recorded actions/narration. Returns up to ten matching journal excerpts, never other sessions or GM-private notes."""

    def search(user, session_id, digest, query):
        query = play.text(query, "query", 200, True)
        session = play.check_grant(user, session_id, digest)
        matches = []
        for event in session.events.filter(kind__in=["turn", "finish"]):
            content = "\n".join(str(v) for v in event.payload.values())
            index = content.casefold().find(query.casefold())
            if index >= 0:
                matches.append(
                    {
                        "version": event.version,
                        "excerpt": content[max(0, index - 150) : index + 600],
                    }
                )
                if len(matches) == 10:
                    break
        index = session.memory_markdown.casefold().find(query.casefold())
        return {
            "version": session.version,
            "memory_excerpt": session.memory_markdown[max(0, index - 150) : index + 600]
            if index >= 0
            else "",
            "journal_matches": matches,
        }

    return await play_call(search, query)


@play_mcp.tool()
async def commit_play_turn(
    operation_id: str,
    expected_version: int,
    player_action: str,
    narration: str,
    memory_markdown: str,
    current_scene: str,
    consequences: list[dict],
) -> dict:
    """Atomically commit one resolved turn. Use a new UUID per logical operation, retain exact arguments on transport retry, and supply get_play_context's version. Consequences accept category (resource/relationship/discovery/narrative) and description ONLY. The server decides approved vs pending. Memory replaces working memory; preserve important prior facts. This does not change the main campaign."""
    return await play_call(
        play.commit_turn,
        operation_id,
        expected_version,
        {
            "player_action": player_action,
            "narration": narration,
            "memory_markdown": memory_markdown,
            "current_scene": current_scene,
            "consequences": consequences,
        },
    )


@play_mcp.tool()
async def finish_play_session(
    operation_id: str, expected_version: int, summary: str
) -> dict:
    """Finish the activity and record a recap. Pending consequences remain pending for human GM review. No more turns after completion. Identical operation retries return their original receipt."""
    return await play_call(play.finish_play, operation_id, expected_version, summary)


# One script performs rate admission and leases together across all workers.
ADMIT = """
local now=tonumber(ARGV[1]); local lease=ARGV[2]
redis.call('ZREMRANGEBYSCORE',KEYS[2],'-inf',now)
local used=redis.call('INCR',KEYS[1]); if used==1 then redis.call('EXPIRE',KEYS[1],60) end
if used>tonumber(ARGV[3]) then return 0 end
if redis.call('ZCARD',KEYS[2])>=tonumber(ARGV[4]) then return 0 end
if redis.call('ZCARD',KEYS[3])>=tonumber(ARGV[5]) then
 redis.call('ZREMRANGEBYSCORE',KEYS[3],'-inf',now)
 if redis.call('ZCARD',KEYS[3])>=tonumber(ARGV[5]) then return 0 end
end
redis.call('ZADD',KEYS[2],now+45,lease); redis.call('EXPIRE',KEYS[2],60)
redis.call('ZADD',KEYS[3],now+45,lease); redis.call('EXPIRE',KEYS[3],60)
return 1
"""


class Gate:
    def __init__(self, app, scoped=False):
        self.app = app
        self.scoped = scoped
        self.redis = Redis.from_url(
            os.environ.get("REDIS_URL", "redis://localhost:6379/0")
        )

    async def __call__(self, scope, receive, send):
        if scope["type"] != "http":
            return await self.app(scope, receive, send)

        async def reject(status, message):
            import json

            await send(
                {
                    "type": "http.response.start",
                    "status": status,
                    "headers": [
                        (b"content-type", b"application/json"),
                        (b"retry-after", b"60"),
                    ],
                }
            )
            await send(
                {
                    "type": "http.response.body",
                    "body": json.dumps({"error": message}).encode(),
                }
            )

        headers = dict(scope["headers"])
        auth = headers.get(b"authorization", b"").decode("latin1")
        if not auth.startswith("Bearer ") or len(auth) != 47:
            return await reject(401, "Bearer token required")
        # Limit invalid/valid token attempts before database authentication.
        import time

        peer = hashlib.sha256(
            str(scope.get("client", ("unknown",))[0]).encode()
        ).hexdigest()
        try:
            attempts = await self.redis.eval(
                "local n=redis.call('INCR',KEYS[1]); if n==1 then redis.call('EXPIRE',KEYS[1],60) end; return n",
                1,
                f"mcp:auth:{peer}:{int(time.time() // 60)}",
            )
        except Exception:
            return await reject(503, "Admission service unavailable")
        if attempts > 600:
            return await reject(429, "Too many authentication attempts")
        key = auth[7:]
        try:
            assignment = None
            if self.scoped:
                assignment = await db_call(play.resolve_access, key)
                authenticated_user = assignment.player if assignment else None
            else:
                credential = await db_call(
                    lambda: (
                        Token.objects.select_related("user")
                        .filter(key=key, user__is_active=True)
                        .first()
                    )
                )
                authenticated_user = credential.user if credential else None
        except DomainError as error:
            if error.status == 503:
                return await reject(503, str(error))
            raise
        if authenticated_user is None:
            return await reject(401, "Invalid token")
        import time

        digest = hashlib.sha256(key.encode()).hexdigest()
        lease = str(uuid.uuid4())
        bucket = int(time.time() // 60)
        keys = [f"mcp:rate:{digest}:{bucket}", f"mcp:leases:{digest}", "mcp:global"]
        try:
            admitted = await self.redis.eval(
                ADMIT,
                3,
                *keys,
                time.time(),
                lease,
                int(os.environ.get("MCP_RATE_LIMIT", "60")),
                int(os.environ.get("MCP_CONCURRENCY", "2")),
                int(os.environ.get("MCP_GLOBAL_CONCURRENCY", "8")),
            )
        except Exception:
            return await reject(503, "Admission service unavailable")
        if not admitted:
            return await reject(429, "MCP limit reached; retry later")
        # Buffer only bounded requests before dispatch, including chunked bodies.
        chunks = []
        size = 0
        response_started = False

        async def tracked_send(message):
            nonlocal response_started
            if message["type"] == "http.response.start":
                response_started = True
            await send(message)

        try:
            async with asyncio.timeout(BODY_TIMEOUT_SECONDS):
                while True:
                    message = await receive()
                    if message["type"] == "http.disconnect":
                        return
                    body = message.get("body", b"")
                    size += len(body)
                    if size > 300_000:
                        return await reject(413, "Request exceeds 300 KB")
                    chunks.append(body)
                    if not message.get("more_body", False):
                        break
            if scope.get("method") == "POST":
                try:

                    def invalid_constant(value):
                        raise ValueError("Nonfinite number")

                    bounded_json(
                        json.loads(
                            b"".join(chunks).decode("utf-8"),
                            parse_constant=invalid_constant,
                        )
                    )
                except (
                    ValueError,
                    UnicodeError,
                    RecursionError,
                    OverflowError,
                    DomainError,
                ):
                    return await reject(400, "Invalid or excessive JSON request")
            sent = False

            async def buffered():
                nonlocal sent
                if not sent:
                    sent = True
                    return {
                        "type": "http.request",
                        "body": b"".join(chunks),
                        "more_body": False,
                    }
                return await receive()

            token = principal.set(authenticated_user)
            scope_token = (
                play_scope.set((assignment.id, digest)) if assignment else None
            )
            try:
                async with asyncio.timeout(TOOL_TIMEOUT_SECONDS):
                    await self.app(scope, buffered, tracked_send)
            except TimeoutError:
                if not response_started:
                    return await reject(504, "MCP execution deadline exceeded")
                # A response already began. Let the server abort the stream rather
                # than write a second HTTP status or pretend a partial JSON is complete.
                raise
            finally:
                principal.reset(token)
                if scope_token is not None:
                    play_scope.reset(scope_token)
        except TimeoutError:
            if not response_started:
                return await reject(408, "Request body deadline exceeded")
            raise
        finally:
            try:
                await self.redis.zrem(keys[1], lease)
                await self.redis.zrem(keys[2], lease)
            except Exception:
                pass  # Expiring leases recover after Redis interruption.


author_app = Gate(mcp.streamable_http_app())
player_app = Gate(play_mcp.streamable_http_app(), scoped=True)


@asynccontextmanager
async def lifespan(app):
    # Mounted Starlette applications do not run their lifespan automatically.
    async with mcp.session_manager.run(), play_mcp.session_manager.run():
        yield


application = Starlette(
    routes=[Mount("/play", app=player_app), Mount("/", app=author_app)],
    lifespan=lifespan,
)
