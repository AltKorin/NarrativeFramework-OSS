"""Run inside isolated API container; real incomplete HTTP body, no model calls."""

import asyncio
import hashlib
import os
import uuid

os.environ.setdefault("DJANGO_SETTINGS_MODULE", "config.settings")
import django

django.setup()
from django.contrib.auth import get_user_model
from rest_framework.authtoken.models import Token
from redis.asyncio import Redis

user = get_user_model().objects.create_user("slow-request-" + uuid.uuid4().hex[:12])
token = Token.objects.create(user=user).key


async def run():
    reader, writer = await asyncio.open_connection("mcp", 8001)
    request = (
        f"POST /mcp HTTP/1.1\r\nHost: mcp:8001\r\nAuthorization: Bearer {token}\r\nContent-Type: application/json\r\nAccept: application/json, text/event-stream\r\nTransfer-Encoding: chunked\r\n\r\n1\r\n{{\r\n"
    ).encode()
    try:
        writer.write(request)
        await writer.drain()
        head = await asyncio.wait_for(reader.readuntil(b"\r\n\r\n"), timeout=13)
        assert head.split(b"\r\n")[0].split()[1] == b"408", head.split(b"\r\n")[0]
        redis = Redis.from_url(os.environ["REDIS_URL"])
        key = "mcp:leases:" + hashlib.sha256(token.encode()).hexdigest()
        for _ in range(20):
            if await redis.zcard(key) == 0:
                break
            await asyncio.sleep(0.01)
        assert await redis.zcard(key) == 0, "Receive deadline leaked an admission lease"
        await redis.aclose()
    finally:
        writer.close()
        try:
            await writer.wait_closed()
        except ConnectionError:
            pass
    print("PASS: real incomplete HTTP request receives 408 and releases MCP admission")


try:
    asyncio.run(run())
finally:
    user.delete()
