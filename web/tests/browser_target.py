"""Optional process-local DNS pin for testing a newly propagated hostname.

TLS still verifies the actual public hostname. Never changes system DNS/hosts.
"""

import ipaddress
import os
import socket
from urllib.parse import urlparse


def browser_args():
    address = os.environ.get("SITE_TEST_RESOLVE_IP")
    if not address:
        return []
    ipaddress.ip_address(address)
    hostname = urlparse(os.environ["SITE_TEST_BASE"]).hostname
    original = socket.getaddrinfo

    def resolve(host, port, *args, **kwargs):
        if host in (hostname, hostname.encode()):
            host = address
        return original(host, port, *args, **kwargs)

    # Python MCP requests and Chromium have independent resolvers.
    socket.getaddrinfo = resolve
    return [f"--host-resolver-rules=MAP {hostname} {address}"]
