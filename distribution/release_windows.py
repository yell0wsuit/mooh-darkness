#!/usr/bin/env python3
"""Publish and archive the Windows x64 or ARM64 Mooh Darkness release."""

import os
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path

SCRIPT_DIR = Path(__file__).parent
PROJECT_ROOT = SCRIPT_DIR.parent.resolve()
CSPROJ = PROJECT_ROOT / "src" / "Mooh.Desktop" / "Mooh.Desktop.csproj"
RELEASE_DIR = PROJECT_ROOT / "src" / "Mooh.Desktop" / "bin" / "release_github"

ARCHITECTURES = {
    "x64": {"rid": "win-x64", "label": "x64"},
    "arm64": {
        "rid": "win-arm64",
        "label": "ARM64",
    },
}

# The name the game publishes under, from the project's AssemblyName.
GAME_ASSEMBLY = "Mooh"

UNSHIPPED_SUFFIXES = ".pdb"


def publish(out_dir: Path, version: str, use_aot: bool, runtime_id: str) -> None:
    """Publish the game into out_dir, failing the script if the build fails."""
    cmd = [
        "dotnet",
        "publish",
        str(CSPROJ),
        "-c",
        "Release",
        "-f",
        "net10.0",
        "-r",
        runtime_id,
        "--self-contained",
        "true",
        f"-p:Version={version}",
        "-p:VersionSuffix=",
        f"-p:PublishAot={str(use_aot).lower()}",
        "-o",
        str(out_dir),
    ]
    print(f"\n> {' '.join(cmd)}\n")
    result = subprocess.run(cmd, check=False)
    if result.returncode != 0:
        sys.exit(result.returncode)


def is_shipped(output_dir: Path, path: Path) -> bool:
    """Whether a published file belongs in the archive players download."""
    return not any(
        part.endswith(UNSHIPPED_SUFFIXES) for part in path.relative_to(output_dir).parts
    )


def find_7z() -> str:
    """The 7-Zip command line, which only the packaging step needs.

    7-Zip rather than py7zr because py7zr compresses on a single thread: its LZMA goes
    through Python's lzma module, which has no multithreaded encoder, and the release
    archive spent about four minutes there on CI. 7-Zip ships with the GitHub Windows
    runners; a desktop install is usually not on PATH, so its default location is
    checked too.
    """
    for name in ("7z", "7za"):
        found = shutil.which(name)
        if found:
            return found
    for root in filter(
        None, (os.environ.get("ProgramFiles"), os.environ.get("ProgramW6432"))
    ):
        candidate = Path(root) / "7-Zip" / "7z.exe"
        if candidate.is_file():
            return str(candidate)
    print(
        "Required: 7-Zip (https://www.7-zip.org). Install it with: winget install 7zip.7zip",
        file=sys.stderr,
    )
    sys.exit(1)


def package(output_dir: Path, version: str, arch_label: str):
    """Compress the build output into a .7z archive."""
    seven_zip = find_7z()
    RELEASE_DIR.mkdir(parents=True, exist_ok=True)
    # "+" in a prerelease version is not kept in GitHub asset names, so file names use "_".
    archive_name = f"Mooh-v{version.replace('+', '_')}-Windows-{arch_label}.7z"
    archive_path = RELEASE_DIR / archive_name

    published = sorted(f for f in output_dir.rglob("*") if f.is_file())
    files = [f for f in published if is_shipped(output_dir, f)]
    if len(published) != len(files):
        print(
            f"Excluding {len(published) - len(files)} "
            "debug/documentation file(s) from the archive"
        )

    # "7z a" adds to an archive that already exists, so a rebuild would otherwise
    # carry the previous build's files along.
    archive_path.unlink(missing_ok=True)

    print(f"\nPackaging {archive_name}...")
    with tempfile.TemporaryDirectory() as temp:
        list_file = Path(temp) / "files.txt"
        list_file.write_text(
            "\n".join(str(f.relative_to(output_dir)) for f in files) + "\n",
            encoding="utf-8",
        )
        # Relative paths, run from the output directory, so the archive holds the
        # release's layout rather than this machine's.
        subprocess.run(
            [
                seven_zip,
                "a",
                "-t7z",
                "-m0=LZMA2",
                "-mx=9",
                "-mmt=on",
                "-scsUTF-8",
                "-bsp1",
                str(archive_path),
                f"@{list_file}",
            ],
            cwd=output_dir,
            check=True,
        )

    size_mb = archive_path.stat().st_size / (1024 * 1024)
    print(f"Created {archive_path} ({size_mb:.1f} MB)")


def resolve_options() -> tuple[str, bool, str]:
    """Take version, AOT choice, and target architecture from argv or prompts."""
    args = sys.argv[1:]
    use_aot = "--no-aot" not in args
    args = [a for a in args if a != "--no-aot"]

    arch = "x64"
    if "--arch" in args:
        index = args.index("--arch")
        try:
            arch = args[index + 1].lower()
        except IndexError:
            print("--arch requires x64 or arm64", file=sys.stderr)
            sys.exit(2)
        del args[index : index + 2]

    # Accept "arm" as a convenient alias, but Windows/.NET call it ARM64.
    if arch == "arm":
        arch = "arm64"
    if arch not in ARCHITECTURES:
        print("Architecture must be x64 or arm64", file=sys.stderr)
        sys.exit(2)

    if args:
        return args[0], use_aot, arch

    if not sys.stdin.isatty():
        print(
            "Usage: release_windows.py <version> [--arch x64|arm64] [--no-aot]",
            file=sys.stderr,
        )
        sys.exit(1)

    version = input("Version (e.g. 1.0.0): ").strip()
    if not version:
        print("Version is required.", file=sys.stderr)
        sys.exit(1)

    use_aot = input("Use NativeAOT? [Y/n]: ").strip().lower() != "n"
    return version, use_aot, arch


def main():
    """Build and package the selected Windows architecture."""
    version, use_aot, arch = resolve_options()
    # Ask for the archiver before the build rather than after it, so a missing
    # dependency costs a second instead of a full publish.
    find_7z()
    config = ARCHITECTURES[arch]
    runtime_id = config["rid"]
    arch_label = config["label"]
    output_dir = (
        PROJECT_ROOT / "src" / "Mooh.Desktop" / "bin" / "Publish" / runtime_id
    )

    print(f"\nBuilding v{version} for {runtime_id} " f"(NativeAOT: {use_aot})...")

    if output_dir.exists():
        shutil.rmtree(output_dir)

    publish(output_dir, version, use_aot, runtime_id)

    produced = output_dir / f"{GAME_ASSEMBLY}.exe"
    if not produced.is_file():
        print(f"No game executable at {produced}", file=sys.stderr)
        sys.exit(1)

    package(output_dir, version, arch_label)


if __name__ == "__main__":
    main()
