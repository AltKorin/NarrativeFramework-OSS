"""Create a deployable source archive without local secrets or runtime data."""

import argparse
from pathlib import Path
import tarfile

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("output", type=Path)
args = parser.parse_args()
root = Path(__file__).resolve().parents[2]
excluded = {
    ".venv",
    ".local",
    "node_modules",
    "dist",
    ".prerender",
    "__pycache__",
    "releases",
}
files = [root / name for name in ("LICENSE", "CONTRIBUTING.md", "README.md", "README.pl.md", "CHANGELOG.md", "SECURITY.md", "THIRD_PARTY_NOTICES.md", "BRANDING.md")]
for path in (root / "web").rglob("*"):
    rel = path.relative_to(root)
    if not path.is_file() or path.is_symlink() or excluded.intersection(rel.parts):
        continue
    if (
        path.name in {".env", "db.sqlite3"}
        or path.suffix in {".pyc", ".tsbuildinfo"}
        or (path.name.startswith(".env.") and path.name != ".env.example")
    ):
        continue
    files.append(path)
args.output.parent.mkdir(parents=True, exist_ok=True)
with tarfile.open(args.output, "w:gz") as archive:
    for path in sorted(files):
        archive.add(path, arcname=path.relative_to(root), recursive=False)
print(f"Release source: {args.output} ({len(files)} files)")
