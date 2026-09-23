const assert = require("node:assert/strict");
const fs = require("node:fs/promises");
const os = require("node:os");
const path = require("node:path");
const test = require("node:test");
const { EventEmitter } = require("node:events");
const AdmZip = require("adm-zip");
const {
  createCompatibilityProfile,
  createLobbyProfileText,
} = require("../src/core/compatibility-profile");
const { GAME_EXECUTABLE } = require("../src/core/constants");
const { parseLibraryFolders } = require("../src/core/detect-game");
const { filesHaveSameContents } = require("../src/core/deployer");
const {
  ModManager,
  createGameLaunchEnvironment,
  isSteamRunning,
  resolveStageDirectory,
} = require("../src/core/manager");
const { normalizeEntryName, validateManifest } = require("../src/core/mod-package");
const {
  isCompleteSettings,
  parseActiveSteamUser,
  prepareGameSettings,
  resolveGameSettingsPath,
} = require("../src/core/user-settings");

const multiplayerCompatibilitySource = path.join(
  __dirname,
  "..",
  "mods",
  "scde-multiplayer-compatibility",
  "src",
  "SCDEMultiplayerCompatibilityPlugin.cs"
);
const rendererHtml = path.join(__dirname, "..", "src", "renderer", "index.html");
const rendererCss = path.join(__dirname, "..", "src", "renderer", "styles.css");
const rendererSource = path.join(__dirname, "..", "src", "renderer", "app.js");
const mainSource = path.join(__dirname, "..", "src", "main.js");
const packageJsonPath = path.join(__dirname, "..", "package.json");

async function makeGame(root) {
  const gameDir = path.join(root, "original-game");
  await fs.mkdir(path.join(gameDir, "data"), { recursive: true });
  await fs.writeFile(path.join(gameDir, GAME_EXECUTABLE), "fake executable");
  await fs.writeFile(path.join(gameDir, "data", "rules.dat"), "original rules");
  return gameDir;
}

async function makePackage(root, manifest, files) {
  const zip = new AdmZip();
  zip.addFile("manifest.json", Buffer.from(JSON.stringify(manifest)));
  for (const [relative, content] of Object.entries(files)) {
    zip.addFile(`payload/${relative}`, Buffer.from(content));
  }
  const packagePath = path.join(root, `${manifest.id}.scdemod`);
  zip.writeZip(packagePath);
  return packagePath;
}

function completeSettings(pushMapScrolling = false) {
  return Buffer.from(
    `||SETTINGS||\nName:Test Lord\nPushMapScrolling:${
      pushMapScrolling ? "True" : "False"
    }\n||SETTINGS||\nLeft:LeftArrow:\n||KEYS||\n`
  );
}

test("game launch environment keeps Steam in the isolated copy", () => {
  const environment = createGameLaunchEnvironment({ SENTINEL: "kept" });
  assert.equal(environment.SENTINEL, "kept");
  assert.equal(environment.SteamAppId, "3024040");
  assert.equal(environment.SteamGameId, "3024040");
});

test("manager releases only the single-file portable Windows artifact", async () => {
  const packageJson = JSON.parse(await fs.readFile(packageJsonPath, "utf8"));
  assert.equal(packageJson.scripts.dist, "electron-builder --win portable");
  assert.equal(Object.hasOwn(packageJson.scripts, "dist:no-install"), false);
  assert.deepEqual(packageJson.build.win.target, ["portable"]);
  assert.equal(
    packageJson.build.win.artifactName,
    "SCDE-Mod-Manager-Portable.${ext}"
  );
});

test("SCDE settings validation rejects a missing or half-written file", () => {
  assert.equal(isCompleteSettings(completeSettings()), true);
  assert.equal(isCompleteSettings("||SETTINGS||\nName:Test Lord\n"), false);
  assert.equal(
    isCompleteSettings(
      "||SETTINGS||\nName:   \nPushMapScrolling:False\n||SETTINGS||\n||KEYS||\n"
    ),
    false
  );
  assert.equal(isCompleteSettings(""), false);
  assert.equal(
    resolveGameSettingsPath({ LOCALAPPDATA: path.join("C:", "Users", "Test", "AppData", "Local") }),
    path.join(
      "C:",
      "Users",
      "Test",
      "AppData",
      "LocalLow",
      "Firefly Studios",
      "Stronghold Crusader Definitive Edition",
      "settings.cfg"
    )
  );
  assert.equal(
    parseActiveSteamUser("ActiveUser    REG_DWORD    0x138c4791"),
    "327960465"
  );
});

test("SCDE settings protection restores the active account snapshot", async (t) => {
  const root = await fs.mkdtemp(path.join(os.tmpdir(), "scde-manager-settings-restore-"));
  t.after(() => fs.rm(root, { recursive: true, force: true }));
  const settingsPath = path.join(root, "LocalLow", "settings.cfg");
  const backupPath = path.join(root, "manager-data", "user-settings", "1", "settings.cfg.last-good");
  await fs.mkdir(path.dirname(backupPath), { recursive: true });
  await fs.writeFile(backupPath, completeSettings(false));
  await fs.mkdir(path.dirname(settingsPath), { recursive: true });
  await fs.writeFile(settingsPath, "||SETTINGS||\nName:partial");

  const result = await prepareGameSettings({
    settingsPath,
    backupPath,
    sleep: async () => {},
  });

  assert.equal(result.status, "restored");
  assert.deepEqual(await fs.readFile(settingsPath), completeSettings(false));
});

test("SCDE settings protection restores a blank-name reset without replacing the snapshot", async (t) => {
  const root = await fs.mkdtemp(path.join(os.tmpdir(), "scde-manager-blank-name-"));
  t.after(() => fs.rm(root, { recursive: true, force: true }));
  const settingsPath = path.join(root, "settings.cfg");
  const backupPath = path.join(root, "settings.cfg.last-good");
  await fs.writeFile(backupPath, completeSettings(false));
  await fs.writeFile(settingsPath, completeSettings(true).toString().replace("Name:Test Lord", "Name:"));
  const result = await prepareGameSettings({ settingsPath, backupPath, sleep: async () => {} });
  assert.equal(result.status, "restored");
  assert.deepEqual(await fs.readFile(settingsPath), completeSettings(false));
  assert.deepEqual(await fs.readFile(backupPath), completeSettings(false));
});

