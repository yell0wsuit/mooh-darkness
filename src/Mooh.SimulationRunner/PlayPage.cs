namespace Mooh.SimulationRunner
{
    /// <summary>Holds the single self-contained page served by the interactive play shell.</summary>
    internal static class PlayPage
    {
        /// <summary>
        /// Gets the page source. The browser is used only for real key-down and key-up events and
        /// for colour emoji rendering; all gameplay stays in the deterministic simulation.
        /// </summary>
        internal const string Html = """
<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<title>Mooh</title>
<style>
  :root { color-scheme: dark; }
  body {
    margin: 0; padding: 12px; background: #0d0d10; color: #eee;
    font: 13px ui-monospace, SFMono-Regular, Menlo, monospace;
    display: flex; flex-direction: column; align-items: center; gap: 8px;
    overflow: hidden;
  }
  #hud { display: flex; gap: 16px; flex-wrap: wrap; justify-content: center; }
  #hud b { color: #9cf; font-weight: 600; }
  canvas { image-rendering: auto; }
  #banner { min-height: 20px; font-size: 15px; color: #fd6; }
  #help { color: #777; font-size: 11px; }
</style>
</head>
<body>
<div id="hud"></div>
<canvas id="view"></canvas>
<div id="banner"></div>
<div id="help">WASD or arrows to walk · R restart level · N next level</div>
<script>
const held = { l: 0, u: 0, r: 0, d: 0 };
const codes = {
  ArrowLeft: 'l', KeyA: 'l', ArrowUp: 'u', KeyW: 'u',
  ArrowRight: 'r', KeyD: 'r', ArrowDown: 'd', KeyS: 'd',
};
let pending = '';

addEventListener('keydown', e => {
  const k = codes[e.code];
  if (k) { held[k] = 1; e.preventDefault(); return; }
  if (e.code === 'KeyR') { pending = 'restart'; e.preventDefault(); }
  if (e.code === 'KeyN') { pending = 'next'; e.preventDefault(); }
});
addEventListener('keyup', e => {
  const k = codes[e.code];
  if (k) { held[k] = 0; e.preventDefault(); }
});
// A hidden tab never delivers the keyup, so clear the state on blur.
addEventListener('blur', () => { held.l = held.u = held.r = held.d = 0; });

const canvas = document.getElementById('view');
const ctx = canvas.getContext('2d');
const hud = document.getElementById('hud');
const banner = document.getElementById('banner');
const banners = {
  LevelCompleted: 'Level complete — press N for the next level',
  CampaignCompleted: 'Pack complete',
  LevelFailed: 'You died — press R to replay this level',
};

let cell = 0, cols = 0, rows = 0;

function resize(width, height) {
  const dpr = devicePixelRatio || 1;
  const fit = Math.min(
    (innerWidth - 32) / width,
    (innerHeight - 110) / height);
  const next = Math.max(10, Math.min(56, Math.floor(fit)));
  if (next === cell && width === cols && height === rows) { return; }
  cell = next; cols = width; rows = height;
  canvas.width = width * cell * dpr;
  canvas.height = height * cell * dpr;
  canvas.style.width = `${width * cell}px`;
  canvas.style.height = `${height * cell}px`;
  ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
  ctx.textAlign = 'center';
  ctx.textBaseline = 'middle';
}
addEventListener('resize', () => { cell = 0; });

function draw(f) {
  resize(f.width, f.height);
  ctx.font = `${Math.floor(cell * 0.86)}px "Apple Color Emoji", "Segoe UI Emoji", "Noto Color Emoji", sans-serif`;
  ctx.clearRect(0, 0, canvas.width, canvas.height);
  for (const s of f.sprites) {
    ctx.fillText(s.glyph, (s.x + 0.5) * cell, (s.y + 0.55) * cell);
  }
  hud.innerHTML = [
    `level <b>${f.level}/${f.levelCount}</b>`,
    `score <b>${f.score}</b>`,
    `health <b>${Math.round(f.health)}</b>`,
    `grenades <b>${f.grenades}</b>`,
    `monsters left <b>${f.monstersLeft}</b>`,
    `<b>${f.reaction}</b>`,
  ].map(t => `<span>${t}</span>`).join('');
  banner.textContent = banners[f.state] || '';
}

async function frame() {
  const command = pending;
  pending = '';
  const query = `l=${held.l}&u=${held.u}&r=${held.r}&d=${held.d}`
    + (command ? `&command=${command}` : '');
  try {
    const response = await fetch(`/frame?${query}`);
    if (!response.ok) { throw new Error(response.status); }
    draw(await response.json());
  } catch (error) {
    banner.textContent = `disconnected (${error.message}) — is the runner still running?`;
  }
  requestAnimationFrame(frame);
}
requestAnimationFrame(frame);
</script>
</body>
</html>
""";
    }
}
