import { test } from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";

// Load as an ES module even without package.json type metadata in this .NET repo.
const source = readFileSync(new URL("../../src/Mooh.Browser/wwwroot/runtime-mode.js", import.meta.url), "utf8");
const { probeEnvironment, selectRuntime, parseModeQuery, THREADED_RUNTIME, SINGLE_RUNTIME } =
    await import(`data:text/javascript;base64,${Buffer.from(source).toString("base64")}`);
const supported = {
    isolated: true,
    sharedMemory: true,
    offscreenCanvas: true,
    workerGraphics: true,
    localGraphics: true,
};

test("isolated capable browsers select threaded runtime", () => {
    assert.deepEqual(selectRuntime(supported), {
        mode: "threaded", runtime: THREADED_RUNTIME, reason: null,
    });
});

for (const capability of ["isolated", "sharedMemory", "offscreenCanvas", "workerGraphics"]) {
    test(`missing ${capability} falls back before transferring canvas`, () => {
        assert.deepEqual(selectRuntime({ ...supported, [capability]: false }), {
            mode: "single", runtime: SINGLE_RUNTIME, reason: null,
        });
    });
}

test("query overrides accept trimmed mode names without forcing unsupported threads", () => {
    assert.deepEqual(parseModeQuery("?mode=%20MULTI%20"), { mode: "threaded", raw: " MULTI " });
    assert.deepEqual(parseModeQuery("?mode=single"), { mode: "single", raw: "single" });
    assert.deepEqual(parseModeQuery("?mode=invalid"), { mode: null, raw: "invalid" });
    assert.deepEqual(parseModeQuery(), { mode: null, raw: null });
    assert.equal(selectRuntime(supported, "single").runtime, SINGLE_RUNTIME);
    assert.equal(selectRuntime({ ...supported, isolated: false }, "threaded").runtime, SINGLE_RUNTIME);
});

test("unavailable graphics produce a useful unsupported result", () => {
    const result = selectRuntime({ ...supported, workerGraphics: false, localGraphics: false });
    assert.equal(result.mode, "unsupported");
    assert.equal(result.runtime, null);
    assert.match(result.reason, /cannot draw/);
});

test("probe checks both WebGL2 paths and releases temporary contexts", () => {
    let released = 0;
    const context = { getExtension: () => ({ loseContext: () => released++ }) };
    class Canvas {
        getContext(kind) { assert.equal(kind, "webgl2"); return context; }
        transferControlToOffscreen() {}
    }
    const actual = probeEnvironment({
        crossOriginIsolated: true,
        SharedArrayBuffer,
        OffscreenCanvas: Canvas,
        HTMLCanvasElement: Canvas,
        document: { createElement: () => new Canvas() },
    });
    assert.deepEqual(actual, supported);
    assert.equal(released, 2);
});

test("partial and throwing browser APIs are handled as unavailable", () => {
    assert.deepEqual(probeEnvironment({}), Object.fromEntries(Object.keys(supported).map(key => [key, false])));
    const environment = probeEnvironment({
        OffscreenCanvas: class { constructor() { throw new Error("no worker graphics"); } },
        document: { createElement() { throw new Error("disabled graphics"); } },
    });
    assert.equal(environment.workerGraphics, false);
    assert.equal(environment.localGraphics, false);
});
