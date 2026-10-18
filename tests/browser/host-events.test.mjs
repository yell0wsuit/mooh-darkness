import { test } from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";

const source = readFileSync(new URL("../../src/Mooh.Browser/wwwroot/host-events.js", import.meta.url), "utf8");
const moduleUrl = `data:text/javascript;base64,${Buffer.from(source).toString("base64")}`;
let serial = 0;
async function harness(shared = false) {
    const BufferType = shared ? SharedArrayBuffer : ArrayBuffer;
    let buffer = new BufferType(65536);
    let wakes = 0;
    globalThis.moohWasmModule = { HEAPU8: new Uint8Array(buffer), _mooh_wake: () => wakes++ };
    const events = await import(`${moduleUrl}#${serial++}`);
    events.attach(16, 0);
    return {
        events,
        get words() { return new Int32Array(buffer); },
        get wakes() { return wakes; },
        record(index) { return [...this.words.slice(8 + index * 6, 14 + index * 6)]; },
        grow() {
            const grown = new BufferType(131072);
            new Uint8Array(grown).set(new Uint8Array(buffer));
            buffer = grown;
            globalThis.moohWasmModule.HEAPU8 = new Uint8Array(buffer);
        },
    };
}
const event = (code, overrides = {}) => ({ code, ctrlKey: false, metaKey: false, altKey: false, ...overrides });

test("DOM key IDs match managed SDL controls and aliases", async () => {
    const { events } = await harness();
    const managed = readFileSync(new URL("../../src/Mooh.Browser/BrowserKeys.cs", import.meta.url), "utf8");
    const keys = {
        Escape: [1, "Escape"], Enter: [2, "Return"], ArrowLeft: [3, "Left"],
        ArrowUp: [4, "Up"], ArrowRight: [5, "Right"], ArrowDown: [6, "Down"],
        Space: [7, "Space"], ControlLeft: [8, "LCtrl"], ControlRight: [9, "RCtrl"],
        Digit1: [10, "Alpha1"], Digit2: [11, "Alpha2"], Digit3: [12, "Alpha3"], Digit4: [13, "Alpha4"],
        KeyP: [14, "P"], Pause: [15, "Pause"], F2: [16, "F2"], F10: [17, "F10"],
        Backspace: [18, "Backspace"], Delete: [19, "Delete"], Home: [20, "Home"], End: [21, "End"], Tab: [22, "Tab"], ShiftLeft: [23, "LShift"], ShiftRight: [24, "RShift"],
    };
    for (const [code, [id, sdl]] of Object.entries(keys)) {
        assert.equal(events.keyId(code), id, code);
        assert.match(managed, new RegExp(`\\[${id}\\] = SDL\\.Keycode\\.${sdl}\\b`));
    }
    assert.equal(events.keyId("KeyQ"), undefined);
    assert.equal(events.keyId("NumpadEnter"), 2);
    assert.equal(events.keyId("KeyZ"), undefined);
});

test("Ctrl combines fire with movement and weapon changes while browser shortcuts stay available", async () => {
    const { events } = await harness();
    for (const code of ["ControlLeft", "ControlRight", "ArrowLeft", "ArrowUp", "ArrowRight", "ArrowDown", "Space", "Enter", "Digit1", "Digit2", "Digit3", "Digit4"]) {
        assert.equal(events.forwardsKey(event(code, { ctrlKey: true })), true, code);
        assert.equal(events.forwardsKey(event(code, { metaKey: true })), false, code);
        assert.equal(events.forwardsKey(event(code, { altKey: true })), false, code);
    }
    assert.equal(events.forwardsKey(event("KeyP", { ctrlKey: true })), false);
    assert.equal(events.forwardsKey(event("KeyR", { metaKey: true })), false);
    assert.equal(events.forwardsKey(event("KeyQ")), false);
    assert.equal(events.forwardsKey(event("KeyZ")), false);
});

test("repeat flags and Unicode scalar text survive ring encoding", async () => {
    const h = await harness();
    h.events.key(7, true, true);
    h.events.key(7, false);
    h.events.text("Aé😀");
    assert.deepEqual(h.record(0), [2, 1, 7, 1, 0, 0]);
    assert.deepEqual(h.record(1), [2, 0, 7, 0, 0, 0]);
    for (const [offset, point] of [65, 233, 0x1f600].entries()) {
        assert.deepEqual(h.record(offset + 2), [7, point, 0, 0, 0, 0]);
    }
});

for (const shared of [false, true]) {
    test(`move flood reserves release and lifecycle slots (${shared ? "shared" : "unshared"})`, async () => {
        const h = await harness(shared);
        const pointer = { pointerType: "touch", pointerId: 9, buttons: 1 };
        for (let i = 0; i < 1000; i++) h.events.pointer(1, pointer, 1, 2);
        assert.equal(h.words[4], 992);
        assert.equal(h.words[6], 8);
        h.events.pointer(2, pointer, 1, 2);
        h.events.key(7, false);
        h.events.active(false, true);
        assert.equal(h.words[4], 995);
        assert.equal(h.record(992)[1], 2 | (2 << 8));
        assert.deepEqual(h.record(993), [2, 0, 7, 0, 0, 0]);
        assert.deepEqual(h.record(994), [4, 0, 1, 0, 0, 0]);
        assert.equal(h.wakes, 1);
    });
    test(`HEAPU8 replacement after growth refreshes views (${shared ? "shared" : "unshared"})`, async () => {
        const h = await harness(shared);
        h.events.key(3, true);
        h.grow();
        h.events.key(3, false);
        h.events.start();
        assert.equal(h.words[4], 3);
        assert.deepEqual(h.record(0), [2, 1, 3, 0, 0, 0]);
        assert.deepEqual(h.record(1), [2, 0, 3, 0, 0, 0]);
        assert.deepEqual(h.record(2), [6, 0, 0, 0, 0, 0]);
        assert.equal(h.wakes, 1);
    });
}

test("worker lifecycle wake reaches its owning worker", async () => {
    const h = await harness(true);
    const messages = [];
    globalThis.moohWasmModule.PThread = { pthreads: { 12: { postMessage: message => messages.push(message) } } };
    h.events.attach(16, 12);
    h.events.active(true, false);
    assert.deepEqual(messages, [{ moohWake: 1 }]);
    assert.throws(() => h.events.attach(16, 99), /worker is missing/);
});

test("mapped key releases still forward after modifier state changes", async () => {
    const { events } = await harness();
    assert.equal(events.forwardsKey(event("ArrowLeft", { type: "keydown" })), true);
    // A modifier can be pressed between down and up; losing this up leaves movement held.
    for (const modifier of ["ctrlKey", "metaKey", "altKey"]) {
        assert.equal(events.forwardsKey(event("ArrowLeft", { type: "keyup", [modifier]: true })), true);
    }
    assert.equal(events.forwardsKey(event("ControlLeft", { type: "keyup", metaKey: true })), true);
    assert.equal(events.forwardsKey(event("KeyZ", { type: "keyup" })), false);
});
