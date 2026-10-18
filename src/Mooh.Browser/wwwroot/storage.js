function unavailable(error) {
    console.warn("Save storage is unavailable:", error);
    const message = document.getElementById("storage-warning");
    if (message) message.hidden = false;
}
export function read(key) {
    try { return localStorage.getItem(key); } catch (error) { unavailable(error); return null; }
}
export function write(key, value) {
    try { localStorage.setItem(key, value); } catch (error) { unavailable(error); }
}
