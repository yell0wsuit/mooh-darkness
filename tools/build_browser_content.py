#!/usr/bin/env python3
"""Package the existing runtime assets for Mooh's browser virtual filesystem."""

from pathlib import Path
import hashlib
import json
import zipfile
import shutil
import subprocess
from concurrent.futures import ThreadPoolExecutor

ROOT = Path(__file__).resolve().parents[1]


def runtime_files(root: Path) -> list[Path]:
    assets = root / "assets"
    files = []
    for folder in (
        "animations",
        "fonts",
        "map",
        "screens",
        "sounds",
        "sprites",
        "text",
    ):
        files.extend(
            path
            for path in (assets / folder).rglob("*")
            if path.is_file() and path.name != ".DS_Store"
        )
    manifest = assets / "levels" / "manifest.json"
    files.append(manifest)
    files.extend(
        manifest.parent / level["file"]
        for level in json.loads(manifest.read_text())["levels"]
    )
    return sorted(set(files))


def ensure_music(root: Path, destination: Path) -> None:
    source = root / "assets/music"
    digest = (
        hashlib.sha256(
            (source / "TrackMenu.json").read_bytes()
            + (source / "Music.json").read_bytes()
            + b"".join(path.read_bytes() for path in sorted((source / "samples").glob("*.ogg")))
        )
        .hexdigest()
        .upper()
    )
    manifest = destination / "tracks.json"

    def ready():
        if not manifest.exists():
            return False
        data = json.loads(manifest.read_text())
        return (
            data.get("formatVersion") == 4
            and data.get("codec") == "opus"
            and data.get("bitRate") == 192000
            and data.get("sourceSha256") == digest
            and bool(data.get("tracks"))
            and all(
                (destination / entry["file"]).is_file()
                for entry in data["tracks"].values()
            )
        )

    if ready():
        for obsolete in destination.glob("*.mp3"):
            obsolete.unlink()
        return
    if not shutil.which("ffmpeg"):
        raise RuntimeError(
            "Install ffmpeg to render browser music offline before packaging."
        )
    project = str(root / "tools/Mooh.MusicRenderer")
    # Serialize MSBuild to avoid macOS task-host stalls; run exactly the freshly built tool.
    subprocess.run(
        ["dotnet", "build", project, "-c", "Release", "--disable-build-servers",
         "-m:1", "-p:NuGetAudit=false"],
        check=True,
    )
    subprocess.run(
        ["dotnet", "run", "--no-build", "--no-restore", "-c", "Release",
         "--project", project, "--", str(source), str(destination)],
        check=True,
    )
    if not ready():
        raise RuntimeError(
            "Offline music rendering did not produce a complete matching catalog."
        )
    for obsolete in destination.glob("*.mp3"):
        obsolete.unlink()


def ensure_sfx(root: Path) -> dict[Path, Path]:
    source = root / "assets/sounds"
    files = sorted(source.rglob("*.ogg"))
    if not files:
        return {}
    if not shutil.which("ffmpeg"):
        raise RuntimeError(
            "Install ffmpeg with libopus to encode browser sound effects."
        )
    destination = root / "artifacts/browser-audio/sfx"
    destination.mkdir(parents=True, exist_ok=True)
    manifest = destination / "manifest.json"
    settings = {
        "codec": "opus",
        "bitRate": 96000,
        "channels": 1,
        "sampleRate": 48000,
        "version": 2,
    }
    cached = json.loads(manifest.read_text()) if manifest.exists() else {}
    entries = cached.get("files", {}) if cached.get("settings") == settings else {}

    def encode(path):
        relative = path.relative_to(source).as_posix()
        output = destination / relative
        digest = hashlib.sha256(path.read_bytes()).hexdigest()
        if entries.get(relative) != digest or not output.is_file():
            output.parent.mkdir(parents=True, exist_ok=True)
            temporary = output.with_suffix(".tmp.ogg")
            subprocess.run(
                [
                    "ffmpeg",
                    "-hide_banner",
                    "-loglevel",
                    "error",
                    "-y",
                    "-i",
                    str(path),
                    "-map_metadata",
                    "-1",
                    "-c:a",
                    "libopus",
                    "-b:a",
                    "96k",
                    "-vbr",
                    "on",
                    "-ac",
                    "1",
                    "-ar",
                    "48000",
                    "-af",
                    "asetpts=N/SR/TB",
                    "-fflags",
                    "+bitexact",
                    "-flags:a",
                    "+bitexact",
                    "-f",
                    "ogg",
                    str(temporary),
                ],
                check=True,
                stdin=subprocess.DEVNULL,
            )
            temporary.replace(output)
        return path, output, relative, digest

    with ThreadPoolExecutor(max_workers=4) as workers:
        results = list(workers.map(encode, files))
    manifest.write_text(
        json.dumps(
            {
                "settings": settings,
                "files": {relative: digest for _, _, relative, digest in results},
            },
            indent=2,
        )
        + "\n"
    )
    return {path: output for path, output, _, _ in results}


def build(root: Path = ROOT) -> Path:
    destination = root / "src/Mooh.Browser/wwwroot/content"
    destination.mkdir(parents=True, exist_ok=True)
    ensure_music(root, destination / "music")
    effects = ensure_sfx(root)
    archive = destination / "assets.zip"
    files = runtime_files(root)
    with zipfile.ZipFile(
        archive, "w", compression=zipfile.ZIP_DEFLATED, compresslevel=6
    ) as bundle:
        for path in files:
            info = zipfile.ZipInfo(
                path.relative_to(root).as_posix(), (2026, 1, 1, 0, 0, 0)
            )
            info.compress_type = zipfile.ZIP_DEFLATED
            bundle.writestr(info, effects.get(path, path).read_bytes())
    digest = hashlib.sha256(archive.read_bytes()).hexdigest()
    (destination / "assets.json").write_text(
        json.dumps(
            {
                "archive": "assets.zip",
                "sha256": digest,
                "files": [path.relative_to(root).as_posix() for path in files],
                "audio": {
                    "music": {"codec": "opus", "bitRate": 192000, "channels": 2},
                    "sfx": {"codec": "opus", "bitRate": 96000, "channels": 1},
                },
            },
            indent=2,
        )
        + "\n"
    )
    print(
        f"Packaged {len(files)} files, {archive.stat().st_size / 1024 / 1024:.1f} MiB: {archive}"
    )
    return archive


if __name__ == "__main__":
    build()