test("SCDE settings protection waits for an account handoff and keeps its older backup", async (t) => {
  const root = await fs.mkdtemp(path.join(os.tmpdir(), "scde-manager-settings-handoff-"));
  t.after(() => fs.rm(root, { recursive: true, force: true }));
  const settingsPath = path.join(root, "LocalLow", "settings.cfg");
  const backupPath = path.join(root, "manager-data", "user-settings", "2", "settings.cfg.last-good");
  await fs.mkdir(path.dirname(backupPath), { recursive: true });
  await fs.writeFile(backupPath, completeSettings(false));
  let waits = 0;

  const result = await prepareGameSettings({
    settingsPath,
    backupPath,
    accountChanged: true,
    sleep: async () => {
      waits += 1;
      if (waits === 1) {
        await fs.mkdir(path.dirname(settingsPath), { recursive: true });
        await fs.writeFile(settingsPath, "||SETTINGS||\nName:partial");
      }
      if (waits === 2) await fs.writeFile(settingsPath, completeSettings(true));
    },
  });

  assert.equal(result.status, "validated");
  assert.equal(waits >= 4, true);
  assert.deepEqual(await fs.readFile(settingsPath), completeSettings(true));
  assert.deepEqual(await fs.readFile(backupPath), completeSettings(false));
});

test("SCDE prelaunch validation never overwrites an existing recovery snapshot", async (t) => {
  const root = await fs.mkdtemp(path.join(os.tmpdir(), "scde-manager-settings-preserve-"));
  t.after(() => fs.rm(root, { recursive: true, force: true }));
  const settingsPath = path.join(root, "settings.cfg");
  const backupPath = path.join(root, "backup", "settings.cfg.last-good");
  await fs.writeFile(settingsPath, completeSettings(true));
  await fs.mkdir(path.dirname(backupPath), { recursive: true });
  await fs.writeFile(backupPath, completeSettings(false));

  const result = await prepareGameSettings({ settingsPath, backupPath });

  assert.equal(result.status, "validated");
  assert.deepEqual(await fs.readFile(settingsPath), completeSettings(true));
  assert.deepEqual(await fs.readFile(backupPath), completeSettings(false));
});

test("manifest and archive paths reject unsafe input", () => {
  assert.throws(() => validateManifest({ id: "Bad Id", name: "Bad", version: "1" }));
  assert.throws(() =>
    validateManifest({
      id: "self.mod",
      name: "Self",
      version: "1",
      dependencies: [{ id: "self.mod" }],
    })
  );
  assert.throws(() => normalizeEntryName("../outside.txt"));
  assert.throws(() => normalizeEntryName("C:/outside.txt"));
});

test("manifest preserves explicit Mod dependencies", () => {
  const manifest = validateManifest({
    id: "plugin.mod",
    name: "Plugin",
    version: "1.0.0",
    dependencies: [{ id: "bepinex-runtime", version: "5.4.23.5" }],
  });
  assert.deepEqual(manifest.dependencies, [
    { id: "bepinex-runtime", version: "5.4.23.5" },
  ]);
});

test("large conflict comparisons are chunked and still detect late differences", async (t) => {
  const root = await fs.mkdtemp(path.join(os.tmpdir(), "scde-manager-file-compare-"));
  t.after(() => fs.rm(root, { recursive: true, force: true }));
  const first = path.join(root, "first.bin");
  const second = path.join(root, "second.bin");
  const bytes = Buffer.alloc(192 * 1024, 7);
  await fs.writeFile(first, bytes);
  await fs.writeFile(second, bytes);
  assert.equal(await filesHaveSameContents(first, second), true);
  bytes[bytes.length - 1] = 8;
  await fs.writeFile(second, bytes);
  assert.equal(await filesHaveSameContents(first, second), false);
});

test("Steam library VDF parser returns escaped Windows paths", () => {
  const vdf = '"libraryfolders" { "0" { "path" "C:\\\\Steam" } "1" { "path" "D:\\\\Games" } }';
  assert.deepEqual(parseLibraryFolders(vdf), ["C:\\Steam", "D:\\Games"]);
});

test("first launch language is detected once and then persisted", async (t) => {
  const root = await fs.mkdtemp(path.join(os.tmpdir(), "scde-manager-language-"));
  t.after(() => fs.rm(root, { recursive: true, force: true }));

  const chineseManager = new ModManager(path.join(root, "chinese"));
  assert.equal(await chineseManager.initializeLanguage("zh-TW"), "zh-CN");
  assert.equal(await chineseManager.initializeLanguage("en-US"), "zh-CN");
  assert.equal((await chineseManager.configStore.load()).language, "zh-CN");

  const nonChineseManager = new ModManager(path.join(root, "non-chinese"));
  assert.equal(await nonChineseManager.initializeLanguage("fr-FR"), "en");
  assert.equal((await nonChineseManager.configStore.load()).language, "en");
});

test("Steam detection recognizes only a running Steam process", async () => {
  assert.equal(
    await isSteamRunning(async () => ({ stdout: '"steam.exe","123","Console"' })),
    true
  );
  assert.equal(
    await isSteamRunning(async () => ({ stdout: "INFO: No tasks are running" })),
    false
  );
  assert.equal(
    await isSteamRunning(async () => {
      throw new Error("tasklist unavailable");
    }),
    false
  );
});

test("the game copy is stored beside the original game instead of manager data", async (t) => {
  const root = await fs.mkdtemp(path.join(os.tmpdir(), "scde-manager-stage-location-"));
  t.after(() => fs.rm(root, { recursive: true, force: true }));
  const gameDir = await makeGame(root);
  const manager = new ModManager(path.join(root, "manager-data"));

  await manager.setGameDirectory(gameDir);
  const expected = path.join(root, "original-game - SCDE Modded");
  assert.equal(resolveStageDirectory(gameDir), expected);
  assert.equal((await manager.getState()).stageDir, expected);
  assert.equal(manager.stageDir, expected);
  assert.equal(manager.stageDir.startsWith(manager.dataRoot), false);
});

