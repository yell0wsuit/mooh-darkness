from pathlib import Path
import hashlib
import importlib.util
import json
import os
import zipfile

import pytest

MODULE = Path(__file__).resolve().parents[2] / "tools/build_browser_content.py"
spec = importlib.util.spec_from_file_location("build_browser_content", MODULE)
content = importlib.util.module_from_spec(spec)
spec.loader.exec_module(content)


def fixture(root):
    assets = root / "assets"
    for folder in (
        "levels",
        "animations",
        "fonts",
        "map",
        "screens",
        "sounds",
        "sprites",
        "text",
        "music",
    ):
        (assets / folder).mkdir(parents=True)
    files = {
        "levels/manifest.json": json.dumps(
            {"levels": [{"file": "shipped.json"}]}
        ).encode(),
        "levels/shipped.json": b'{"game":"level"}',
        "levels/research-only.json": b'{"archival":"not shipped"}',
        "sprites/SpritePlayer.png": b"original-pixels",
        "sprites/SpritePlayer.json": b"original-table",
        "music/TrackMenu.skm": b"original-module",
        "music/Music.ini": b"original-catalog",
        "screens/.DS_Store": b"finder-metadata",
    }
    for name, data in files.items():
        (assets / name).write_bytes(data)
    music = root / "src/Mooh.Browser/wwwroot/content/music"
    music.mkdir(parents=True)
    (music / "TrackMenu.ogg").write_bytes(b"offline-recording")
    (music / "tracks.json").write_text(
        json.dumps(
            {
                "formatVersion": 4,
                "codec": "opus",
                "bitRate": 192000,
                "tracks": {"menu": {"file": "TrackMenu.ogg"}},
                "sourceSha256": hashlib.sha256(
                    files["music/TrackMenu.skm"] + files["music/Music.ini"]
                )
                .hexdigest()
                .upper(),
            }
        )
    )
    return files


def test_pack_uses_authoritative_level_manifest_and_original_paths(tmp_path):
    files = fixture(tmp_path)
    archive = content.build(tmp_path)
    with zipfile.ZipFile(archive) as bundle:
        names = bundle.namelist()
        assert "assets/levels/shipped.json" in names
        assert "assets/levels/research-only.json" not in names
        assert "assets/screens/.DS_Store" not in names
        assert (
            bundle.read("assets/sprites/SpritePlayer.png")
            == files["sprites/SpritePlayer.png"]
        )
        assert (
            bundle.read("assets/sprites/SpritePlayer.json")
            == files["sprites/SpritePlayer.json"]
        )
        assert "assets/music/TrackMenu.skm" not in names
        assert "assets/music/Music.ini" not in names
        assert (
            archive.parent / "music/TrackMenu.ogg"
        ).read_bytes() == b"offline-recording"
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


def test_pack_rejects_a_renderer_that_did_not_produce_its_outputs(
    tmp_path, monkeypatch
):
    import pytest

    fixture(tmp_path)
    (tmp_path / "src/Mooh.Browser/wwwroot/content/music/TrackMenu.ogg").unlink()
    monkeypatch.setattr(content.shutil, "which", lambda _: "/test/ffmpeg")
    monkeypatch.setattr(content.subprocess, "run", lambda *args, **kwargs: None)
    with pytest.raises(RuntimeError, match="complete matching catalog"):
        content.build(tmp_path)


def test_music_cache_rejects_old_mp3_catalog(tmp_path, monkeypatch):
    import pytest

    fixture(tmp_path)
    path = tmp_path / "src/Mooh.Browser/wwwroot/content/music/tracks.json"
    old = json.loads(path.read_text())
    old["formatVersion"] = 3
    old["tracks"]["menu"]["file"] = "menu.mp3"
    path.write_text(json.dumps(old))
    (path.parent / "menu.mp3").write_bytes(b"old-mp3")
    monkeypatch.setattr(content.shutil, "which", lambda _: "/test/ffmpeg")
    monkeypatch.setattr(content.subprocess, "run", lambda *args, **kwargs: None)
    with pytest.raises(RuntimeError, match="complete matching catalog"):
        content.ensure_music(tmp_path, path.parent)


