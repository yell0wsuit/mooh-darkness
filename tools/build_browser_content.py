#!/usr/bin/env python3
"""Package the existing runtime assets for Mooh's browser virtual filesystem."""
from pathlib import Path
import hashlib
import json
import zipfile
import shutil
import subprocess

ROOT = Path(__file__).resolve().parents[1]


def runtime_files(root: Path) -> list[Path]:
    assets = root / "assets"
    files = []
    for folder in ("animations", "fonts", "map", "screens", "sounds", "sprites", "text"):
        files.extend(path for path in (assets / folder).rglob("*") if path.is_file() and path.name != ".DS_Store")
    manifest = assets / "levels" / "manifest.json"
    files.append(manifest)
    files.extend(manifest.parent / level["file"] for level in json.loads(manifest.read_text())["levels"])
    return sorted(set(files))


def ensure_music(root: Path, destination: Path) -> None:
    source = root / "assets/music"
    digest = hashlib.sha256((source / "TrackMenu.skm").read_bytes() + (source / "Music.ini").read_bytes()).hexdigest().upper()
    manifest = destination / "tracks.json"

    def ready():
        if not manifest.exists():
            return False
        data = json.loads(manifest.read_text())
        return (data.get("formatVersion") == 3 and data.get("sourceSha256") == digest
                and bool(data.get("tracks")) and all(
                    (destination / entry["file"]).is_file() for entry in data["tracks"].values()))

    if ready():
        return
    if not shutil.which("ffmpeg"):
        raise RuntimeError("Install ffmpeg to render browser music offline before packaging.")
    subprocess.run(["dotnet", "run", "--disable-build-servers", "-p:NuGetAudit=false", "--project", str(root / "tools/Mooh.MusicRenderer"),
                    "--", str(source), str(destination)], check=True)
    if not ready():
        raise RuntimeError("Offline music rendering did not produce a complete matching catalog.")


def build(root: Path = ROOT) -> Path:
    destination = root / "src/Mooh.Browser/wwwroot/content"
    destination.mkdir(parents=True, exist_ok=True)
    ensure_music(root, destination / "music")
    archive = destination / "assets.zip"
    files = runtime_files(root)
    with zipfile.ZipFile(archive, "w", compression=zipfile.ZIP_DEFLATED, compresslevel=6) as bundle:
        for path in files:
            info = zipfile.ZipInfo(path.relative_to(root).as_posix(), (2026, 1, 1, 0, 0, 0))
            info.compress_type = zipfile.ZIP_DEFLATED
            bundle.writestr(info, path.read_bytes())
    digest = hashlib.sha256(archive.read_bytes()).hexdigest()
    (destination / "assets.json").write_text(json.dumps({"archive": "assets.zip", "sha256": digest,
        "files": [path.relative_to(root).as_posix() for path in files]}, indent=2) + "\n")
    print(f"Packaged {len(files)} files, {archive.stat().st_size / 1024 / 1024:.1f} MiB: {archive}")
    return archive


if __name__ == "__main__":
    build()
