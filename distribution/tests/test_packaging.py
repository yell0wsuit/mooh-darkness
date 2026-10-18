"""Exercise release layouts with fake SDK and platform packaging commands."""
import importlib.util
import os
import plistlib
import shutil
import subprocess
from pathlib import Path

import pytest

ROOT = Path(__file__).resolve().parents[2]


def setup_repository(tmp_path):
    repo = tmp_path / "repo"
    shutil.copytree(ROOT / "distribution", repo / "distribution", ignore=shutil.ignore_patterns("__pycache__"))
    tools = tmp_path / "tools"
    tools.mkdir()
    dotnet = tools / "dotnet"
    dotnet.write_text('''#!/usr/bin/env python3
import sys
from pathlib import Path
out = Path(sys.argv[sys.argv.index('-o') + 1])
for name, data in {
    'Mooh': b'game', 'Mooh.exe': b'game',
    'Mooh.xml': b'docs', 'Mooh.pdb': b'symbols',
    'Mooh.dSYM/Contents/Resources/DWARF/Mooh': b'symbols',
    'assets/levels/001.xml': b'level data',
    'assets/sounds/AirAmb01.ogg': b'original audio',
    'libSDL3.dylib': b'native library',
}.items():
    dest = out / name
    dest.parent.mkdir(parents=True, exist_ok=True)
    dest.write_bytes(data)
''')
    dotnet.chmod(0o755)
    for name in ("codesign", "xattr", "hdiutil"):
        tool = tools / name
        tool.write_text("#!/bin/sh\nexit 0\n")
        tool.chmod(0o755)
    return repo, {**os.environ, "PATH": f"{tools}{os.pathsep}{os.environ['PATH']}", "USE_AOT": "false"}


@pytest.mark.parametrize("version,short,build", [
    ("0.1.0-prerelease+7", "0.1.0", "0.1.7"),
    ("1.2.3", "1.2.3", "1.2.3"),
])
def test_macos_layout_and_numeric_versions(tmp_path, version, short, build):
    if shutil.which("rsync") is None:
        pytest.skip("rsync is required by the macOS bundler")
    repo, env = setup_repository(tmp_path)
    subprocess.run(["sh", str(repo / "distribution/bundle_macos.sh"), version], env=env,
                   check=True, capture_output=True, text=True)
    bundle = repo / "src/Mooh.Desktop/bin/Publish/osx-arm64/Mooh Darkness.app"
    binaries = bundle / "Contents/MacOS"
    assert (binaries / "assets").is_symlink()
    assert (binaries / "assets/levels/001.xml").read_bytes() == b"level data"
    assert (bundle / "Contents/Resources/assets/sounds/AirAmb01.ogg").read_bytes() == b"original audio"
    assert (binaries / "Mooh").read_bytes() == b"game"
    assert (binaries / "libSDL3.dylib").is_file()
    for name in ("Mooh.xml", "Mooh.pdb", "Mooh.dSYM"):
        assert not (binaries / name).exists()
    info = plistlib.loads((bundle / "Contents/Info.plist").read_bytes())
    assert info["CFBundleExecutable"] == "Mooh"
    assert info["CFBundleIdentifier"] == "page.yell0wsuit.mooh.darkness"
    assert info["CFBundleName"] == "Mooh Darkness"
    assert info["CFBundleShortVersionString"] == short
    assert info["CFBundleVersion"] == build
    assert info["MoohDisplayVersion"] == version
    assert "CFBundleIconFile" not in info


def windows_module():
    spec = importlib.util.spec_from_file_location("release_windows", ROOT / "distribution/release_windows.py")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def test_windows_archive_retains_assets_and_sanitizes_version(tmp_path, monkeypatch):
    module = windows_module()
    output = tmp_path / "publish"
    (output / "assets/levels").mkdir(parents=True)
    (output / "Mooh.exe").write_bytes(b"game")
    (output / "Mooh.pdb").write_bytes(b"symbols")
    (output / "assets/levels/001.xml").write_bytes(b"level")
    monkeypatch.setattr(module, "RELEASE_DIR", tmp_path / "releases")
    monkeypatch.setattr(module, "find_7z", lambda: "7z")
    def archive(cmd, **kwargs):
        assert kwargs["cwd"] == output
        files = Path(cmd[-1][1:]).read_text().splitlines()
        assert files == ["Mooh.exe", "assets/levels/001.xml"]
        Path(cmd[-2]).write_bytes(b"archive")
    monkeypatch.setattr(module.subprocess, "run", archive)
    module.package(output, "0.1.0-prerelease+7", "ARM64")
    assert (module.RELEASE_DIR / "Mooh-v0.1.0-prerelease_7-Windows-ARM64.7z").is_file()


def test_windows_publish_passes_complete_version_and_architecture(tmp_path, monkeypatch):
    module = windows_module()
    commands = []
    monkeypatch.setattr(module.subprocess, "run", lambda cmd, **kwargs:
                        commands.append(cmd) or subprocess.CompletedProcess(cmd, 0))
    module.publish(tmp_path, "0.1.0-prerelease+7", True, "win-arm64")
    assert "-p:Version=0.1.0-prerelease+7" in commands[0]
    assert "-p:VersionSuffix=" in commands[0]
    assert "-p:PublishAot=true" in commands[0]
    assert "win-arm64" in commands[0]


def test_linux_appimage_layout(tmp_path):
    repo, env = setup_repository(tmp_path)
    tool = repo / "src/Mooh.Desktop/bin/distribution_tools/appimagetool-x86_64.AppImage"
    tool.parent.mkdir(parents=True)
    tool.write_text('''#!/usr/bin/env python3
import sys
from pathlib import Path
appdir = Path(sys.argv[-2])
assert (appdir / 'usr/bin/Mooh').is_file()
assert not list(appdir.rglob('*.pdb'))
assert (appdir / 'usr/bin/assets/levels/001.xml').read_bytes() == b'level data'
assert (appdir / 'Mooh.svg').is_file()
assert (appdir / '.DirIcon').is_symlink()
assert 'Exec=Mooh' in (appdir / 'Mooh.desktop').read_text()
assert 'contrejour' not in (appdir / 'usr/share/metainfo/page.yell0wsuit.mooh.darkness.metainfo.xml').read_text()
Path(sys.argv[-1]).write_bytes(b'appimage')
''')
    tool.chmod(0o755)
    subprocess.run(["bash", str(repo / "distribution/build_appimage.sh"), "0.1.0-prerelease+7"],
                   env=env, check=True, capture_output=True, text=True)
    assert (repo / "src/Mooh.Desktop/bin/release_github/Mooh-v0.1.0-prerelease_7-x86_64.AppImage").is_file()
