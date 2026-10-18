import test from "node:test";
import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";

test("update dialog announces both inactive and resumed lifecycle states", async () => {
    const page = new EventTarget();
    globalThis.addEventListener = page.addEventListener.bind(page);
    globalThis.removeEventListener = page.removeEventListener.bind(page);
    globalThis.dispatchEvent = page.dispatchEvent.bind(page);
    class Dialog extends EventTarget {
        open = false;
        showModal() { this.open = true; }
        close() { this.open = false; this.dispatchEvent(new Event("close")); }
    }
    const dialog = new Dialog();
    const later = {};
    const now = {};
    const documentEvents = new EventTarget();
    globalThis.document = {
        getElementById: id => ({ update: dialog, "update-later": later, "update-now": now })[id] ?? null,
        addEventListener: documentEvents.addEventListener.bind(documentEvents),
        visibilityState: "visible",
    };
    Object.defineProperty(globalThis, "navigator", { configurable: true, value: {
        serviceWorker: { controller: {}, addEventListener() {} }, storage: {},
    } });
    globalThis.moohServiceWorkerRegistration = Promise.resolve({ waiting: { postMessage() {} },
        update: async () => {}, addEventListener() {} });
    const activity = [];
    page.addEventListener("mooh-modal-change", () => activity.push(dialog.open));
    const source = await readFile(new URL("../../src/Mooh.Browser/wwwroot/pwa.js", import.meta.url), "utf8");
    await import(`data:text/javascript;base64,${Buffer.from(source).toString("base64")}`);
    await new Promise(resolve => setImmediate(resolve));
    assert.equal(dialog.open, true);
    later.onclick();
    assert.equal(dialog.open, false);
    assert.deepEqual(activity, [true, false]);
});
