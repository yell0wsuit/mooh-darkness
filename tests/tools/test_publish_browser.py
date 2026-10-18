from pathlib import Path

import sys

sys.path.insert(0, str(Path(__file__).resolve().parents[2] / "tools"))
import publish_browser


def test_defaults():
    args = publish_browser.parse_args([])
    assert args.output == publish_browser.REPO_ROOT / "dist" / "browser"
    assert args.aot is True
    assert args.serve is None


def test_serve_without_port_uses_default():
    assert publish_browser.parse_args(["--serve"]).serve == publish_browser.DEFAULT_PORT
    assert publish_browser.parse_args(["--serve", "9000"]).serve == 9000


def test_publish_command_turns_aot_on_only_when_asked():
    out = Path("/tmp/out")
    aot = publish_browser.publish_command(out, aot=True)
    plain = publish_browser.publish_command(out, aot=False)
    assert aot[:2] == ["dotnet", "publish"]
    assert "-p:RunAOTCompilation=true" in aot
    assert "-p:RunAOTCompilation=true" not in plain
    assert aot[aot.index("-o") + 1] == str(out)
    assert aot[aot.index("-c") + 1] == "Release"


def test_server_knows_the_wasm_and_content_types():
    types = publish_browser.Handler.extensions_map
    assert types[".wasm"] == "application/wasm"
    assert types[".webp"] == "image/webp"
    assert types[".ogg"] == "audio/ogg"
    assert types[".js"] == "text/javascript"


def test_missing_content_stops_before_dotnet(tmp_path, monkeypatch, capsys):
    calls = []
    monkeypatch.setattr(publish_browser, "CONTENT_CATALOG", tmp_path / "assets.json")
    monkeypatch.setattr(publish_browser, "publish_all", lambda *a, **k: calls.append(a))

    assert publish_browser.main([]) == 1
    assert calls == []
    assert "build_browser_content.py" in capsys.readouterr().err


def test_merge_preserves_runtime_hashes_and_versions():
    threaded = {"version": "thread", "assets": [{"url": "index.html", "hash": "shell"}, {"url": "_framework/a.wasm", "hash": "thread-hash"}]}
    single = {"version": "single", "assets": [{"url": "index.html", "hash": "ignored"}, {"url": "_framework/a.wasm", "hash": "single-hash"}]}
    merged, version = publish_browser.merge_manifests(threaded, single)
    assert merged["assets"] == threaded["assets"] + [{"url": "_framework-single/a.wasm", "hash": "single-hash"}]
    assert merged["version"] == version
    assert publish_browser.merge_manifests(threaded, single)[1] == version
    assert publish_browser.merge_manifests(threaded, {**single, "version": "changed"})[1] != version
    assert publish_browser.merge_manifests({**threaded, "version": "changed"}, single)[1] != version


def test_merge_fallback_rewrites_manifest_worker_and_sidecars(tmp_path):
    site, fallback = tmp_path / "site", tmp_path / "single"
    site.mkdir()
    (fallback / "_framework").mkdir(parents=True)
    (fallback / "_framework" / "a.wasm").write_bytes(b"runtime")
    (fallback / "_framework" / "a.wasm.br").write_bytes(b"compressed")
    (site / "service-worker-assets.js").write_text('self.assetsManifest = {"version":"t","assets":[]};')
    (fallback / "service-worker-assets.js").write_text('self.assetsManifest = {"version":"s","assets":[{"url":"_framework/a.wasm","hash":"sha256-fallback"}]};')
    (site / "coi-sw.js").write_text('/* Manifest version: old */\nworker();\n')
    for name in ("coi-sw.js", "service-worker-assets.js"):
        for suffix in (".br", ".gz"):
            (site / (name + suffix)).write_bytes(b"stale")
    publish_browser.merge_fallback(site, fallback)
    merged = publish_browser.read_manifest(site / "service-worker-assets.js")
    assert merged["assets"][0]["url"] == "_framework-single/a.wasm"
    assert (site / "_framework-single/a.wasm").read_bytes() == b"runtime"
    assert (site / "_framework-single/a.wasm.br").read_bytes() == b"compressed"
    worker = (site / "coi-sw.js").read_text()
    assert worker.startswith(f"/* Manifest version: {merged['version']} */")
    assert "old" not in worker
    for name in ("coi-sw.js", "service-worker-assets.js"):
        assert not (site / (name + ".br")).exists()
        assert not (site / (name + ".gz")).exists()


def test_runtime_modes_have_isolated_artifacts_and_mooh_properties():
    threaded = publish_browser.publish_command(Path("/tmp/threaded"), False)
    single = publish_browser.publish_command(Path("/tmp/single"), False, True)
    assert "-p:MoohSingleThreaded=false" in threaded
    assert "-p:MoohSingleThreaded=true" in single
    assert "-p:MoohSkipContent=true" in single
    assert "-p:MoohSkipContent=true" not in threaded
    assert threaded[threaded.index("--artifacts-path") + 1] != single[single.index("--artifacts-path") + 1]
    assert str(publish_browser.PROJECT).endswith("src/Mooh.Browser/Mooh.Browser.csproj")


def test_server_adds_all_cross_origin_isolation_headers():
    headers = {}
    handler = object.__new__(publish_browser.Handler)
    handler.send_header = lambda name, value: headers.update({name: value})
    handler._headers_buffer = []
    handler.request_version = "HTTP/1.1"
    handler.wfile = type("Sink", (), {"write": lambda self, data: None})()
    handler.end_headers()
    assert headers["Cross-Origin-Opener-Policy"] == "same-origin"
    assert headers["Cross-Origin-Embedder-Policy"] == "require-corp"
    assert headers["Cross-Origin-Resource-Policy"] == "same-origin"


def test_failed_publish_leaves_current_site_intact(tmp_path, monkeypatch):
    import pytest

    catalog = tmp_path / "assets.json"
    catalog.write_text("{}")
    output = tmp_path / "site"
    output.mkdir()
    (output / "sentinel").write_bytes(b"current site")
    monkeypatch.setattr(publish_browser, "CONTENT_CATALOG", catalog)

    def fail(publishes):
        raise SystemExit("publish failed")

    monkeypatch.setattr(publish_browser, "publish_all", fail)
    with pytest.raises(SystemExit, match="publish failed"):
        publish_browser.main(["--output", str(output), "--no-aot"])
    assert (output / "sentinel").read_bytes() == b"current site"


def test_parallel_publishes_wait_for_every_process_after_failure(tmp_path, monkeypatch):
    import io
    import pytest

    waited = []

    class Process:
        def __init__(self, command, **kwargs):
            self.name = command[0]
            self.returncode = 1 if self.name == "threaded" else 0
            self.stdout = io.StringIO("build output\n")

        def wait(self):
            waited.append(self.name)

    monkeypatch.setattr(publish_browser.subprocess, "Popen", Process)
    with pytest.raises(SystemExit, match="threaded.*exit 1"):
        publish_browser.publish_all([
            (tmp_path / "threaded", ["threaded"]),
            (tmp_path / "single", ["single"]),
        ])
    assert waited == ["threaded", "single"]