test("multiplayer profile compares Mod IDs and versions but ignores load order", () => {
  const mods = [
    { id: "first.mod", version: "1.0.0" },
    { id: "second.mod", version: "2.0.0" },
  ];
  const baseline = createCompatibilityProfile(mods, ["first.mod", "second.mod"]);

  assert.deepEqual(baseline.mods, [
    { id: "first.mod", name: "first.mod", version: "1.0.0" },
    { id: "second.mod", name: "second.mod", version: "2.0.0" },
  ]);
  assert.match(createLobbyProfileText(baseline), /^SCDEMM2\|[0-9a-f]{64}\n/);
  assert.equal(
    baseline.fingerprint,
    createCompatibilityProfile(mods, ["second.mod", "first.mod"]).fingerprint
  );
  assert.equal(baseline.schema, 2);
  assert.notEqual(
    baseline.fingerprint,
    createCompatibilityProfile(
      [mods[0], { id: "second.mod", version: "2.0.1" }],
      ["first.mod", "second.mod"]
    ).fingerprint
  );
  assert.notEqual(
    baseline.fingerprint,
    createCompatibilityProfile(mods, ["first.mod"]).fingerprint
  );
});

test("required multiplayer components are enabled and write the staged profile", async (t) => {
  const root = await fs.mkdtemp(path.join(os.tmpdir(), "scde-manager-system-mods-"));
  t.after(() => fs.rm(root, { recursive: true, force: true }));
  const gameDir = await makeGame(root);
  const runtime = await makePackage(
    root,
    { id: "bepinex-runtime", name: "Runtime", version: "5.4.23.5" },
    { "BepInEx/core/BepInEx.dll": "runtime" }
  );
  const compatibility = await makePackage(
    root,
    {
      id: "scde-multiplayer-compatibility",
      name: "Compatibility",
      version: "0.1.0",
      dependencies: [{ id: "bepinex-runtime", version: "5.4.23.5" }],
    },
    { "BepInEx/plugins/Compatibility/plugin.dll": "checker" }
  );
  const manager = new ModManager(path.join(root, "manager-data"), {
    systemPackages: [
      { id: "bepinex-runtime", version: "5.4.23.5", packagePath: runtime },
      {
        id: "scde-multiplayer-compatibility",
        version: "0.1.0",
        packagePath: compatibility,
      },
    ],
  });

  await manager.setGameDirectory(gameDir);
  let state = await manager.ensureSystemMods();
  assert.deepEqual(state.enabledMods, ["bepinex-runtime", "scde-multiplayer-compatibility"]);
  assert.deepEqual(
    state.mods.map((mod) => mod.id),
    ["bepinex-runtime", "scde-multiplayer-compatibility"]
  );
  assert.equal(state.mods.every((mod) => mod.required), true);
  await manager.configStore.update((config) => ({
    ...config,
    modOrder: ["scde-multiplayer-compatibility", "bepinex-runtime"],
    enabledMods: [],
  }));
  state = await manager.ensureSystemMods();
  assert.deepEqual(state.enabledMods, ["bepinex-runtime", "scde-multiplayer-compatibility"]);
  assert.deepEqual(
    state.mods.map((mod) => mod.id),
    ["bepinex-runtime", "scde-multiplayer-compatibility"]
  );
  await assert.rejects(
    () => manager.setEnabled("scde-multiplayer-compatibility", false),
    /不能禁用/
  );
  await assert.rejects(
    () => manager.removeMod("scde-multiplayer-compatibility"),
    /不能移除/
  );
  await assert.rejects(() => manager.moveMod("bepinex-runtime", 1), /位置已固定/);

  state = await manager.prepareStage();
  const profile = JSON.parse(
    await fs.readFile(path.join(manager.stageDir, "_scde_manager", "active-mods.json"), "utf8")
  );
  assert.equal(state.stageReady, true);
  assert.deepEqual(profile.mods, [
    { id: "bepinex-runtime", name: "BepInEx Runtime", version: "5.4.23.5" },
    {
      id: "scde-multiplayer-compatibility",
      name: "Compatibility",
      version: "0.1.0",
    },
  ]);
  assert.match(
    await fs.readFile(path.join(manager.stageDir, "_scde_manager", "active-mods.lobby"), "utf8"),
    /^SCDEMM2\|[0-9a-f]{64}\n/
  );
  assert.match(profile.fingerprint, /^[0-9a-f]{64}$/);
});

