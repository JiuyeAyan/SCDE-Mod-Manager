const fs = require("node:fs/promises");
const path = require("node:path");
const { execFile } = require("node:child_process");
const { promisify } = require("node:util");
const { GAME_DIRECTORY_NAME } = require("./constants");
const { validateGameDirectory } = require("./deployer");

const execFileAsync = promisify(execFile);

async function readSteamPathFromRegistry(key, valueName) {
  try {
    const { stdout } = await execFileAsync("reg.exe", ["query", key, "/v", valueName], {
      windowsHide: true,
    });
    const match = stdout.match(new RegExp(`${valueName}\\s+REG_\\w+\\s+(.+)$`, "mi"));
    return match ? match[1].trim().replaceAll("/", path.sep) : "";
  } catch {
    return "";
  }
}

function parseLibraryFolders(vdfText) {
  const paths = [];
  const pattern = /"path"\s+"([^"]+)"/gi;
  for (const match of vdfText.matchAll(pattern)) {
    paths.push(match[1].replaceAll("\\\\", "\\"));
  }
  return paths;
}

async function detectSteamLibraries(gameDir = "") {
  const roots = new Set();
  const registryCandidates = await Promise.all([
    readSteamPathFromRegistry("HKCU\\Software\\Valve\\Steam", "SteamPath"),
    readSteamPathFromRegistry("HKLM\\SOFTWARE\\WOW6432Node\\Valve\\Steam", "InstallPath"),
  ]);

  for (const candidate of registryCandidates) {
    if (candidate) roots.add(path.resolve(candidate));
  }

  if (process.env["ProgramFiles(x86)"]) {
    roots.add(path.join(process.env["ProgramFiles(x86)"], "Steam"));
  }
  if (process.env.ProgramFiles) {
    roots.add(path.join(process.env.ProgramFiles, "Steam"));
  }

  const libraries = new Set(roots);
  if (gameDir && path.basename(path.dirname(gameDir)).toLowerCase() === "common" &&
      path.basename(path.dirname(path.dirname(gameDir))).toLowerCase() === "steamapps") {
    libraries.add(path.dirname(path.dirname(path.dirname(path.resolve(gameDir)))));
  }
  for (const root of roots) {
    try {
      const vdf = await fs.readFile(path.join(root, "steamapps", "libraryfolders.vdf"), "utf8");
      for (const library of parseLibraryFolders(vdf)) libraries.add(path.resolve(library));
    } catch {
      // A missing library list simply means this Steam candidate is not installed.
    }
  }

  return [...new Map([...libraries].map((library) => [library.toLowerCase(), library])).values()];
}

async function detectSteamGame() {
  const libraries = await detectSteamLibraries();
  const matches = [];
  for (const library of libraries) {
    const candidate = path.join(library, "steamapps", "common", GAME_DIRECTORY_NAME);
    if ((await validateGameDirectory(candidate)).valid) matches.push(candidate);
  }
  return [...new Set(matches)];
}

module.exports = { detectSteamGame, detectSteamLibraries, parseLibraryFolders };
