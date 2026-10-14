"""Refresh downloadable website docs from canonical repository documentation.

Run before building the frontend when docs change. Docker's frontend context is
self-contained, so the resulting files intentionally live under public/.
"""

from pathlib import Path
import shutil

web = Path(__file__).resolve().parents[1]
public = web / "frontend/public"
for name in ("docs", "examples"):
    source = web / name
    for file in source.rglob("*"):
        if file.is_file():
            target = public / name / file.relative_to(source)
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(file, target)
for source, name in (
    (web / "README.md", "README.md"),
    (web / "README.pl.md", "README.pl.md"),
    (web.parent / "CONTRIBUTING.md", "CONTRIBUTING.md"),
    (web.parent / "LICENSE", "docs/LICENSE.txt"),
    (web.parent / "BRANDING.md", "docs/BRANDING.md"),
):
    shutil.copyfile(source, public / name)
print("Website docs synchronized.")