test("multiplayer checker publishes Steam profiles, rejects mismatches, and blocks start", async () => {
  const source = await fs.readFile(multiplayerCompatibilitySource, "utf8");
  assert.match(source, /PluginVersion = "0\.4\.0"/);
  const formatMod = source.match(/private static string FormatMod\(ProfileMod mod\)\s*\{([^}]+)\}/)[1];
  assert.match(formatMod, /return mod.Name \+ "  v" \+ mod.Version;/);
  assert.doesNotMatch(formatMod, /mod.Id/);
  assert.match(source, /HarmonyPatch\(typeof\(ConfigSettings\), "LoadSettings"\)/);
  assert.match(source, /HarmonyPatch\(typeof\(ConfigSettings\), "SaveSettings"\)/);
  assert.match(source, /SCDEModManagerSettingsBackup/);
  assert.match(source, /Thread\.Sleep\(RetryDelayMilliseconds\)/);
  assert.match(source, /ConfigSettings\.LoadSettings\(\)/);
  assert.match(source, /ConfigSettings\.SettingsFileExisted/);
  assert.match(source, /if \(!loadSucceeded && File\.Exists\(backupPath\)\) return;/);
  assert.match(source, /DontDestroyOnLoad\(runtimeObject\)/);
  assert.match(source, /class CompatibilityRuntime : MonoBehaviour/);
  assert.match(source, /ReferenceEquals\(owner, null\).*owner\.RuntimeUpdate\(\)/);
  assert.match(source, /!ReferenceEquals\(plugin, null\) && plugin\.AllowLobbyJoin/);
  assert.match(source, /!ReferenceEquals\(plugin, null\) && plugin\.AllowMultiplayerStart/);
  assert.doesNotMatch(source, /房间尚未发布 Mod 兼容性清单/);
  assert.doesNotMatch(source, /temporarily unable to read the host's complete list/i);
  assert.match(source, /QueuePendingJoin/);
  assert.match(source, /ProcessPendingJoin/);
  assert.match(source, /pendingJoinExpiresAt/);
  assert.match(source, /allowNextJoinLobbyId/);
  assert.match(source, /if \(hostProfile == null && !unverified\) return;/);
  assert.match(source, /if \(advertisedMMC\) return 0;/);
  assert.match(source, /if \(currentLobbyId == 0\) return true;/);
  assert.match(source, /MULTIPLAYER_COMPATIBILITY_RUNTIME_ACTIVE/);
  assert.match(source, /SetLobbyMemberData\(lobby\.id, MemberProfileKey, localToken\)/);
  assert.match(source, /if \(!RefreshRuntimeProfile\(\)\)[\s\S]*SetLobbyMemberData\(lobby\.id, MemberProfileKey, ""\)/);
  assert.match(source, /SetLobbyData\(lobby\.id, LobbyProfileKey, ""\)/);
  assert.match(source, /GetLobbyMemberData/);
  assert.match(source, /SteamMatchmaking\.GetLobbyOwner\(lobby\.id\) == SteamUser\.GetSteamID\(\)/);
  assert.match(source, /bool isLocalHost = lobby\.isHost \|\| localOwnsLobby/);
  assert.match(source, /KickMemberFromLobby\(member\)/);
  assert.match(source, /HarmonyPatch\(typeof\(Platform_Multiplayer\), "JoinLobby"\)/);
  assert.match(source, /plugin\.AllowLobbyJoin\(__instance, __0, __1, __2, __3\)/);
  assert.match(source, /HarmonyPatch\(typeof\(Platform_Multiplayer\), "HostStartGame"\)/);
  assert.doesNotMatch(source, /HarmonyPatch\(typeof\(Platform_Multiplayer\), "StartGame"\)/);
  assert.match(source, /HarmonyPatch\(typeof\(CrusaderDE\.FRONT_Multiplayer\), "doOpen"\)/);
  assert.match(source, /plugin\.SetSkirmishSetup\(__0\)/);
  assert.match(source, /HarmonyPatch\(typeof\(CrusaderDE\.FRONT_Multiplayer\), "LeaveLobby"\)/);
  assert.match(source, /plugin\.CancelPendingJoin\(\)/);
  assert.match(source, /joinGeneration/);
  assert.match(source, /generation != joinGeneration \|\| singlePlayerSkirmishSetup/);
  assert.match(source, /platform\.LeaveLobby\(false\);[\s\S]*return;[\s\S]*lobbyJoined\(\)/);
  assert.match(source, /if \(singlePlayerSkirmishSetup\) return true;/);
  assert.match(source, /MemberHandshakeGraceSeconds = 3f/);
  assert.match(source, /PendingJoinWaitSeconds = 12f/);
  assert.match(source, /MemberHandshakeGraceSeconds\) \{ allAllowed = false; continue; \}[\s\S]*RejectMember\(platform, lobby, memberId, String.IsNullOrEmpty\(remoteToken\)\)/);
  assert.match(source, /OverlayDurationSeconds = 5f/);
  assert.match(source, /overlayUntil = Time\.unscaledTime \+ OverlayDurationSeconds/);
  assert.match(source, /"正在验证 Mod 列表…"/);
  assert.match(source, /"Verifying Mod list\.\.\."/);
  assert.match(source, /ShowOverlay\([\s\S]*true\);/);
  assert.match(source, /handledClientLobbyId/);
  assert.match(source, /handledClientLobbyId == lobby\.id\.m_SteamID\)[\s\S]*platform\.LeaveLobby\(false\)/);
  assert.match(source, /bool firstRejection = handledMembers\.Add\(memberId\.m_SteamID\);[\s\S]*platform\.KickMemberFromLobby\(member\);[\s\S]*if \(!firstRejection\) return;/);
  assert.match(source, /activeMemberIds\.Clear\(\)/);
  assert.match(source, /staleMemberIds\.Clear\(\)/);
  assert.doesNotMatch(source, /RemoveWhere\(/);
  assert.match(source, /overlayTextStyle/);
  assert.match(source, /CultureInfo\.InstalledUICulture/);
  assert.match(source, /LobbyManifestChunkPrefix/);
  assert.match(source, /BuildVisitorMismatchMessage/);
  assert.match(source, /Host's complete enabled Mod list/);
  assert.match(source, /overlayTextStyle\.fontSize = Math\.Max\(18, Math\.Min\(28/);
  assert.match(source, /new Color\(0\.035f, 0\.04f, 0\.04f, 0\.97f\)/);
});

test("compact manager layout keeps the drag bar visible and merges Mod version with name", async () => {
  const [html, css, renderer, main] = await Promise.all([
    fs.readFile(rendererHtml, "utf8"),
    fs.readFile(rendererCss, "utf8"),
    fs.readFile(rendererSource, "utf8"),
    fs.readFile(mainSource, "utf8"),
  ]);
  assert.doesNotMatch(html, /id="stage-detail"/);
  assert.match(html, /data-i18n="author"/);
  assert.match(renderer, /nameLine\.append\(name, version\)/);
  assert.match(renderer, /row\.append\(nameCell, author, description/);
  assert.match(css, /\.mod-row\s*\{[^}]*padding-top: 15px;[^}]*padding-bottom: 15px;/s);
  assert.match(css, /body\s*\{[^}]*display: flex;[^}]*height: 100vh;[^}]*overflow: hidden;/s);
  assert.match(css, /\.app-titlebar\s*\{[^}]*position: relative;[^}]*flex: 0 0 48px;/s);
  assert.match(css, /\.workspace\s*\{[^}]*flex: 1 1 auto;[^}]*overflow: auto;/s);
  assert.match(renderer, /workspace\.addEventListener\(\s*"wheel"[\s\S]*event\.preventDefault\(\)[\s\S]*workspace\.scrollBy/);
  assert.match(main, /await manager\.ensureSystemMods\(false, true\);/);
});

