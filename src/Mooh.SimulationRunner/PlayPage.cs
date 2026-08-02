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
    margin: 0; padding: 16px; background: #111; color: #eee;
    font: 14px ui-monospace, SFMono-Regular, Menlo, monospace;
    display: flex; flex-direction: column; align-items: center; gap: 12px;
  }
  #board {
    font-size: 26px; line-height: 1.05; letter-spacing: 0;
    white-space: pre; user-select: none;
  }
  #hud { display: flex; gap: 18px; flex-wrap: wrap; justify-content: center; }
  #hud span b { color: #9cf; font-weight: 600; }
  #banner { min-height: 22px; font-size: 16px; color: #fd6; }
  #help { color: #888; font-size: 12px; }
</style>
</head>
<body>
<div id="hud"></div>
<div id="board">loading…</div>
<div id="banner"></div>
<div id="help">WASD or arrows to walk · R restart level · N next level</div>
<script>
const held = { l: 0, u: 0, r: 0, d: 0 };
const keys = {
  ArrowLeft: 'l', KeyA: 'l', ArrowUp: 'u', KeyW: 'u',
  ArrowRight: 'r', KeyD: 'r', ArrowDown: 'd', KeyS: 'd',
};
let pending = '';

addEventListener('keydown', e => {
  const k = keys[e.code];
  if (k) { held[k] = 1; e.preventDefault(); return; }
  if (e.code === 'KeyR') { pending = 'restart'; e.preventDefault(); }
  if (e.code === 'KeyN') { pending = 'next'; e.preventDefault(); }
});
addEventListener('keyup', e => {
  const k = keys[e.code];
  if (k) { held[k] = 0; e.preventDefault(); }
});
// A hidden tab keeps its key state; clear it so the player does not walk off unseen.
addEventListener('blur', () => { held.l = held.u = held.r = held.d = 0; });

const board = document.getElementById('board');
const hud = document.getElementById('hud');
const banner = document.getElementById('banner');

const banners = {
  LevelCompleted: 'Level complete — press N for the next level',
  CampaignCompleted: 'Pack complete',
  LevelFailed: 'You died — press R to replay this level',
};

async function frame() {
  const command = pending;
  pending = '';
  const query = `l=${held.l}&u=${held.u}&r=${held.r}&d=${held.d}`
    + (command ? `&command=${command}` : '');
  try {
    const response = await fetch(`/frame?${query}`);
    if (!response.ok) { throw new Error(response.status); }
    const f = await response.json();
    board.textContent = f.rows.join('\n');
    hud.innerHTML = [
      `level <b>${f.level}/${f.levelCount}</b>`,
      `score <b>${f.score}</b>`,
      `health <b>${Math.round(f.health)}</b>`,
      `grenades <b>${f.grenades}</b>`,
      `monsters left <b>${f.monstersLeft}</b>`,
      `<b>${f.reaction}</b>`,
    ].map(t => `<span>${t}</span>`).join('');
    banner.textContent = banners[f.state] || '';
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
