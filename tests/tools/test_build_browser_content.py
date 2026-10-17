from pathlib import Path
import hashlib
import importlib.util
import json
import os
import zipfile

MODULE = Path(__file__).resolve().parents[2] / "tools/build_browser_content.py"
spec = importlib.util.spec_from_file_location("build_browser_content", MODULE)
content = importlib.util.module_from_spec(spec)
spec.loader.exec_module(content)


def fixture(root):
    assets = root / "assets"
    for folder in ("levels", "animations", "fonts", "map", "screens", "sounds", "sprites", "text", "music"):
        (assets / folder).mkdir(parents=True)
    files = {
        "levels/manifest.json": json.dumps({"levels": [{"file": "shipped.json"}]}).encode(),
        "levels/shipped.json": b'{"game":"level"}',
        "levels/research-only.json": b'{"archival":"not shipped"}',
        "sprites/SpritePlayer.png": b"original-pixels",
        "sprites/SpritePlayer.seq": b"original-table",
        "music/TrackMenu.skm": b"original-module",
        "music/Music.ini": b"original-catalog",
        "screens/.DS_Store": b"finder-metadata",
    }
    for name, data in files.items():
        (assets / name).write_bytes(data)
    music = root / "src/Mooh.Browser/wwwroot/content/music"
    music.mkdir(parents=True)
    (music / "TrackMenu.ogg").write_bytes(b"offline-recording")
    (music / "tracks.json").write_text(json.dumps({"formatVersion": 3, "tracks": {"menu": {"file": "TrackMenu.ogg"}}, "sourceSha256": hashlib.sha256(
        files["music/TrackMenu.skm"] + files["music/Music.ini"]).hexdigest().upper()}))
    return files


def test_pack_uses_authoritative_level_manifest_and_original_paths(tmp_path):
    files = fixture(tmp_path)
    archive = content.build(tmp_path)
    with zipfile.ZipFile(archive) as bundle:
        names = bundle.namelist()
        assert "assets/levels/shipped.json" in names
        assert "assets/levels/research-only.json" not in names
        assert "assets/screens/.DS_Store" not in names
        assert bundle.read("assets/sprites/SpritePlayer.png") == files["sprites/SpritePlayer.png"]
        assert bundle.read("assets/sprites/SpritePlayer.seq") == files["sprites/SpritePlayer.seq"]
        assert "assets/music/TrackMenu.skm" not in names
        assert "assets/music/Music.ini" not in names
        assert (archive.parent / "music/TrackMenu.ogg").read_bytes() == b"offline-recording"
        assert bundle.testzip() is None


def test_pack_is_reproducible_and_catalog_matches_payload(tmp_path):
    fixture(tmp_path)
    archive = content.build(tmp_path)
    original = archive.read_bytes()
    os.utime(tmp_path / "assets/sprites/SpritePlayer.png", (123456789, 123456789))
    assert content.build(tmp_path).read_bytes() == original
    catalog = json.loads((archive.parent / "assets.json").read_text())
    assert catalog["sha256"] == hashlib.sha256(original).hexdigest()
    assert catalog["archive"] == "assets.zip"
    with zipfile.ZipFile(archive) as bundle:
        assert catalog["files"] == bundle.namelist()


def test_pack_rejects_a_renderer_that_did_not_produce_its_outputs(tmp_path, monkeypatch):
    import pytest
    fixture(tmp_path)
    (tmp_path / "src/Mooh.Browser/wwwroot/content/music/TrackMenu.ogg").unlink()
    monkeypatch.setattr(content.shutil, "which", lambda _: "/test/ffmpeg")
    monkeypatch.setattr(content.subprocess, "run", lambda *args, **kwargs: None)
    with pytest.raises(RuntimeError, match="complete matching catalog"):
        content.build(tmp_path)