def test_sfx_are_encoded_mono_opus_and_cache_invalidates_changed_source(
    tmp_path, monkeypatch
):
    source = tmp_path / "assets/sounds/effect.ogg"
    source.parent.mkdir(parents=True)
    source.write_bytes(b"original-stereo-vorbis")
    calls = []

    def encode(command, **kwargs):
        calls.append(command)
        Path(command[-1]).write_bytes(b"mono-opus:" + source.read_bytes())

    monkeypatch.setattr(content.subprocess, "run", encode)
    monkeypatch.setattr(content.shutil, "which", lambda _: "/test/ffmpeg")
    encoded = content.ensure_sfx(tmp_path)
    assert encoded[source].read_bytes() == b"mono-opus:original-stereo-vorbis"
    command = calls[0]
    for option, value in [
        ("-c:a", "libopus"),
        ("-b:a", "96k"),
        ("-ac", "1"),
        ("-ar", "48000"),
    ]:
        assert command[command.index(option) + 1] == value
    assert source.read_bytes() == b"original-stereo-vorbis"
    content.ensure_sfx(tmp_path)
    assert len(calls) == 1
    source.write_bytes(b"changed-vorbis")
    content.ensure_sfx(tmp_path)
    assert len(calls) == 2
    assert encoded[source].read_bytes() == b"mono-opus:changed-vorbis"


def test_pack_uses_encoded_sfx_at_original_paths(tmp_path, monkeypatch):
    fixture(tmp_path)
    source = tmp_path / "assets/sounds/effect.ogg"
    source.write_bytes(b"original-vorbis")
    encoded = tmp_path / "opus.ogg"
    encoded.write_bytes(b"encoded-mono-opus")
    monkeypatch.setattr(content, "ensure_sfx", lambda _: {source: encoded})
    archive = content.build(tmp_path)
    with zipfile.ZipFile(archive) as bundle:
        assert bundle.read("assets/sounds/effect.ogg") == b"encoded-mono-opus"
    assert source.read_bytes() == b"original-vorbis"
    assert json.loads((archive.parent / "assets.json").read_text())["audio"]["sfx"] == {
        "codec": "opus", "bitRate": 96000, "channels": 1}


def test_music_cache_removes_obsolete_mp3_payloads(tmp_path):
    fixture(tmp_path)
    music = tmp_path / "src/Mooh.Browser/wwwroot/content/music"
    obsolete = music / "menu.mp3"
    obsolete.write_bytes(b"old-mp3")
    content.ensure_music(tmp_path, music)
    assert not obsolete.exists()


@pytest.mark.parametrize("source_rate", [44100, 48000])
def test_real_opus_sfx_encoding_preserves_duration_and_is_reproducible(tmp_path, source_rate):
    import pytest
    if not content.shutil.which("ffmpeg") or not content.shutil.which("ffprobe"):
        pytest.skip("ffmpeg and ffprobe required for codec integration")
    source = tmp_path / "assets/sounds/effect.ogg"
    source.parent.mkdir(parents=True)
    content.subprocess.run(["ffmpeg", "-v", "error", "-f", "lavfi", "-i", "sine=frequency=440:duration=0.2",
                           "-ac", "2", "-ar", str(source_rate), "-c:a", "vorbis", "-strict", "experimental", str(source)], check=True)
    original = source.read_bytes()
    output = content.ensure_sfx(tmp_path)[source]
    stream = json.loads(content.subprocess.check_output(["ffprobe", "-v", "error", "-show_entries",
        "stream=codec_name,channels,sample_rate", "-of", "json", str(output)]))["streams"][0]
    assert stream == {"codec_name": "opus", "sample_rate": "48000", "channels": 1}
    def frames(path):
        return len(content.subprocess.check_output(["ffmpeg", "-v", "error", "-i", str(path),
            "-ac", "1", "-ar", "48000", "-f", "f32le", "pipe:1"])) // 4
    assert frames(source) == frames(output)
    first = output.read_bytes()
    output.unlink()
    assert content.ensure_sfx(tmp_path)[source].read_bytes() == first
    assert source.read_bytes() == original