test("new imports enable automatically and updates preserve disabled state", async (t) => {
  const root = await fs.mkdtemp(path.join(os.tmpdir(), "scde-auto-enable-"));
  t.after(() => fs.rm(root, { recursive: true, force: true }));
  const manager = new ModManager(path.join(root, "manager-data"));
  await manager.setGameDirectory(await makeGame(root));
  await manager.prepareStage();
  const first = await makePackage(root, { id: "auto.first", name: "First", version: "1.0" },
    { "auto-first.txt": "first" });
  const second = await makePackage(root, { id: "auto.second", name: "Second", version: "1.0" },
    { "auto-second.txt": "second" });
  let result = await manager.installPackages([first, second]);
  assert.deepEqual(result.state.enabledMods, ["auto.first", "auto.second"]);
  assert.equal(result.state.stageReady, true);
  assert.equal(await fs.readFile(path.join(manager.stageDir, "auto-first.txt"), "utf8"), "first");
  await manager.setEnabled("auto.first", false);
  const update = await makePackage(root, { id: "auto.first", name: "First", version: "2.0" },
    { "auto-first.txt": "updated" });
  result = await manager.installPackages([update, second]);
  assert.deepEqual(result.state.enabledMods, ["auto.second"]);
  assert.equal(result.state.mods.find((mod) => mod.id === "auto.first").version, "2.0");
  await assert.rejects(fs.access(path.join(manager.stageDir, "auto-first.txt")));
});

test("batch auto-enable resolves dependencies even when the plugin is imported first", async (t) => {
  const root = await fs.mkdtemp(path.join(os.tmpdir(), "scde-auto-dependencies-"));
  t.after(() => fs.rm(root, { recursive: true, force: true }));
  const manager = new ModManager(root);
  const plugin = await makePackage(root, { id: "auto.plugin", name: "Plugin", version: "1.0",
    dependencies: [{ id: "auto.runtime", version: "1.0" }] }, { "plugin.txt": "plugin" });
  const runtime = await makePackage(root, { id: "auto.runtime", name: "Runtime", version: "1.0" },
    { "runtime.txt": "runtime" });
  const result = await manager.installPackages([plugin, runtime]);
  assert.deepEqual(result.state.enabledMods, ["auto.runtime", "auto.plugin"]);
  assert.deepEqual(result.activationFailures, []);
});

test("invalid dependencies leave new imports disabled and report activation failures", async (t) => {
  const root = await fs.mkdtemp(path.join(os.tmpdir(), "scde-auto-missing-"));
  t.after(() => fs.rm(root, { recursive: true, force: true }));
  const manager = new ModManager(root);
  const plugin = await makePackage(root, { id: "auto.plugin", name: "Plugin", version: "1.0",
    dependencies: [{ id: "auto.runtime", version: "1.0" }] }, { "plugin.txt": "plugin" });
  const result = await manager.installPackages([plugin]);
  assert.equal(result.imported.length, 1);
  assert.deepEqual(result.failures, []);
  assert.deepEqual(result.state.enabledMods, []);
  assert.equal(result.activationFailures.length, 1);
  assert.match(result.activationFailures[0].error, /auto.runtime/);
});

test("multiple Mod packages are imported in one operation and failures are reported", async (t) => {
  const root = await fs.mkdtemp(path.join(os.tmpdir(), "scde-manager-multi-import-"));
  t.after(() => fs.rm(root, { recursive: true, force: true }));
  const manager = new ModManager(path.join(root, "manager-data"));

  const first = await makePackage(
    root,
    { id: "batch.first", name: "Batch First", version: "1.0.0" },
    { "data/first.dat": "first" }
  );
  const second = await makePackage(
    root,
    { id: "batch.second", name: "Batch Second", version: "2.0.0" },
    { "data/second.dat": "second" }
  );
  const invalid = path.join(root, "invalid.scdemod");
  await fs.writeFile(invalid, "not a zip archive");

  const result = await manager.installPackages([first, invalid, second]);

  assert.deepEqual(
    result.imported.map((mod) => mod.id),
    ["batch.first", "batch.second"]
  );
  assert.equal(result.failures.length, 1);
  assert.equal(result.failures[0].file, "invalid.scdemod");
  assert.deepEqual(
    result.state.mods.map((mod) => mod.id).sort(),
    ["batch.first", "batch.second"]
  );
});

test("enabling and disabling a mod changes only the staged game", async (t) => {
  const root = await fs.mkdtemp(path.join(os.tmpdir(), "scde-manager-test-"));
  t.after(() => fs.rm(root, { recursive: true, force: true }));
  const gameDir = await makeGame(root);
  const manager = new ModManager(path.join(root, "manager-data"));
  const packagePath = await makePackage(
    root,
    {
      id: "test.rules",
      name: "Test Rules",
      version: "1.0.0",
      author: "Tests",
      description: "Changes a staged rules file",
    },
    {
      "data/rules.dat": "modded rules",
      "_scde_mods/test.txt": "installed",
    }
  );

  await manager.setGameDirectory(gameDir);
  await manager.installPackage(packagePath);
  await manager.setEnabled("test.rules", true);
  let state = await manager.prepareStage();

  assert.equal(await fs.readFile(path.join(gameDir, "data", "rules.dat"), "utf8"), "original rules");
  assert.equal(await fs.readFile(path.join(manager.stageDir, "data", "rules.dat"), "utf8"), "modded rules");
  assert.equal(await fs.readFile(path.join(manager.stageDir, "_scde_mods", "test.txt"), "utf8"), "installed");
  assert.equal(state.stageReady, true);
  assert.equal(state.activeFileCount, 2);

  state = await manager.setEnabled("test.rules", false);
  assert.equal(await fs.readFile(path.join(gameDir, "data", "rules.dat"), "utf8"), "original rules");
  assert.equal(await fs.readFile(path.join(manager.stageDir, "data", "rules.dat"), "utf8"), "original rules");
  await assert.rejects(fs.access(path.join(manager.stageDir, "_scde_mods", "test.txt")));
  assert.deepEqual(state.enabledMods, []);
});

