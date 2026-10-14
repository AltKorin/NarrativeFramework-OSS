"""Dependency-free publication checks; reports locations, never secret values.

This catches common mistakes, not all possible credentials or security defects.
"""

import json
import re
import subprocess
from pathlib import Path

root = Path(__file__).resolve().parents[1]
paths = subprocess.check_output(
    ["git", "ls-files", "--cached", "--others", "--exclude-standard", "-z"], cwd=root
).split(b"\0")
patterns = {
    "private key": rb"-----BEGIN (?:RSA |OPENSSH |EC |DSA )?PRIVATE KEY-----",
    "provider token": rb"\b(?:sk-(?:proj-)?[A-Za-z0-9_-]{20,}|gh[pousr]_[A-Za-z0-9]{25,}|github_pat_[A-Za-z0-9_]{30,}|AKIA[A-Z0-9]{16})\b",
}
errors = []
for raw in set(paths):
    if not raw:
        continue
    path = Path(raw.decode())
    if (
        path.name == ".env"
        or (path.name.startswith(".env.") and path.name != ".env.example")
        or path.name in {"OpenAISettings.asset", "OpenAISettings.asset.meta"}
        or path.suffix in {".pem", ".key", ".p12", ".pfx", ".sqlite3", ".dump", ".sql"}
        or {".local", ".venv", "node_modules", "__pycache__"}.intersection(path.parts)
    ):
        errors.append(f"Local/credential file included: {path}")
    data = (root / path).read_bytes()
    if len(data) > 50_000_000:
        errors.append(f"Oversized source file: {path}")
    if b"\0" in data[:8192]:
        continue
    for label, pattern in patterns.items():
        for match in re.finditer(pattern, data):
            line = data[: match.start()].count(b"\n") + 1
            errors.append(f"Possible {label}: {path}:{line}")

package = json.loads((root / "web/frontend/package.json").read_text())
lock = json.loads((root / "web/frontend/package-lock.json").read_text())
assert package["version"] == lock["version"] == lock["packages"][""]["version"]
for language in ("en", "pl"):
    for name in ("campaign-wiki.md", "campaign-sharing-plan.md"):
        path = root / "web/docs" / language / name
        mirror = root / "web/frontend/public/docs" / language / name
        if path.read_bytes() != mirror.read_bytes():
            errors.append(f"Stale public documentation: {mirror.relative_to(root)}")
if errors:
    raise SystemExit("\n".join(errors))
print(f"Publication checks passed ({len(set(paths)) - 1} files; version {package['version']}).")
