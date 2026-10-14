#!/bin/sh
# Run inside the deployment LXC from its web/ directory, after configuring
# TRUSTED_PROXY_CIDRS to include the Docker gateway used by local cloudflared.
set -eu

docker compose exec -T api python <<'PY'
import os
os.environ.setdefault('DJANGO_SETTINGS_MODULE', 'config.settings')
import django
django.setup()
from django.core.cache import cache
cache.delete_many(['throttle_anon_203.0.113.80', 'throttle_anon_198.51.100.81', 'throttle_anon_198.51.100.82'])
PY

# Local tunnel path: rightmost untrusted client must win over a spoofed prefix.
curl -fsS -H 'X-Forwarded-For: 203.0.113.80, 198.51.100.81' \
  http://127.0.0.1/api/demo/ >/dev/null

docker compose exec -T api python <<'PY'
import os
os.environ.setdefault('DJANGO_SETTINGS_MODULE', 'config.settings')
import django
django.setup()
import httpx
from django.core.cache import cache
keys = ['throttle_anon_203.0.113.80', 'throttle_anon_198.51.100.81', 'throttle_anon_198.51.100.82']
try:
    assert cache.get(keys[1]), 'Trusted local tunnel did not preserve the real client IP'
    assert cache.get(keys[0]) is None, 'Spoofed XFF prefix won over the real client'
    response = httpx.get('http://web/api/demo/', headers={'Host': 'localhost', 'X-Forwarded-For': '198.51.100.82'}, timeout=10)
    response.raise_for_status()
    assert cache.get(keys[2]) is None, 'Untrusted container peer spoofed its client IP'
    print('PASS: trusted local tunnel, strict XFF parsing and untrusted-peer spoof rejection')
finally:
    cache.delete_many(keys)
PY
