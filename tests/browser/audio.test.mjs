import { test } from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";

const source = readFileSync(new URL("../../src/Mooh.Browser/wwwroot/audio.js", import.meta.url), "utf8");
const moduleUrl = `data:text/javascript;base64,${Buffer.from(source).toString("base64")}`;
let serial = 0;
function buffer(channels, frames, rate) {
    const data = Array.from({ length: channels }, () => new Float32Array(frames));
    return { length: frames, sampleRate: rate, numberOfChannels: channels,
        duration: frames / rate, getChannelData: channel => data[channel] };
}
function node() {
    return { connections: [], disconnected: false,
        connect(...args) { this.connections.push(args); },
        disconnect() { this.disconnected = true; } };
}
async function harness() {
    let context;
    class Context {
        constructor(options) {
            context = this;
            this.sampleRate = options.sampleRate;
            this.currentTime = 10;
            this.destination = node();
            this.sources = [];
            this.gains = [];
            this.resumes = 0;
            this.suspends = 0;
        }
        async resume() { this.resumes++; }
        async suspend() { this.suspends++; }
        async decodeAudioData(bytes) { this.decoded = new Uint8Array(bytes); return this.clip; }
        createBuffer(channels, frames, rate) { return buffer(channels, frames, rate); }
        createBufferSource() {
            const source = Object.assign(node(), {
                playbackRate: { value: 1 }, stops: 0,
                start(time, offset) { this.startTime = time; this.startOffset = offset; },
                stop() { this.stops++; this.onended?.(); },
            });
            this.sources.push(source);
            return source;
        }
        createGain() { const gain = Object.assign(node(), { gain: { value: 1 } }); this.gains.push(gain); return gain; }
        createChannelSplitter() { return node(); }
        createChannelMerger() { return node(); }
    }
    globalThis.AudioContext = Context;
    const audio = await import(`${moduleUrl}#${serial++}`);
    await audio.unlock();
    return { audio, context };
}
test("effect decoding preserves encoded payload", async () => {
    const { audio, context } = await harness();
    context.clip = buffer(1, 3, 44100);
    assert.equal(await audio.decode("clip", "AQID/w=="), 3);
    assert.deepEqual([...context.decoded], [1, 2, 3, 255]);
});

test("effect rate and independent stereo gains reach graph, completion retires voice", async () => {
    const { audio, context } = await harness();
    context.clip = buffer(1, 3, 44100);
    await audio.decode("mono", "AA==");
    const voice = audio.play("mono", 0.25, 0.75, 1.5);
    assert.equal(audio.playing(voice), true);
    assert.equal(context.sources[0].playbackRate.value, 1.5);
    assert.deepEqual(context.gains.map(gain => gain.gain.value), [0.25, 0.75]);
    context.sources[0].onended();
    assert.equal(audio.playing(voice), false);
    assert.equal(context.sources[0].disconnected, true);
    assert.equal(audio.play("missing", 1, 1, 1), 0);
});

test("suspend stops active effects and resume unlocks context", async () => {
    const { audio, context } = await harness();
    context.clip = buffer(1, 3, 44100);
    await audio.decode("mono", "AA==");
    const first = audio.play("mono", 1, 1, 1);
    const second = audio.play("mono", 1, 1, 1);
    audio.suspended(true);
    assert.deepEqual(context.sources.map(source => source.stops), [1, 1]);
    assert.equal(audio.playing(first), false);
    assert.equal(audio.playing(second), false);
    assert.equal(context.suspends, 1);
    audio.suspended(false);
    assert.equal(context.resumes, 2);
});

test("offline music uses original order offsets, loops continuously and changes gain without restarting", async () => {
    const { audio, context } = await harness();
    context.clip = buffer(2, 44100 * 120, 44100);
    globalThis.document = { baseURI: "https://example.test/game/" };
    const requests = [];
    globalThis.fetch = async url => {
        requests.push(String(url));
        return { ok: true, json: async () => ({ tracks: {
            menu: { file: "TrackMenu.ogg", offset: 0, loopStart: 0, loopEnd: 120, volume: .60 }, game: { file: "TrackMenu.ogg", offset: 80, loopStart: 0, loopEnd: 120, volume: .60 }, gameend: { file: "TrackMenu.ogg", offset: 32, loopStart: 8, loopEnd: 120, volume: .65 },
        } }), arrayBuffer: async () => new ArrayBuffer(4) };
    };
    await audio.preloadMusic();
    audio.music("game", .5);
    const source = context.sources[0];
    assert.equal(source.buffer, context.clip);
    assert.equal(source.loop, true);
    assert.equal(source.loopStart, 0);
    assert.equal(source.loopEnd, 120);
    assert.equal(source.startOffset, 80);
    assert.equal(context.gains[0].gain.value, .30);
    audio.music("game", .25);
    assert.equal(context.sources.length, 1);
    assert.equal(context.gains[0].gain.value, .15);
    audio.suspended(true);
    assert.equal(source.stops, 0, "focus loss suspends music without losing its position");
    audio.suspended(false);
    audio.music("gameend", 1);
    assert.equal(source.stops, 1);
    assert.equal(context.sources[1].startOffset, 32);
    assert.equal(context.sources[1].loopStart, 8);
    assert.equal(context.gains[1].gain.value, .65);
    audio.music("", 1);
    assert.equal(context.sources[1].stops, 1);
    assert.deepEqual(requests, ["https://example.test/game/content/music/tracks.json", "https://example.test/game/content/music/TrackMenu.ogg"]);
});
