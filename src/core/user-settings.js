const fs = require("node:fs/promises");
const path = require("node:path");
const { execFile } = require("node:child_process");
const { promisify } = require("node:util");

const execFileAsync = promisify(execFile);
const SETTINGS_RELATIVE_PATH = path.join(
  "Firefly Studios",
  "Stronghold Crusader Definitive Edition",
  "settings.cfg"
);

function resolveGameSettingsPath(environment = process.env) {
  if (environment.LOCALAPPDATA) {
    return path.join(path.dirname(environment.LOCALAPPDATA), "LocalLow", SETTINGS_RELATIVE_PATH);
  }
  if (environment.USERPROFILE) {
    return path.join(environment.USERPROFILE, "AppData", "LocalLow", SETTINGS_RELATIVE_PATH);
  }
  return "";
}

function isCompleteSettings(content) {
  const text = Buffer.isBuffer(content) ? content.toString("utf8") : String(content || "");
  const normalized = text.replace(/\r\n/g, "\n");
  const markers = normalized.match(/^\|\|SETTINGS\|\|$/gm) || [];
  const name = normalized.match(/(?:^|\n)Name:([^\n]*)(?:\n|$)/);
  return (
    markers.length === 2 &&
    normalized.startsWith("||SETTINGS||\n") &&
    name !== null &&
    name[1].trim().length > 0 &&
    /(?:^|\n)PushMapScrolling:(?:True|False)(?:\n|$)/i.test(normalized) &&
    normalized.trimEnd().endsWith("||KEYS||")
  );
}

async function readCompleteSettings(filePath) {
  if (!filePath) return null;
  try {
    const content = await fs.readFile(filePath);
    return isCompleteSettings(content) ? content : null;
  } catch {
    return null;
  }
}

async function writeSmallFile(filePath, content) {
  await fs.mkdir(path.dirname(filePath), { recursive: true });
  const temporary = `${filePath}.${process.pid}.tmp`;
  await fs.writeFile(temporary, content);
  await fs.copyFile(temporary, filePath);
  await fs.rm(temporary, { force: true });
}

function wait(milliseconds) {
  return new Promise((resolve) => setTimeout(resolve, milliseconds));
}

async function waitForCompleteSettings(
  filePath,
  { attempts = 30, intervalMs = 100, stableReads = 2, sleep = wait } = {}
) {
  let previous = null;
  let stableCount = 0;
  for (let attempt = 0; attempt < attempts; attempt += 1) {
    const current = await readCompleteSettings(filePath);
    if (current && previous && current.equals(previous)) {
      stableCount += 1;
    } else {
      stableCount = current ? 1 : 0;
    }
    if (current && stableCount >= stableReads) return current;
    previous = current;
    if (attempt + 1 < attempts) await sleep(intervalMs);
  }
  return null;
}

async function prepareGameSettings({
  settingsPath,
  backupPath,
  accountChanged = false,
  sleep = wait,
}) {
  if (!settingsPath || !backupPath) return { status: "disabled", settingsPath, backupPath };

  let live = await readCompleteSettings(settingsPath);
  if (!live || accountChanged) {
    live = await waitForCompleteSettings(settingsPath, {
      stableReads: accountChanged ? 3 : 2,
      sleep,
    });
  }

  const backup = await readCompleteSettings(backupPath);
  if (live) {
    if (!backup) await writeSmallFile(backupPath, live);
    return { status: "validated", settingsPath, backupPath };
  }
  if (backup) {
    await writeSmallFile(settingsPath, backup);
    return { status: "restored", settingsPath, backupPath };
  }
  return { status: "first-run", settingsPath, backupPath };
}

function parseActiveSteamUser(output) {
  const match = String(output || "").match(/ActiveUser\s+REG_DWORD\s+0x([0-9a-f]+)/i);
  if (!match) return "";
  const value = Number.parseInt(match[1], 16);
  return Number.isSafeInteger(value) && value > 0 ? String(value) : "";
}

async function getActiveSteamUserId(runRegistry = execFileAsync) {
  if (process.platform !== "win32" && runRegistry === execFileAsync) return "";
  try {
    const { stdout } = await runRegistry(
      "reg.exe",
      ["query", "HKCU\\Software\\Valve\\Steam\\ActiveProcess", "/v", "ActiveUser"],
      { windowsHide: true }
    );
    return parseActiveSteamUser(stdout);
  } catch {
    return "";
  }
}

function resolveSettingsBackupPath(dataRoot, steamUserId) {
  const accountKey = /^\d+$/.test(steamUserId || "") ? steamUserId : "default";
  return path.join(dataRoot, "user-settings", accountKey, "settings.cfg.last-good");
}

module.exports = {
  getActiveSteamUserId,
  isCompleteSettings,
  parseActiveSteamUser,
  prepareGameSettings,
  resolveGameSettingsPath,
  resolveSettingsBackupPath,
  waitForCompleteSettings,
};
