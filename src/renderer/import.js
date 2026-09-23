let localization;
const byId = (id) => document.getElementById(id);
let language = "en";
let items = [];
let selected = new Set();
let page = 0;
let busy = false;
let updatesOnly = false;
let searchTimer;
const PAGE_SIZE = 60;
function t(key, values = {}) {
  const section = updatesOnly && Object.hasOwn(localization.strings.updates, key) ? "updates" : "import";
  return SCDEI18n.translate(localization, section, key, values);
}
function status(text, error = false) {
  byId("status").textContent = text;
  byId("status").classList.toggle("error", error);
}
function matching() {
  if (updatesOnly) return items;
  const term = byId("search").value.trim().toLocaleLowerCase();
  return items.filter((item) => `${item.name} ${item.author} ${item.description}`.toLocaleLowerCase().includes(term));
}
function controls() {
  for (const id of ["manual", "refresh", "select-all", "clear"]) byId(id).disabled = busy;
  byId("confirm").disabled = busy || !selected.size;
  byId("selection-count").textContent = t("selected", { count: selected.size });
}
function render() {
  const matches = matching();
  const pages = Math.max(1, Math.ceil(matches.length / PAGE_SIZE));
  page = Math.min(page, pages - 1);
  const visible = updatesOnly ? matches : matches.slice(page * PAGE_SIZE, (page + 1) * PAGE_SIZE);
  const rows = visible.map((item) => {
    const row = document.createElement("label");
    row.className = "candidate import-columns";
    row.title = updatesOnly ? item.description : `${item.path}\n${item.description}`;
    const checkbox = document.createElement("input");
    checkbox.type = "checkbox";
    checkbox.checked = selected.has(item.path);
    checkbox.disabled = busy;
    checkbox.setAttribute("aria-label", `${item.name} v${item.version}`);
    checkbox.addEventListener("change", () => {
      if (checkbox.checked) selected.add(item.path); else selected.delete(item.path);
      controls();
    });
    const name = document.createElement("div");
    const title = document.createElement("strong");
    const author = document.createElement("small");
    title.textContent = (item.format === "se" ? "[SE] " : "") + item.name; author.textContent = item.author;
    name.append(title, author);
    const version = document.createElement("div");
    version.className = "candidate-version"; version.textContent = `v${item.version}`;
    const description = document.createElement("div");
    description.className = "candidate-description"; description.textContent = item.description;
    if (item.updateReason === "content-changed") {
      description.textContent = `${t("contentChanged")} — ${item.description}`;
      row.title = `${t("contentChanged")}\n${row.title}`;
      checkbox.setAttribute("aria-label", `${item.name} v${item.version}: ${t("contentChanged")}`);
    }
    const source = document.createElement("div");
    source.className = "candidate-source";
    if (updatesOnly) {
      source.textContent = `v${item.installedVersion}`;
    } else {
      source.textContent = t(item.source === "manual" ? "manualSource" : "workshop");
      const installed = document.createElement("small");
      installed.textContent = item.installedVersion ? t("installed", { version: item.installedVersion }) : "";
      source.append(installed);
    }
    row.append(checkbox, name, version, description, source);
    return row;
  });
  byId("candidates").replaceChildren(...rows);
  byId("empty").classList.toggle("hidden", !!matches.length || busy);
  byId("page").textContent = `${page + 1} / ${pages}`;
  byId("previous").disabled = busy || page === 0;
  byId("next").disabled = busy || page + 1 >= pages;
  controls();
}
function showIssues(issues) {
  byId("issues").classList.toggle("hidden", !issues.length);
  byId("issues-summary").textContent = t("issues", { count: issues.length });
  byId("issues-list").replaceChildren(...issues.map((issue) => {
    const entry = document.createElement("div"); entry.textContent = `${issue.path}: ${issue.error}`; return entry;
  }));
}
async function load(manual = false) {
  if (busy) return;
  busy = true; render(); status(t(manual ? "reading" : "scanning"));
  try {
    const result = await (manual ? window.modImport.manual() : window.modImport.scan());
    const previous = new Set(items.map((item) => item.path));
    items = result.items;
    const available = new Set(items.map((item) => item.path));
    selected = new Set([...selected].filter((file) => available.has(file)));
    if (manual) for (const item of items) if (!previous.has(item.path)) selected.add(item.path);
    if (result.searchedRoots) byId("locations").textContent = result.searchedRoots.join("\n") || "—";
    showIssues(result.issues);
    status(updatesOnly ? "" : `${t("found", { count: items.length })} · ${t("trust")}`);
  } catch (error) { status(t("failure", { error: error.message }), true); }
  finally { busy = false; render(); }
}
byId("refresh").addEventListener("click", () => load());
byId("manual").addEventListener("click", () => load(true));
byId("select-all").addEventListener("click", () => { for (const item of matching()) selected.add(item.path); render(); });
byId("clear").addEventListener("click", () => { selected.clear(); render(); });
byId("search").addEventListener("input", () => {
  clearTimeout(searchTimer); searchTimer = setTimeout(() => { page = 0; render(); }, 120);
});
window.addEventListener("unload", () => clearTimeout(searchTimer));
byId("previous").addEventListener("click", () => { page--; render(); });
byId("next").addEventListener("click", () => { page++; render(); });
byId("cancel").addEventListener("click", () => window.modImport.close());
byId("confirm").addEventListener("click", async () => {
  if (busy || !selected.size) return;
  const chosen = items.filter((item) => selected.has(item.path));
  if (new Set(chosen.map((item) => item.id)).size !== chosen.length) { status(t("duplicate"), true); return; }
  busy = true; render(); byId("cancel").disabled = true; status(t("importing"));
  try { await window.modImport.commit([...selected]); }
  catch (error) {
    status(error.message.includes("DUPLICATE_MOD_SELECTION") ? t("duplicate") :
      error.message.includes("MOD_CHANGED_RESCAN") ? t("changed") : t("failure", { error: error.message }), true);
  } finally { busy = false; byId("cancel").disabled = false; render(); }
});
(async () => {
  localization = await window.modImport.language();
  language = localization.language;
  updatesOnly = await window.modImport.mode() === "updates";
  if (updatesOnly) {
    document.body.classList.add("updates-only");
    document.querySelector(".app-titlebar").textContent = "SCDE Mod Manager";
  }
  document.documentElement.lang = language;
  document.documentElement.dir = localization.direction;
  document.title = `${t("title")} — SCDE Mod Manager`;
  for (const element of document.querySelectorAll("[data-i18n]")) element.textContent = t(element.dataset.i18n);
  byId("search").placeholder = t("search"); byId("search").setAttribute("aria-label", t("search"));
  byId("previous").setAttribute("aria-label", t("previous")); byId("next").setAttribute("aria-label", t("next"));
  await load();
})().catch((error) => status(error.message, true));