test("launch locks immediately, rejects Mod changes, and unlocks on game exit", async (t) => {
  const root = await fs.mkdtemp(path.join(os.tmpdir(), "scde-manager-launch-lock-"));
  t.after(() => fs.rm(root, { recursive: true, force: true }));
  const gameDir = await makeGame(root);
  const children = [];
  const locks = [];
  let allowSteamCheck;
  const steamCheck = new Promise((resolve) => { allowSteamCheck = resolve; });
  const manager = new ModManager(path.join(root, "manager-data"), {
    isSteamRunning: () => steamCheck,
    onGameLockChanged: (locked) => locks.push(locked),
    spawn: () => {
      const child = new EventEmitter();
      child.pid = 24683 + children.length;
      child.unref = () => {};
      children.push(child);
      process.nextTick(() => child.emit("spawn"));
      return child;
    },
  });
  await manager.setGameDirectory(gameDir);
  const packagePath = await makePackage(root,
    { id: "test.lock", name: "Lock", version: "1.0.0" },
    { "lock.txt": "locked" });
  await manager.installPackage(packagePath);
  await manager.setEnabled("test.lock", true);
  const launching = manager.launch();
  assert.equal(manager.gameLocked, true);
  for (const action of [
    () => manager.launch(),
    () => manager.setEnabled("test.lock", false),
    () => manager.moveMod("test.lock", 1),
    () => manager.removeMod("test.lock"),
    () => manager.installPackages([packagePath]),
    () => manager.setGameDirectory(gameDir),
  ]) await assert.rejects(action(), /GAME_RUNNING/);
  allowSteamCheck(true);
  const result = await launching;
  assert.equal(result.state.gameLocked, true);
  assert.equal(result.state.appVersion, require("../package.json").version);
  await assert.rejects(manager.launch(), /GAME_RUNNING/);
  await assert.rejects(manager.setEnabled("test.lock", false), /GAME_RUNNING/);
  await assert.rejects(manager.moveMod("test.lock", 1), /GAME_RUNNING/);
  children[0].emit("exit", 0, null);
  assert.equal((await manager.getState()).gameLocked, false);
  await manager.setEnabled("test.lock", false);
  await manager.moveMod("test.lock", 1);
  manager.onGameStarting = async () => { throw new Error("Status display unavailable"); };
  manager.onGameExited = () => { throw new Error("Status window already closed"); };
  await manager.launch();
  assert.equal(children.length, 2);
  children[1].emit("exit", 1, null);
  assert.deepEqual(locks, [true, false, true, false]);
  await new Promise((resolve) => setImmediate(resolve));
});

test("Steam, validation, and process spawn failures release the launch lock", async (t) => {
  const root = await fs.mkdtemp(path.join(os.tmpdir(), "scde-manager-launch-failure-"));
  t.after(() => fs.rm(root, { recursive: true, force: true }));
  const manager = new ModManager(path.join(root, "manager-data"), {
    isSteamRunning: async () => false,
  });
  await assert.rejects(manager.launch(), /STEAM_NOT_RUNNING/);
  assert.equal(manager.gameLocked, false);
  manager.runSteamCheck = async () => true;
  await assert.rejects(manager.launch());
  assert.equal(manager.gameLocked, false);
  await manager.setGameDirectory(await makeGame(root));
  manager.spawnProcess = () => {
    const child = new EventEmitter();
    process.nextTick(() => child.emit("error", new Error("spawn failed")));
    return child;
  };
  await assert.rejects(manager.launch(), /spawn failed/);
  assert.equal(manager.gameLocked, false);
});

test("BepInEx metadata is always English, including previously installed Chinese manifests", async (t) => {
  const root = await fs.mkdtemp(path.join(os.tmpdir(), "scde-manager-runtime-english-"));
  t.after(() => fs.rm(root, { recursive: true, force: true }));
  const manager = new ModManager(root);
  const folder = path.join(manager.modsRoot, "bepinex-runtime");
  await fs.mkdir(folder, { recursive: true });
  await fs.writeFile(path.join(folder, "manifest.json"), JSON.stringify({
    id: "bepinex-runtime", name: "BepInEx 运行库", version: "5.4.23.5",
    description: "供 SCDE 运行时代码 Mod 共用的 BepInEx 5 Mono x64 运行库。",
  }));
  for (const language of ["zh-CN", "en"]) {
    await manager.configStore.update((config) => ({ ...config, language }));
    const mod = (await manager.getState()).mods[0];
    assert.equal(mod.name, "BepInEx Runtime");
    assert.equal(mod.description, "Shared BepInEx 5 Mono x64 runtime for SCDE runtime code Mods.");
  }
});

test("launch repairs the staged environment when its single deployment probe is missing", async (t) => {
  const root = await fs.mkdtemp(path.join(os.tmpdir(), "scde-manager-launch-repair-"));
  t.after(() => fs.rm(root, { recursive: true, force: true }));
  const gameDir = await makeGame(root);
  let spawned = false;
  const fakeChild = new EventEmitter();
  fakeChild.pid = 24680;
  fakeChild.unref = () => {};
  const manager = new ModManager(path.join(root, "manager-data"), {
    isSteamRunning: async () => true,
    spawn: (_executable, _args, options) => {
      spawned = true;
      assert.match(options.env.SCDEModManagerLaunchId, /^\d+-\d+$/);
      process.nextTick(() => fakeChild.emit("spawn"));
      return fakeChild;
    },
  });
  const packagePath = await makePackage(
    root,
    { id: "test.launch", name: "Launch Repair", version: "1.0.0" },
    { "BepInEx/plugins/LaunchRepair/bootstrap.bin": "required bootstrap bytes" }
  );

  await manager.setGameDirectory(gameDir);
  await manager.installPackage(packagePath);
  await manager.setEnabled("test.launch", true);
  await manager.prepareStage();
  const stagedProbe = path.join(manager.stageDir, "BepInEx/plugins/LaunchRepair/bootstrap.bin");
  await fs.rm(stagedProbe);

  assert.equal((await manager.getState()).stageReady, false);
  const result = await manager.launch();

  assert.equal(spawned, true);
  assert.equal(result.pid, 24680);
  assert.equal(result.state.stageReady, true);
  assert.equal(await fs.readFile(stagedProbe, "utf8"), "required bootstrap bytes");
  assert.match(
    await fs.readFile(path.join(manager.dataRoot, "launch-history.log"), "utf8"),
    /deployment=receipt-current settings=disabled mods=test\.launch@1\.0\.0/
  );
});

