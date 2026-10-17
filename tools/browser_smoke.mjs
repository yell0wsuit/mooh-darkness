#!/usr/bin/env node
// Runs against a separately launched Chromium test profile via its DevTools port.
// Usage: node tools/browser_smoke.mjs [url] [debug-port] [capture-directory]
import { mkdir, writeFile } from "node:fs/promises";
import assert from "node:assert/strict";

const url = process.argv[2] ?? "http://127.0.0.1:8765/?mode=single&screen=main";
const port = process.argv[3] ?? "9229";
const output = process.argv[4] ?? "/tmp/mooh-browser-smoke";
const version = await (await fetch(`http://127.0.0.1:${port}/json/version`)).json();
const socket = new WebSocket(version.webSocketDebuggerUrl);
await new Promise((resolve, reject) => { socket.onopen = resolve; socket.onerror = reject; });
let serial = 0;
let session;
const pending = new Map();
const failures = [];
socket.onclose = event => {
    const error = new Error(`Browser connection closed: ${event.code} ${event.reason}`);
    for (const job of pending.values()) job.reject(error);
    pending.clear();
};
socket.onerror = event => console.error("Browser connection error", event.message);
socket.onmessage = event => {
    const message = JSON.parse(event.data);
    if (message.id) {
        const job = pending.get(message.id);
        if (!job) return;
        pending.delete(message.id);
        if (message.error) job.reject(new Error(JSON.stringify(message.error)));
        else job.resolve(message.result);
    } else if (message.sessionId === session) {
        if (message.method === "Runtime.consoleAPICalled") {
            const text = message.params.args.map(value => value.value ?? value.description).join(" ");
            console.log(`[browser ${message.params.type}] ${text}`);
        }
        if (message.method === "Runtime.exceptionThrown") {
            const text = message.params.exceptionDetails.exception?.description ?? message.params.exceptionDetails.text;
            failures.push(text);
            console.error(text);
        }
    }
};
function send(method, params = {}, target = session) {
    const id = ++serial;
    return new Promise((resolve, reject) => {
        const timeout = setTimeout(() => {
            pending.delete(id);
            reject(new Error(`Browser command timed out: ${method}`));
        }, 30000);
        pending.set(id, {
            resolve: value => { clearTimeout(timeout); resolve(value); },
            reject: error => { clearTimeout(timeout); reject(error); },
        });
        socket.send(JSON.stringify({ id, method, params, ...(target ? { sessionId: target } : {}) }));
    });
}
async function evaluate(expression) {
    const result = await send("Runtime.evaluate", { expression, returnByValue: true, awaitPromise: true });
    if (result.exceptionDetails) throw new Error(result.exceptionDetails.exception?.description ?? result.exceptionDetails.text);
    return result.result.value;
}
async function until(expression, timeout = 120000) {
    const began = Date.now();
    while (Date.now() - began < timeout) {
        const value = await evaluate(expression);
        if (value) return value;
        if (failures.length) throw new Error(failures.join("\n"));
        await new Promise(resolve => setTimeout(resolve, 250));
    }
    throw new Error(`Timed out: ${expression}; state=${await evaluate("JSON.stringify({screen:document.getElementById('game')?.dataset.screen,progress:document.getElementById('splash-progress')?.textContent})")}`);
}
async function key(key, code, number) {
    await send("Input.dispatchKeyEvent", { type: "keyDown", key, code, windowsVirtualKeyCode: number });
    await send("Input.dispatchKeyEvent", { type: "keyUp", key, code, windowsVirtualKeyCode: number });
}
async function capture(name) {
    const size = await evaluate("({width:innerWidth,height:innerHeight})");
    const { data } = await send("Page.captureScreenshot", { format: "png",
        clip: { x: 0, y: 0, width: size.width, height: size.height, scale: Math.min(1, 1280 / size.width) } });
    await writeFile(`${output}/${name}.png`, Buffer.from(data, "base64"));
}
const { targetId } = await send("Target.createTarget", { url: "about:blank" }, null);
session = (await send("Target.attachToTarget", { targetId, flatten: true }, null)).sessionId;
await send("Page.enable");
await send("Runtime.enable");
await send("Network.enable");
if (process.env.MOOH_DISABLE_COI === "1") {
    // Worker-script fetches run outside this page's Network domain. Simulate a
    // host/browser without the service-worker isolation shim instead.
    await send("Network.setBypassServiceWorker", { bypass: true });
    await send("Page.addScriptToEvaluateOnNewDocument", { source:
        "navigator.serviceWorker.register = () => Promise.reject(new Error('Isolation registration disabled for fallback smoke'));" });
}
await send("Page.addScriptToEvaluateOnNewDocument", { source: `
    const OriginalAudioContext = globalThis.AudioContext;
    const connect = AudioNode.prototype.connect;
    AudioNode.prototype.connect = function (destination, ...rest) {
        if (destination === this.context.destination && this.context.moohMonitor && this !== this.context.moohMonitor)
            return connect.call(this, this.context.moohMonitor, ...rest);
        return connect.call(this, destination, ...rest);
    };
    globalThis.AudioContext = class extends OriginalAudioContext {
        constructor(options) {
            super(options);
            this.moohMonitor = this.createAnalyser();
            this.moohMonitor.fftSize = 2048;
            this.moohMonitor.connect(this.destination);
            globalThis.moohSmokeAudio = this;
        }
        createBufferSource() {
            const source = super.createBufferSource();
            const start = source.start.bind(source);
            source.start = (...args) => {
                if (source.loop) {
                    globalThis.moohSmokeMusic = { duration: source.buffer.duration,
                        loopStart: source.loopStart, loopEnd: source.loopEnd, offset: args[1] };
                }
                return start(...args);
            };
            return source;
        }
    };
` });
await send("Page.bringToFront");
await send("Emulation.setDeviceMetricsOverride", { width: 1280, height: 720, deviceScaleFactor: 1, mobile: false });
await mkdir(output, { recursive: true });
try {
    await send("Page.navigate", { url });
    await until("document.getElementById('start') && !document.getElementById('start').hidden");
    const ready = await evaluate("({isolated:crossOriginIsolated,progress:document.getElementById('splash-progress').textContent})");
    console.log("ready", ready);
    if (process.env.MOOH_DISABLE_COI === "1") assert.equal(ready.isolated, false);
    await evaluate("document.getElementById('start').click()");
    await until("document.getElementById('game')?.dataset.screen === 'Main'");
    await new Promise(resolve => setTimeout(resolve, 600));
    await capture("main");
    await key("Enter", "Enter", 13);
    await until("document.getElementById('game')?.dataset.screen === 'Map'");
    await key("Enter", "Enter", 13);
    await until("document.getElementById('game')?.dataset.screen === 'PreLevel'");
    // Wait for panel entry in game time; under load the fixed-step host intentionally clamps stalls.
    for (let attempt = 0; attempt < 12; attempt++) {
        await new Promise(resolve => setTimeout(resolve, 1000));
        await key("Enter", "Enter", 13);
        await new Promise(resolve => setTimeout(resolve, 250));
        if (await evaluate("document.getElementById('game')?.dataset.screen === 'Playing'")) break;
    }
    await until("document.getElementById('game')?.dataset.screen === 'Playing'");
    await new Promise(resolve => setTimeout(resolve, 7000));
    await capture("gameplay");
    const audio = await evaluate(`(() => {
        const context = globalThis.moohSmokeAudio;
        const samples = new Float32Array(context.moohMonitor.fftSize);
        context.moohMonitor.getFloatTimeDomainData(samples);
        return { state: context.state, time: context.currentTime,
            rms: Math.sqrt(samples.reduce((sum, value) => sum + value * value, 0) / samples.length) };
    })()`);
    console.log("audio", audio);
    assert.equal(audio.state, "running");
    assert.ok(audio.rms > 0.00001, "The offline music recording must produce audible PCM energy");
    const recording = await evaluate("globalThis.moohSmokeMusic");
    assert.ok(recording?.duration > 10, "Music must use a complete offline recording");
    assert.ok(recording.loopEnd > recording.loopStart);
    assert.ok(Math.abs(recording.duration - recording.loopEnd) < 0.01, "Decoded recording retains exact loop frames");
    const elapsed = await evaluate(`(() => {
        const context = globalThis.moohSmokeAudio;
        const before = context.currentTime;
        const deadline = performance.now() + 750;
        while (performance.now() < deadline) { }
        return context.currentTime - before;
    })()`);
    assert.ok(elapsed > 0.6, "Browser audio continues independently during a page-frame stall");
    console.log("offline music", recording, "audio advanced during stall", elapsed);
    await send("Input.dispatchKeyEvent", { type: "keyDown", key: "ArrowRight", code: "ArrowRight", windowsVirtualKeyCode: 39 });
    await evaluate("document.getElementById('update').showModal(); dispatchEvent(new Event('mooh-modal-change'))");
    await until("globalThis.moohSmokeAudio.state === 'suspended'");
    await send("Input.dispatchKeyEvent", { type: "keyUp", key: "ArrowRight", code: "ArrowRight", windowsVirtualKeyCode: 39 });
    // Exercise main.js lifecycle handling; pwa-lifecycle.test.mjs verifies the
    // real update prompt emits this notification on its native close event.
    await evaluate("document.getElementById('update').close(); dispatchEvent(new Event('mooh-modal-change'))");
    await until("globalThis.moohSmokeAudio.state === 'running'");
    console.log("modal lifecycle passed");
    await send("Input.dispatchMouseEvent", { type: "mousePressed", x: 640, y: 360, button: "right", buttons: 2, clickCount: 1 });
    await send("Input.dispatchMouseEvent", { type: "mouseReleased", x: 640, y: 360, button: "right", buttons: 0, clickCount: 1 });
    await until("document.getElementById('game')?.dataset.screen === 'Pause'");
    await new Promise(resolve => setTimeout(resolve, 600));
    await key("Escape", "Escape", 27);
    await until("document.getElementById('game')?.dataset.screen === 'Playing'");
    await key("Escape", "Escape", 27);
    await until("document.getElementById('game')?.dataset.screen === 'Pause'");
    await new Promise(resolve => setTimeout(resolve, 600));
    await capture("pause");
    await send("Emulation.setDeviceMetricsOverride", { width: 600, height: 900, deviceScaleFactor: 1, mobile: false });
    await new Promise(resolve => setTimeout(resolve, 600));
    await key("Escape", "Escape", 27);
    await until("document.getElementById('game')?.dataset.screen === 'Playing'");
    await new Promise(resolve => setTimeout(resolve, 600));
    await capture("portrait");
    await send("Emulation.setDeviceMetricsOverride", { width: 2560, height: 1080, deviceScaleFactor: 1, mobile: false });
    await new Promise(resolve => setTimeout(resolve, 600));
    await capture("ultrawide");
    // Return to the standard viewport for save/offline checks after exercising
    // ultrawide rendering.
    await send("Emulation.setDeviceMetricsOverride", { width: 1280, height: 720, deviceScaleFactor: 1, mobile: false });
    if (process.env.MOOH_STORAGE_TEST === "1") {
        // Toggle an actual options item, then verify its normalized save survives a normal reload.
        await key("Escape", "Escape", 27);
        await until("document.getElementById('game')?.dataset.screen === 'Pause'");
        await new Promise(resolve => setTimeout(resolve, 800));
        await key("F10", "F10", 121);
        await until("document.getElementById('game')?.dataset.screen === 'Options'");
        await new Promise(resolve => setTimeout(resolve, 1000));
        const beforeMusic = await evaluate("JSON.parse(localStorage.getItem('MoohDarkness.player-profile.v1'))?.options.musicVolume ?? 1");
        await key("ArrowDown", "ArrowDown", 40);
        await key("Enter", "Enter", 13);
        await until(`JSON.parse(localStorage.getItem('MoohDarkness.player-profile.v1'))?.options.musicVolume !== ${beforeMusic}`);
        const saved = await evaluate("localStorage.getItem('MoohDarkness.player-profile.v1')");
        await send("Page.reload", { ignoreCache: false });
        await until("document.getElementById('start') && !document.getElementById('start').hidden");
        assert.equal(await evaluate("localStorage.getItem('MoohDarkness.player-profile.v1')"), saved);
        console.log("PASS: options save survives normal reload");
    }
    if (process.env.MOOH_OFFLINE_TEST === "1") {
        await until("navigator.serviceWorker.controller !== null");
        await until(`(async () => {
            const source = await (await fetch('./service-worker-assets.js')).text();
            const manifest = JSON.parse(source.slice(source.indexOf('{'), source.lastIndexOf('}') + 1));
            const tree = new URL(location.href).searchParams.get('mode') === 'single' ? '_framework-single/' : '_framework/';
            const assets = manifest.assets.filter(asset => asset.url.startsWith(tree) || asset.url.startsWith('content/'));
            return (await Promise.all(assets.map(asset => caches.match(new URL(asset.url, document.baseURI))))).every(Boolean);
        })()`);
        await send("Network.emulateNetworkConditions", { offline: true, latency: 0, downloadThroughput: 0, uploadThroughput: 0 });
        await send("Page.reload", { ignoreCache: false });
        await until("document.getElementById('start') && !document.getElementById('start').hidden");
        await evaluate("document.getElementById('start').click()");
        await until("document.getElementById('game')?.dataset.screen === 'Main'");
        await capture("offline");
        await send("Network.emulateNetworkConditions", { offline: false, latency: 0, downloadThroughput: -1, uploadThroughput: -1 });
        console.log("PASS: normal offline reload boots the cached game");
    }
    assert.equal(failures.length, 0);
    console.log("PASS: menu -> map -> level -> pause/resume, portrait and ultrawide rendering");
} catch (error) {
    try { await capture("failure"); } catch { }
    throw error;
} finally {
    await send("Target.closeTarget", { targetId }, null);
    socket.close();
}
