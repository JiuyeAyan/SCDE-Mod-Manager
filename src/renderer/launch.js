let localization;
let state;
const byId = id => document.getElementById(id);
const time = ms => `${Math.max(0, ms / 1000).toFixed(1)} s`;
let signature = "";

function render() {
  if (!state || !localization) return;
  const t = localization.strings.launch;
  const now = state.endedAt || Date.now();
  document.documentElement.lang = state.language || "en";
  document.documentElement.dir = localization.direction;
  byId("heading").textContent = t.title;
  byId("elapsed").textContent = t.elapsed.replace("{time}", time(now - state.startedAt));
  byId("phase").textContent = t[state.phase] || t.runtime;
  const active = state.items.find(item => !item.endedAt);
  const ended = ["exited", "unconfirmed", "ready", "failed"].includes(state.phase);
  byId("spinner").classList.toggle("hidden", ended);
  byId("current").textContent = active ? active.name : (state.phase === "menu" ? t.menuHint : ended ? "" : t.pending);
  byId("hint").textContent = state.phase === "failed" ? `${t.failedHint} ${state.detail || ""}` : t.hint;
  byId("close-hint").textContent = t.closeHint;
  byId("close").textContent = t.close;
  const next = JSON.stringify([state.items, state.phase, state.language]);
  if (signature !== next) {
    signature = next;
    const history = byId("history");
    const follow = history.scrollHeight - history.scrollTop - history.clientHeight < 35;
    history.replaceChildren(...state.items.map(item => {
      const row = document.createElement("div");
      row.className = "loading-row" + (!item.endedAt ? " active" : "");
      const name = document.createElement("span"); name.textContent = item.name;
      const duration = document.createElement("small");
      row.append(name, duration);
      return row;
    }));
    if (follow) history.scrollTop = history.scrollHeight;
  }
  [...byId("history").children].forEach((row, index) => {
    const item = state.items[index];
    const suffix = !item.endedAt ? ` · ${ended ? t.interrupted : t.loading}` : "";
    row.lastChild.textContent = `≈ ${time((item.endedAt || now) - item.startedAt)}${suffix}`;
  });
}
window.launch.onProgress(next => { state = next; if (localization) render(); });
window.launch.getState().then(next => { localization = next.localization; state = next; render(); }).catch(() => {});
byId("close").onclick = () => window.launch.close();
const timer = setInterval(render, 250);
window.addEventListener("unload", () => clearInterval(timer));
