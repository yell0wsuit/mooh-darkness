// Web Audio effects and frame-independent playback of offline-rendered music.
const clips = new Map();
const voices = new Map();
let serial = 0;
let context;
function audio() { return context ??= new AudioContext({ sampleRate: 44100 }); }
export async function unlock() { await audio().resume(); }
export async function decode(key, base64) {
    const bytes = Uint8Array.from(atob(base64), c => c.charCodeAt(0));
    let buffer;
    try { buffer = await audio().decodeAudioData(bytes.buffer); }
    catch (error) { throw new Error(`Cannot decode ${key}: ${error.message}`, { cause: error }); }
    clips.set(key, buffer);
    return buffer.length;
}
export function play(key, left, right, rate) {
    const clip = clips.get(key);
    if (!clip) return 0;
    const ctx = audio();
    const source = ctx.createBufferSource();
    source.buffer = clip;
    source.playbackRate.value = rate;
    const splitter = ctx.createChannelSplitter(clip.numberOfChannels);
    const merger = ctx.createChannelMerger(2);
    source.connect(splitter);
    for (let channel = 0; channel < 2; channel++) {
        const gain = ctx.createGain();
        gain.gain.value = channel === 0 ? left : right;
        splitter.connect(gain, Math.min(channel, clip.numberOfChannels - 1));
        gain.connect(merger, 0, channel);
    }
    merger.connect(ctx.destination);
    const id = ++serial;
    voices.set(id, source);
    source.onended = () => { voices.delete(id); source.disconnect(); splitter.disconnect(); merger.disconnect(); };
    source.start();
    return id;
}
export function playing(id) { return voices.has(id); }
export function suspended(value) {
    if (!context) return;
    if (value) {
        for (const source of voices.values()) source.stop();
        void context.suspend();
    } else void context.resume();
}

const musicBuffers = new Map();
let musicTracks;
let currentMusic;
export async function preloadMusic() {
    const root = new URL("./content/music/", document.baseURI);
    const catalog = await fetch(new URL("tracks.json", root));
    if (!catalog.ok) throw new Error("Cannot load offline music catalog");
    const manifest = await catalog.json();
    for (const entry of Object.values(manifest.tracks)) {
        if (musicBuffers.has(entry.file)) continue;
        const response = await fetch(new URL(entry.file, root));
        if (!response.ok) throw new Error(`Cannot load offline music recording: ${entry.file}`);
        musicBuffers.set(entry.file, await audio().decodeAudioData(await response.arrayBuffer()));
    }
    musicTracks = manifest.tracks;
}
export function music(track, volume) {
    if (currentMusic?.track !== track) {
        if (currentMusic) {
            currentMusic.source.stop();
            currentMusic.source.disconnect();
            currentMusic.gain.disconnect();
            currentMusic = undefined;
        }
        const entry = musicTracks?.[track];
        if (track && !entry) throw new Error(`Unknown offline music track: ${track}`);
        if (entry) {
            const gain = audio().createGain();
            const source = audio().createBufferSource();
            source.buffer = musicBuffers.get(entry.file);
            source.loop = true;
            source.loopStart = entry.loopStart;
            source.loopEnd = entry.loopEnd;
            gain.gain.value = entry.volume * Math.max(0, Math.min(1, volume));
            source.connect(gain);
            gain.connect(audio().destination);
            source.start(0, entry.offset);
            currentMusic = { track, source, gain };
        }
    }
    if (currentMusic) currentMusic.gain.gain.value = musicTracks[track].volume * Math.max(0, Math.min(1, volume));
}
