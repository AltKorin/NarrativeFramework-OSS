"""Fault injection ONLY for the disposable narrative-adversarial Compose project.

Run from repo root after the isolated stack is healthy. Dependencies are restored
in finally blocks. Credentials are captured in memory and never printed.
"""

import json
import subprocess
import time
import uuid
import httpx

PROJECT = "narrative-adversarial"
BASE = "http://127.0.0.1:18080"
compose = ["docker", "compose", "-p", PROJECT, "-f", "web/compose.yaml"]


def command(*args, input=None):
    return subprocess.run(
        compose + list(args), input=input, text=True, capture_output=True, check=True
    )


def python(code):
    return command("exec", "-T", "api", "python", input=code).stdout


def wait_for(service):
    for _ in range(40):
        args = ["exec", "-T", service]
        args += (
            ["redis-cli", "ping"]
            if service == "redis"
            else ["sh", "-c", 'pg_isready -U "$POSTGRES_USER" -d "$POSTGRES_DB"']
        )
        result = subprocess.run(compose + args, capture_output=True)
        if result.returncode == 0:
            return
        time.sleep(0.25)
    raise AssertionError(service + " did not recover")


name = "fault-" + uuid.uuid4().hex[:12]
setup = f"""import os,json
os.environ.setdefault('DJANGO_SETTINGS_MODULE','config.settings')
import django;django.setup()
from django.contrib.auth import get_user_model
from django.test import Client
from rest_framework.authtoken.models import Token
u=get_user_model().objects.create_user({name!r})
c=Client();c.force_login(u)
print(json.dumps({{"cookie":c.cookies['sessionid'].value,"token":Token.objects.create(user=u).key}}))
"""
fixture = json.loads(python(setup))
headers = {
    "Authorization": "Bearer " + fixture["token"],
    "Content-Type": "application/json",
    "Accept": "application/json, text/event-stream",
}
initialize = {
    "jsonrpc": "2.0",
    "id": 1,
    "method": "initialize",
    "params": {
        "protocolVersion": "2025-11-25",
        "capabilities": {},
        "clientInfo": {"name": "adversarial-fixture", "version": "1"},
    },
}
try:
    with httpx.Client(base_url=BASE, timeout=10) as client:
        client.cookies.set("sessionid", fixture["cookie"])
        session = client.get("/api/session/")
        assert session.status_code == 200
        client.headers["X-CSRFToken"] = session.json()["csrf"]
        assert client.get("/api/scenarios/").status_code == 200
        assert client.post("/mcp", headers=headers, json=initialize).status_code == 200
        document = client.get("/api/demo/?lang=en").json()["document"]
        attempted_id = str(uuid.uuid4())
        try:
            command("stop", "redis")
            assert client.get("/").status_code == 200, (
                "Static UI should remain available"
            )
            assert client.get("/api/health/").status_code == 200, (
                "DB health should not depend on throttle cache"
            )
            assert client.get("/api/scenarios/").status_code == 503
            assert (
                client.post(
                    "/api/scenarios/",
                    json={
                        "id": attempted_id,
                        "title": "Must not write",
                        "document": document,
                    },
                ).status_code
                == 503
            )
            assert (
                client.post("/mcp", headers=headers, json=initialize).status_code == 503
            )
            print(
                "PASS: Redis outage fails closed with 503; static UI and DB probe stay available"
            )
        finally:
            command("start", "redis")
            wait_for("redis")
        response = client.get("/api/scenarios/")
        assert response.status_code == 200
        assert all(s["id"] != attempted_id for s in response.json()), (
            "Outage rejected write must not appear later"
        )
        assert client.post("/mcp", headers=headers, json=initialize).status_code == 200
        try:
            command("stop", "db")
            assert client.get("/").status_code == 200
            assert client.get("/api/health/").status_code == 503
            assert client.get("/api/scenarios/").status_code == 503
            assert (
                client.post("/mcp", headers=headers, json=initialize).status_code == 503
            )
            print(
                "PASS: PostgreSQL outage returns 503 without exposing internal connection errors"
            )
        finally:
            command("start", "db")
            wait_for("db")
        assert client.get("/api/health/").status_code == 200
        assert client.get("/api/scenarios/").status_code == 200
        assert client.post("/mcp", headers=headers, json=initialize).status_code == 200
        print("PASS: existing API/MCP workers recover after both dependencies restart")
finally:
    command("start", "db", "redis")
    wait_for("db")
    wait_for("redis")
    cleanup = f"""import os
os.environ.setdefault('DJANGO_SETTINGS_MODULE','config.settings')
import django;django.setup()
from django.contrib.auth import get_user_model
from django.contrib.sessions.models import Session
get_user_model().objects.filter(username={name!r}).delete()
Session.objects.filter(session_key={fixture["cookie"]!r}).delete()
"""
    python(cleanup)
