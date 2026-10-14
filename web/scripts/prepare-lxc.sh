#!/bin/sh
# Run as root INSIDE a fresh Debian LXC, never on the Proxmox hypervisor.
# Installs Docker from its official apt repository; does not deploy the application.
set -eu
if [ "$(id -u)" != 0 ]; then
    echo 'Run as root inside the new LXC.' >&2
    exit 1
fi
if command -v pveversion >/dev/null 2>&1; then
    echo 'Refusing to install on the Proxmox hypervisor.' >&2
    exit 1
fi
if [ "$(systemd-detect-virt --container 2>/dev/null || true)" != lxc ]; then
    echo 'This helper is intended for the new LXC guest.' >&2
    exit 1
fi
. /etc/os-release
if [ "$ID" != debian ]; then
    echo 'Use a Debian 12 or 13 guest.' >&2
    exit 1
fi
case "$VERSION_CODENAME" in
    bookworm|trixie) ;;
    *) echo 'Use a Debian 12 or 13 guest.' >&2; exit 1 ;;
esac
apt-get update
apt-get install -y ca-certificates curl openssh-server
install -m 0755 -d /etc/apt/keyrings
curl -fsSL https://download.docker.com/linux/debian/gpg -o /etc/apt/keyrings/docker.asc
chmod a+r /etc/apt/keyrings/docker.asc
cat > /etc/apt/sources.list.d/docker.sources <<SOURCES
Types: deb
URIs: https://download.docker.com/linux/debian
Suites: $VERSION_CODENAME
Components: stable
Architectures: $(dpkg --print-architecture)
Signed-By: /etc/apt/keyrings/docker.asc
SOURCES
apt-get update
apt-get install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin
systemctl enable --now docker ssh
docker run --rm hello-world
docker compose version
hostname -I
