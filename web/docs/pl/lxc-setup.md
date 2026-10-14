# Przygotowanie LXC na proxmox-hp

[Strona i wdrożenie](website.md)

## 1. Utwórz kontener w panelu Proxmox

Proponowany punkt startowy dla naszej wersji testowej:

| Ustawienie | Wartość |
| --- | --- |
| Hostname | `narrativeframework` |
| System | Debian 13 standard, amd64 (Debian 12 także obsługiwany) |
| Typ | Unprivileged container |
| CPU | 4 rdzenie wirtualne |
| RAM | 6144 MiB |
| Swap | 1024 MiB |
| Dysk | 40 GiB na lokalnym storage |
| Sieć | `vmbr0`, adres LAN stały lub rezerwacja DHCP |
| DNS | działający DNS sieci lokalnej |
| Start at boot | włączony |
| Features | `nesting=1`, `keyctl=1` |

To przydział do uruchomienia i testów, nie deklarowana liczba obsługiwanych użytkowników. Dobierz wolne ID i IP; nie kopiuj adresów zajętych przez inne usługi. Publiczny klucz SSH wklej w kreatorze **Create CT → General → SSH public key**. Użyj publicznego `~/.ssh/id_ed25519.pub` z komputera, z którego ma być wykonywane wdrożenie.

Po utworzeniu zatrzymaj kontener, ustaw **Options → Features → Nesting i keyctl**, a następnie uruchom. Opis funkcji: [dokumentacja Proxmox](https://github.com/proxmox/pve-docs/blob/master/generated/pct.conf.5-opts.adoc). Nie wyłączaj AppArmor ani nie zmieniaj kontenera na privileged. Jeśli test Dockera zgłosi błąd funkcji kernela/AppArmor, zachowaj komunikat do diagnozy konfiguracji hosta.

## 2. Docker i SSH wewnątrz kontenera

Otwórz konsolę **nowego LXC** i zaloguj się jako root. Skrypt [prepare-lxc.sh](../../../web/scripts/prepare-lxc.sh) zawiera instalację z oficjalnego repozytorium apt Dockera i odmawia działania na hypervisorze Proxmox.

Jeśli plik został skopiowany do kontenera:

```sh
sh /root/prepare-lxc.sh
```

Można też wkleić poniższe polecenia bezpośrednio do konsoli świeżego Debiana 13:

```sh
apt-get update
apt-get install -y ca-certificates curl openssh-server
install -m 0755 -d /etc/apt/keyrings
curl -fsSL https://download.docker.com/linux/debian/gpg -o /etc/apt/keyrings/docker.asc
chmod a+r /etc/apt/keyrings/docker.asc

cat > /etc/apt/sources.list.d/docker.sources <<'SOURCES'
Types: deb
URIs: https://download.docker.com/linux/debian
Suites: trixie
Components: stable
Architectures: amd64
Signed-By: /etc/apt/keyrings/docker.asc
SOURCES

apt-get update
apt-get install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin
systemctl enable --now docker ssh
docker run --rm hello-world
docker compose version
hostname -I
```

Instalacja odpowiada [oficjalnej instrukcji Docker dla Debiana](https://docs.docker.com/engine/install/debian/). Dla Debiana 12 w ręcznych poleceniach użyj `Suites: bookworm`; skrypt dobiera wydanie automatycznie.

## 3. Sprawdź dostęp i przekaż adres

Jeśli działa firewall, umożliw SSH z komputera wykonującego wdrożenie do LXC na TCP 22. Kontener potrzebuje także wyjścia do Internetu: DNS, HTTPS i repozytoria apt/Docker.

Z komputera z kluczem:

```sh
ssh root@ADRES_IP_LXC
```

Przekaż adres IP nowego LXC, jego ID i wynik testu `hello-world`. Nie przesyłaj hasła ani klucza prywatnego. Aplikację, nowe sekrety, bazę, konto administracyjne i routing przygotujemy na tej instancji. Przekierowanie publicznego ruchu i DNS ustalimy po sprawdzeniu lokalnego wdrożenia; adres LAN sam w sobie nie wystawia aplikacji w Internecie.