test("launch does not rewrite intact Mod payloads when the deployment receipt is current", async (t) => {
  const root = await fs.mkdtemp(path.join(os.tmpdir(), "scde-manager-launch-fast-path-"));
  t.after(() => fs.rm(root, { recursive: true, force: true }));
  const gameDir = await makeGame(root);
  const fakeChild = new EventEmitter();
  fakeChild.pid = 24681;
  fakeChild.unref = () => {};
  const manager = new ModManager(path.join(root, "manager-data"), {
    isSteamRunning: async () => true,
    spawn: () => {
      process.nextTick(() => fakeChild.emit("spawn"));
      return fakeChild;
    },
  });
  const packagePath = await makePackage(
    root,
    { id: "test.fast-launch", name: "Fast Launch", version: "1.0.0" },
    {
      "BepInEx/plugins/FastLaunch/bootstrap.bin": "bootstrap sentinel",
      "BepInEx/plugins/FastLaunch/large-map-pack.bin": Buffer.alloc(1024 * 1024, 7),
    }
  );

  await manager.setGameDirectory(gameDir);
  await manager.installPackage(packagePath);
  await manager.setEnabled("test.fast-launch", true);
  await manager.prepareStage();
  const stagedPayload = path.join(
    manager.stageDir,
    "BepInEx",
    "plugins",
    "FastLaunch",
    "large-map-pack.bin"
  );
  const fixedTime = new Date("2001-02-03T04:05:06.000Z");
  await fs.utimes(stagedPayload, fixedTime, fixedTime);

  const result = await manager.launch();
  const afterLaunch = await fs.stat(stagedPayload);

  assert.equal(result.pid, 24681);
  assert.equal(result.state.stageReady, true);
  assert.equal(afterLaunch.mtimeMs, fixedTime.getTime());
});

test("launch protects settings for the active Steam account without scanning Mod payloads", async (t) => {
  const root = await fs.mkdtemp(path.join(os.tmpdir(), "scde-manager-launch-settings-"));
  t.after(() => fs.rm(root, { recursive: true, force: true }));
  const gameDir = await makeGame(root);
  const settingsPath = path.join(root, "LocalLow", "settings.cfg");
  await fs.mkdir(path.dirname(settingsPath), { recursive: true });
  await fs.writeFile(settingsPath, completeSettings(false));
  const fakeChild = new EventEmitter();
  fakeChild.pid = 24682;
  fakeChild.unref = () => {};
  let launchEnvironment;
  const manager = new ModManager(path.join(root, "manager-data"), {
    isSteamRunning: async () => true,
    getActiveSteamUserId: async () => "327960465",
    gameSettingsPath: settingsPath,
    settingsSleep: async () => {},
    spawn: (_executable, _args, options) => {
      launchEnvironment = options.env;
      process.nextTick(() => fakeChild.emit("spawn"));
      return fakeChild;
    },
  });

  await manager.setGameDirectory(gameDir);
  await manager.prepareStage();
  const result = await manager.launch();
  const backupPath = path.join(
    manager.dataRoot,
    "user-settings",
    "327960465",
    "settings.cfg.last-good"
  );

  assert.equal(result.pid, 24682);
  assert.equal(launchEnvironment.SCDEModManagerDataRoot, manager.dataRoot);
  assert.equal(launchEnvironment.SCDEModManagerSettingsBackup, backupPath);
  assert.deepEqual(await fs.readFile(backupPath), completeSettings(false));
  assert.equal((await manager.configStore.load()).lastSteamUserId, "327960465");
  assert.match(
    await fs.readFile(path.join(manager.dataRoot, "launch-history.log"), "utf8"),
    /deployment=receipt-current settings=validated mods=/
  );
});

test("deployment invalidates only the staged BepInEx plugin discovery cache", async (t) => {
  const root = await fs.mkdtemp(path.join(os.tmpdir(), "scde-manager-plugin-cache-"));
  t.after(() => fs.rm(root, { recursive: true, force: true }));
  const gameDir = await makeGame(root);
  const originalCache = path.join(
    gameDir,
    "BepInEx",
    "cache",
    "chainloader_typeloader.dat"
  );
  await fs.mkdir(path.dirname(originalCache), { recursive: true });
  await fs.writeFile(originalCache, "original cache");

  const manager = new ModManager(path.join(root, "manager-data"));
  const packagePath = await makePackage(
    root,
    { id: "plugin.mod", name: "Plugin", version: "1.0.0" },
    { "BepInEx/plugins/Plugin/plugin.dll": "plugin" }
  );

  await manager.setGameDirectory(gameDir);
  await manager.installPackage(packagePath);
  await manager.setEnabled("plugin.mod", true);
  await manager.prepareStage();

  const stagedCache = path.join(
    manager.stageDir,
    "BepInEx",
    "cache",
    "chainloader_typeloader.dat"
  );
  assert.equal(await fs.readFile(originalCache, "utf8"), "original cache");
  await assert.rejects(fs.access(stagedCache));

  await fs.mkdir(path.dirname(stagedCache), { recursive: true });
  await fs.writeFile(stagedCache, "stale zero-plugin result");
  await manager.redeploy();

  await assert.rejects(fs.access(stagedCache));
  assert.equal(await fs.readFile(originalCache, "utf8"), "original cache");
});

test("later enabled mods win conflicts and the conflict is reported", async (t) => {
  const root = await fs.mkdtemp(path.join(os.tmpdir(), "scde-manager-conflict-"));
  t.after(() => fs.rm(root, { recursive: true, force: true }));
  const gameDir = await makeGame(root);
  const manager = new ModManager(path.join(root, "manager-data"));

  const first = await makePackage(
    root,
    { id: "first.mod", name: "First", version: "1.0.0" },
    { "data/rules.dat": "first" }
  );
  const second = await makePackage(
    root,
    { id: "second.mod", name: "Second", version: "1.0.0" },
    { "data/rules.dat": "second" }
  );

  await manager.setGameDirectory(gameDir);
  await manager.installPackage(first);
  await manager.installPackage(second);
  await manager.setEnabled("first.mod", true);
  await manager.setEnabled("second.mod", true);
  const state = await manager.prepareStage();

  assert.equal(await fs.readFile(path.join(manager.stageDir, "data", "rules.dat"), "utf8"), "second");
  assert.deepEqual(state.lastConflicts, [
    { path: "data/rules.dat", overwritten: "first.mod", winner: "second.mod" },
  ]);
});

