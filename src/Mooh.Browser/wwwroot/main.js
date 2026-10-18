import * as hostEvents from "./host-events.js";
import { unlock, suspended } from "./audio.js";
import { setLoadingProgress } from "./page.js";
import { parseModeQuery, probeEnvironment, selectRuntime } from "./runtime-mode.js";

await globalThis.moohIsolationReady;
const choice = selectRuntime(probeEnvironment(), parseModeQuery(location.search).mode);
console.info(`Mooh runtime: ${choice.mode}, isolated=${crossOriginIsolated}`);
if (choice.mode === "unsupported") throw new Error(choice.reason);
globalThis.moohBootComplete = () => {
    globalThis.moohInstallWorker?.();
    globalThis.moohCacheGame?.(choice.mode === "threaded" ? "_framework" : "_framework-single");
};
try {
    const { dotnet } = await import(choice.runtime);
    const runtime = await dotnet.withDiagnosticTracing(false)
        .withModuleConfig({ onDownloadResourceProgress: (loaded, total) => setLoadingProgress("runtime", loaded, total) }).create();
    globalThis.moohWasmModule = runtime.Module;
    await runtime.runMain(runtime.getConfig().mainAssemblyName, []);
} catch (error) { globalThis.moohFail(error.message); throw error; }

const canvas = document.getElementById("game");
function pointer(event, phase) {
    event.preventDefault();
    const rect = canvas.getBoundingClientRect();
    hostEvents.pointer(phase, event, event.clientX - rect.left, event.clientY - rect.top);
}
canvas.addEventListener("pointerdown", event => { canvas.focus(); canvas.setPointerCapture(event.pointerId); pointer(event, 0); });
canvas.addEventListener("pointermove", event => pointer(event, 1));
canvas.addEventListener("pointerup", event => pointer(event, 2));
canvas.addEventListener("pointercancel", event => pointer(event, 3));
canvas.addEventListener("contextmenu", event => event.preventDefault());
function key(event, down) {
    if (hostEvents.forwardsKey(event)) {
        event.preventDefault();
        hostEvents.key(hostEvents.keyId(event.code), down, event.repeat);
    }
    if (down && !event.isComposing && !event.ctrlKey && !event.metaKey && !event.altKey && [...event.key].length === 1)
        hostEvents.text(event.key);
}
globalThis.addEventListener("keydown", event => key(event, true));
globalThis.addEventListener("keyup", event => key(event, false));
globalThis.addEventListener("compositionend", event => hostEvents.text(event.data));
function active() {
    const visible = !document.hidden;
    const focused = document.hasFocus();
    const modal = document.getElementById("update")?.open === true;
    hostEvents.active(visible && focused && !modal, !visible);
    suspended(!visible || !focused || modal);
}
globalThis.addEventListener("blur", active);
globalThis.addEventListener("focus", active);
globalThis.addEventListener("mooh-modal-change", active);
document.addEventListener("visibilitychange", active);
globalThis.addEventListener("pagehide", () => hostEvents.active(false, true));
active();
document.getElementById("start").addEventListener("click", async () => {
    await unlock();
    document.getElementById("splash").classList.add("hidden");
    canvas.focus();
    hostEvents.start();
    active();
});
document.getElementById("fullscreen").addEventListener("click", () => {
    if (document.fullscreenElement) void document.exitFullscreen();
    else void document.documentElement.requestFullscreen?.();
});