test("moving a Mod changes the real deployment and conflict priority", async (t) => {
  const root = await fs.mkdtemp(path.join(os.tmpdir(), "scde-manager-order-"));
  t.after(() => fs.rm(root, { recursive: true, force: true }));
  const gameDir = await makeGame(root);
  const manager = new ModManager(path.join(root, "manager-data"));

  const first = await makePackage(
    root,
    { id: "order.first", name: "First", version: "1.0.0" },
    { "data/rules.dat": "first" }
  );
  const second = await makePackage(
    root,
    { id: "order.second", name: "Second", version: "1.0.0" },
    { "data/rules.dat": "second" }
  );

  await manager.setGameDirectory(gameDir);
  await manager.installPackages([first, second]);
  await manager.setEnabled("order.first", true);
  await manager.setEnabled("order.second", true);
  await manager.prepareStage();

  const state = await manager.moveMod("order.second", -1);

  assert.deepEqual(
    state.mods.map((mod) => mod.id),
    ["order.second", "order.first"]
  );
  assert.deepEqual(state.enabledMods, ["order.second", "order.first"]);
  assert.equal(await fs.readFile(path.join(manager.stageDir, "data", "rules.dat"), "utf8"), "first");
  assert.deepEqual(state.lastConflicts, [
    { path: "data/rules.dat", overwritten: "order.second", winner: "order.first" },
  ]);
});

test("identical files from multiple mods are shared without a conflict", async (t) => {
  const root = await fs.mkdtemp(path.join(os.tmpdir(), "scde-manager-shared-"));
  t.after(() => fs.rm(root, { recursive: true, force: true }));
  const gameDir = await makeGame(root);
  const manager = new ModManager(path.join(root, "manager-data"));

  const first = await makePackage(
    root,
    { id: "first.mod", name: "First", version: "1.0.0" },
    {
      "BepInEx/plugins/shared-assets/shared-loader.dll": "same runtime bytes",
      "BepInEx/plugins/first/plugin.dll": "first plugin",
    }
  );
  const second = await makePackage(
    root,
    { id: "second.mod", name: "Second", version: "1.0.0" },
    {
      "BepInEx/plugins/shared-assets/shared-loader.dll": "same runtime bytes",
      "BepInEx/plugins/second/plugin.dll": "second plugin",
    }
  );

  await manager.setGameDirectory(gameDir);
  await manager.installPackage(first);
  await manager.installPackage(second);
  await manager.setEnabled("first.mod", true);
  await manager.setEnabled("second.mod", true);
  const state = await manager.prepareStage();

  assert.deepEqual(state.lastConflicts, []);
  assert.equal(state.activeFileCount, 3);
  assert.equal(
    await fs.readFile(path.join(manager.stageDir, "BepInEx", "plugins", "shared-assets", "shared-loader.dll"), "utf8"),
    "same runtime bytes"
  );
  assert.equal(
    await fs.readFile(path.join(manager.stageDir, "BepInEx", "plugins", "first", "plugin.dll"), "utf8"),
    "first plugin"
  );
  assert.equal(
    await fs.readFile(path.join(manager.stageDir, "BepInEx", "plugins", "second", "plugin.dll"), "utf8"),
    "second plugin"
  );
});

test("enabling a plugin enables its runtime first and protects the dependency", async (t) => {
  const root = await fs.mkdtemp(path.join(os.tmpdir(), "scde-manager-dependency-"));
  t.after(() => fs.rm(root, { recursive: true, force: true }));
  const gameDir = await makeGame(root);
  const manager = new ModManager(path.join(root, "manager-data"));

  const runtime = await makePackage(
    root,
    { id: "bepinex-runtime", name: "BepInEx Runtime", version: "5.4.23.5" },
    { "BepInEx/core/BepInEx.dll": "runtime" }
  );
  const plugin = await makePackage(
    root,
    {
      id: "plugin.mod",
      name: "Plugin",
      version: "1.0.0",
      dependencies: [{ id: "bepinex-runtime", version: "5.4.23.5" }],
    },
    { "BepInEx/plugins/Plugin/plugin.dll": "plugin" }
  );

  await manager.setGameDirectory(gameDir);
  await manager.installPackage(runtime);
  await manager.installPackage(plugin);
  let state = await manager.setEnabled("plugin.mod", true);
  assert.deepEqual(state.enabledMods, ["bepinex-runtime", "plugin.mod"]);

  await assert.rejects(
    () => manager.moveMod("bepinex-runtime", 1),
    /依赖项晚于使用它的 Mod/
  );
  state = await manager.getState();
  assert.deepEqual(
    state.mods.map((mod) => mod.id),
    ["bepinex-runtime", "plugin.mod"]
  );

  state = await manager.prepareStage();
  assert.deepEqual(state.lastConflicts, []);
  assert.equal(
    await fs.readFile(path.join(manager.stageDir, "BepInEx", "core", "BepInEx.dll"), "utf8"),
    "runtime"
  );
  assert.equal(
    await fs.readFile(path.join(manager.stageDir, "BepInEx", "plugins", "Plugin", "plugin.dll"), "utf8"),
    "plugin"
  );

  await assert.rejects(() => manager.setEnabled("bepinex-runtime", false), /正被以下 Mod 使用/);
  await assert.rejects(() => manager.removeMod("bepinex-runtime"), /仍被以下已安装 Mod 依赖/);

  state = await manager.setEnabled("plugin.mod", false);
  assert.deepEqual(state.enabledMods, ["bepinex-runtime"]);
  state = await manager.setEnabled("bepinex-runtime", false);
  assert.deepEqual(state.enabledMods, []);
});

test("radar inverse projection stretches the playable logic diamond to the full minimap", () => {
  const p00 = { x: 0, y: 1 };
  const axisU = { x: 1, y: -1 };
  const axisV = { x: -1, y: -1 };
  const determinant = axisU.x * axisV.y - axisU.y * axisV.x;
  const inverse = ({ x, y }) => {
    const dx = x - p00.x;
    const dy = y - p00.y;
    return {
      u: (dx * axisV.y - dy * axisV.x) / determinant,
      v: (axisU.x * dy - axisU.y * dx) / determinant,
    };
  };

  const assertCorner = (point, expectedU, expectedV) => {
    const actual = inverse(point);
    assert.ok(Math.abs(actual.u - expectedU) < 1e-9);
    assert.ok(Math.abs(actual.v - expectedV) < 1e-9);
  };
  assertCorner({ x: 0, y: 0 }, 0.5, 0.5);
  assertCorner({ x: -0.5, y: 0.5 }, 0, 0.5);
  assertCorner({ x: 0.5, y: 0.5 }, 0.5, 0);
  assertCorner({ x: -0.5, y: -0.5 }, 0.5, 1);
  assertCorner({ x: 0.5, y: -0.5 }, 1, 0.5);
});

