using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Threading;
using BepInEx;
using HarmonyLib;
using Steamworks;
using UnityEngine;

namespace JiuyeAyan.SCDEMultiplayerCompatibility
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency("000shcdese", BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class SCDEMultiplayerCompatibilityPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.jiuyeayan.scde.multiplayer-compatibility";
        public const string PluginName = "SCDE Multiplayer Mod Compatibility";
        public const string PluginVersion = "0.3.1";

        private const string MemberProfileKey = "scdemm_profile_v3";
        private const string LobbyProfileKey = "scdemm_host_profile_v3";
        private const string LobbyManifestCountKey = "scdemm_mod_count_v3";
        private const string LobbyManifestChunkPrefix = "scdemm_mods_v3_";
        private const int LobbyManifestChunkSize = 3000;
        private const int MaxLobbyManifestChunks = 16;
        private const float PollIntervalSeconds = 0.75f;
        private const float MemberHandshakeGraceSeconds = 3f;
        private const float PendingJoinWaitSeconds = 12f;
        private const float PendingJoinMinimumDisplaySeconds = 0.5f;
        private const float OverlayDurationSeconds = 5f;
        private static readonly FieldInfo PlatformInstanceField = typeof(Platform_Multiplayer).GetField(
            "instance",
            BindingFlags.Static | BindingFlags.NonPublic
        );

        internal static SCDEMultiplayerCompatibilityPlugin Instance { get; private set; }

        private readonly Dictionary<ulong, float> missingProfileSince =
            new Dictionary<ulong, float>();
        private readonly HashSet<ulong> handledMembers = new HashSet<ulong>();
        private readonly HashSet<ulong> activeMemberIds = new HashSet<ulong>();
        private readonly List<ulong> staleMemberIds = new List<ulong>();
        private Harmony harmony;
        private CompatibilityRuntime runtime;
        private string localToken = "";
        private string localLobbyProfile = "";
        private readonly List<ProfileMod> localMods = new List<ProfileMod>();
        private readonly List<ProfileMod> managerMods = new List<ProfileMod>();
        private bool managerProfileLoaded;
        private string runtimeProfileError = "";
        private bool useChinese;
        private bool singlePlayerSkirmishSetup;
        private ulong currentLobbyId;
        private ulong publishedMemberLobbyId;
        private ulong publishedHostLobbyId;
        private ulong handledClientLobbyId;
        private Platform_Multiplayer pendingJoinPlatform;
        private Platform_Multiplayer.MPLobby pendingJoinLobby;
        private Action pendingLobbyJoined;
        private Action<string, string, int> pendingLobbyChat;
        private bool pendingKeepAutoJoinLobby;
        private float pendingJoinExpiresAt;
        private float pendingJoinProcessAt;
        private ulong allowNextJoinLobbyId;
        private int joinGeneration;
        private ulong localProfileErrorLobbyId;
        private float nextPollAt;
        private bool lobbyCompatible;
        private string overlayMessage = "";
        private float overlayUntil;
        private bool overlayIsVerification;
        private Vector2 overlayScroll;
        private Texture2D overlayBackground;
        private GUIStyle overlayTextStyle;
        private readonly GUIContent overlayContent = new GUIContent();
        private bool pluginHostDestroyed;
        private bool runtimeSurvivalLogged;

        private void Awake()
        {
            if (!ReferenceEquals(Instance, null) && !ReferenceEquals(Instance, this))
            {
                Logger.LogWarning(
                    "A persistent multiplayer compatibility runtime already exists; duplicate plugin host ignored."
                );
                enabled = false;
                return;
            }

            Instance = this;
            useChinese = CultureInfo.InstalledUICulture.Name.StartsWith(
                "zh",
                StringComparison.OrdinalIgnoreCase
            );
            LoadLocalProfile();
            harmony = new Harmony(PluginGuid);
            if (managerProfileLoaded) ScriptExtenderBridge.InstallManagedPolicy(harmony);
            harmony.PatchAll(typeof(SCDEMultiplayerCompatibilityPlugin).Assembly);
            CreateDetachedRuntime();
            Logger.LogInfo(
                "Multiplayer compatibility checker initialized; SCDE settings load guard enabled."
            );
        }

        internal static void LogSettingsGuardInfo(string message)
        {
            SCDEMultiplayerCompatibilityPlugin plugin = Instance;
            if (!ReferenceEquals(plugin, null)) plugin.Logger.LogInfo(message);
        }

        internal static void LogSettingsGuardWarning(string message)
        {
            SCDEMultiplayerCompatibilityPlugin plugin = Instance;
            if (!ReferenceEquals(plugin, null)) plugin.Logger.LogWarning(message);
        }

        private void OnDestroy()
        {
            if (ReferenceEquals(runtime, null)) return;
            pluginHostDestroyed = true;
            Logger.LogInfo(
                "BepInEx plugin host was destroyed; the detached multiplayer compatibility runtime remains active across game scenes."
            );
        }

        private void CreateDetachedRuntime()
        {
            GameObject runtimeObject = new GameObject("SCDE Multiplayer Compatibility Runtime");
            runtimeObject.hideFlags = HideFlags.HideAndDontSave;
            DontDestroyOnLoad(runtimeObject);
            runtime = runtimeObject.AddComponent<CompatibilityRuntime>();
            runtime.Owner = this;
        }

        internal void RuntimeUpdate()
        {
            if (pluginHostDestroyed && !runtimeSurvivalLogged)
            {
                runtimeSurvivalLogged = true;
                Logger.LogInfo(
                    "MULTIPLAYER_COMPATIBILITY_RUNTIME_ACTIVE: Lobby checks survived the plugin host scene transition."
                );
            }
            if (!String.IsNullOrEmpty(overlayMessage) && Time.unscaledTime > overlayUntil)
            {
                ClearOverlay();
            }
            if (Time.unscaledTime < nextPollAt) return;
            nextPollAt = Time.unscaledTime + PollIntervalSeconds;
            StartupReadyReporter.Tick();
            ProcessPendingJoin();
            PollLobby();
        }

        internal void RuntimeDestroyed(CompatibilityRuntime destroyedRuntime)
        {
            if (!ReferenceEquals(runtime, destroyedRuntime)) return;
            runtime = null;
            if (harmony != null) harmony.UnpatchSelf();
            if (overlayBackground != null) Destroy(overlayBackground);
            overlayBackground = null;
            overlayTextStyle = null;
            overlayContent.text = "";
            ClearPendingJoin();
            if (ReferenceEquals(Instance, this)) Instance = null;
        }

        private void LoadLocalProfile()
        {
            string profilePath = Path.Combine(
                Paths.GameRootPath,
                "_scde_manager",
                "active-mods.lobby"
            );
            if (!File.Exists(profilePath))
            {
                Logger.LogError("Manager compatibility profile is missing: " + profilePath);
                localToken = "";
                return;
            }

            localLobbyProfile = File.ReadAllText(profilePath).TrimEnd('\r', '\n');
            ProfileDocument profile;
            if (!TryParseProfile(localLobbyProfile, out profile))
            {
                Logger.LogError("Manager compatibility profile is invalid.");
                localToken = "";
                return;
            }

            managerMods.Clear();
            managerMods.AddRange(profile.Mods);
            managerProfileLoaded = true;
            localToken = ""; // BepInEx is still loading plugins; capture runtime state only at the lobby boundary.
            Logger.LogInfo("Loaded manager Mod profile " + profile.Token.Substring(0, 14) + "...");
        }

        private bool RefreshRuntimeProfile()
        {
            if (!managerProfileLoaded) return false;
            try
            {
                bool requireExtender = managerMods.Exists(mod => mod.Id == ScriptExtenderBridge.PackageId);
                List<ProfileMod> merged = RuntimeProfile.Merge(managerMods, ScriptExtenderBridge.Read(requireExtender));
                string fingerprint = RuntimeProfile.Fingerprint(merged);
                string token = "3:" + fingerprint;
                string document = RuntimeProfile.Serialize(merged, fingerprint);
                if (document.Length > LobbyManifestChunkSize * MaxLobbyManifestChunks)
                    throw new InvalidOperationException("Combined Mod profile exceeds the lobby metadata limit.");
                if (localToken != token)
                {
                    localToken = token;
                    localLobbyProfile = document;
                    localMods.Clear();
                    localMods.AddRange(merged);
                    publishedMemberLobbyId = publishedHostLobbyId = 0;
                    lobbyCompatible = false;
                    handledClientLobbyId = 0;
                    Logger.LogInfo("SE_BRIDGE_PROFILE_READY: " + merged.Count + " required manager/runtime entries; " + token);
                }
                runtimeProfileError = "";
                return true;
            }
            catch (Exception error)
            {
                localToken = "";
                lobbyCompatible = false;
                if (runtimeProfileError != error.Message)
                    Logger.LogError("SE_BRIDGE_PROFILE_FAILED: " + error);
                runtimeProfileError = error.Message;
                return false;
            }
        }

        private static bool TryParseProfile(string text, out ProfileDocument profile)
        {
            profile = null;
            if (String.IsNullOrEmpty(text) || text.Length > LobbyManifestChunkSize * MaxLobbyManifestChunks) return false;

            string[] lines = text.Replace("\r", "").Split('\n');
            string[] header = lines[0].Split('|');
            if (header.Length != 2 || (header[0] != "SCDEMM2" && header[0] != "SCDEMM3") || header[1].Length != 64)
                return false;

            for (int index = 0; index < header[1].Length; index++)
            {
                char value = header[1][index];
                if (!Uri.IsHexDigit(value)) return false;
            }

            ProfileDocument parsed = new ProfileDocument();
            parsed.Token = header[0].Substring(6) + ":" + header[1].ToLowerInvariant();
            for (int index = 1; index < lines.Length; index++)
            {
                if (String.IsNullOrWhiteSpace(lines[index])) continue;
                string[] fields = lines[index].Split('|');
                if (fields.Length != 3) return false;
                try
                {
                    parsed.Mods.Add(new ProfileMod(
                        DecodeProfileField(fields[0]),
                        DecodeProfileField(fields[1]),
                        DecodeProfileField(fields[2])
                    ));
                }
                catch (FormatException)
                {
                    return false;
                }
            }
            try
            {
                if (RuntimeProfile.Fingerprint(parsed.Mods) != header[1].ToLowerInvariant()) return false;
            }
            catch (FormatException) { return false; }
            profile = parsed;
            return true;
        }

        private static string DecodeProfileField(string value)
        {
            return Encoding.UTF8.GetString(Convert.FromBase64String(value));
        }

        private Platform_Multiplayer GetPlatform()
        {
            if (PlatformInstanceField == null) return null;
            return PlatformInstanceField.GetValue(null) as Platform_Multiplayer;
        }

        private void PollLobby()
        {
            try
            {
                if (singlePlayerSkirmishSetup)
                {
                    ResetLobbyState();
                    return;
                }
                Platform_Multiplayer platform = GetPlatform();
                Platform_Multiplayer.MPLobby lobby = platform == null ? null : platform.GetActiveLobby();
                if (lobby == null)
                {
                    ResetLobbyState();
                    return;
                }

                ulong lobbyId = lobby.id.m_SteamID;
                if (currentLobbyId != lobbyId)
                {
                    currentLobbyId = lobbyId;
                    handledClientLobbyId = 0;
                    localProfileErrorLobbyId = 0;
                    missingProfileSince.Clear();
                    handledMembers.Clear();
                    lobbyCompatible = false;
                    Logger.LogInfo("Checking multiplayer lobby " + lobbyId + ".");
                }

                if (!RefreshRuntimeProfile())
                {
                    lobbyCompatible = false;
                    // Retract a previously valid claim if the live registry becomes unreadable.
                    if (publishedMemberLobbyId == lobbyId)
                    {
                        SteamMatchmaking.SetLobbyMemberData(lobby.id, MemberProfileKey, "");
                        publishedMemberLobbyId = 0;
                    }
                    if (publishedHostLobbyId == lobbyId && SteamMatchmaking.SetLobbyData(lobby.id, LobbyProfileKey, ""))
                        publishedHostLobbyId = 0;
                    if (localProfileErrorLobbyId != lobbyId)
                    {
                        localProfileErrorLobbyId = lobbyId;
                        ShowOverlay(
                            L(
                                "无法验证联机 Mod：管理器清单或 Script Extender 运行时未就绪。",
                                "Cannot verify multiplayer Mods: the manager profile or Script Extender runtime is not ready."
                            )
                        );
                    }
                    return;
                }

                if (publishedMemberLobbyId != lobbyId)
                {
                    SteamMatchmaking.SetLobbyMemberData(lobby.id, MemberProfileKey, localToken);
                    publishedMemberLobbyId = lobbyId;
                }
                bool localOwnsLobby =
                    SteamMatchmaking.GetLobbyOwner(lobby.id) == SteamUser.GetSteamID();
                bool isLocalHost = lobby.isHost || localOwnsLobby;
                if (isLocalHost)
                {
                    if (publishedHostLobbyId != lobbyId)
                    {
                        if (PublishHostProfile(lobby.id)) publishedHostLobbyId = lobbyId;
                    }
                    if (publishedHostLobbyId != lobbyId) { lobbyCompatible = false; return; }
                    ValidateMembersAsHost(platform, lobby);
                }
                else
                {
                    ValidateHostAsClient(platform, lobby);
                }
            }
            catch (Exception error)
            {
                lobbyCompatible = false;
                Logger.LogError("Lobby compatibility check failed: " + error);
            }
        }

        private bool PublishHostProfile(CSteamID lobbyId)
        {
            int chunkCount = (localLobbyProfile.Length + LobbyManifestChunkSize - 1) /
                             LobbyManifestChunkSize;
            if (chunkCount < 1 || chunkCount > MaxLobbyManifestChunks)
            {
                Logger.LogError(
                    "Host Mod list is too large to publish to Steam Lobby data."
                );
                return false;
            }

            bool published = true;
            for (int index = 0; index < chunkCount; index++)
            {
                int offset = index * LobbyManifestChunkSize;
                int length = Math.Min(LobbyManifestChunkSize, localLobbyProfile.Length - offset);
                published &= SteamMatchmaking.SetLobbyData(
                    lobbyId,
                    LobbyManifestChunkPrefix + index,
                    localLobbyProfile.Substring(offset, length)
                );
            }
            published &= SteamMatchmaking.SetLobbyData(
                lobbyId,
                LobbyManifestCountKey,
                chunkCount.ToString(CultureInfo.InvariantCulture)
            );
            // Commit the token last; readers verify both the token and the document hash.
            if (published) published = SteamMatchmaking.SetLobbyData(lobbyId, LobbyProfileKey, localToken);
            if (!published)
                Logger.LogError("Steam rejected one or more host Mod list Lobby data fields.");
            else
                Logger.LogInfo("Published host enabled Mod list to the Steam Lobby.");
            return published;
        }

        private static ProfileDocument ReadHostProfile(CSteamID lobbyId)
        {
            string countText = SteamMatchmaking.GetLobbyData(lobbyId, LobbyManifestCountKey);
            int chunkCount;
            if (!Int32.TryParse(countText, NumberStyles.None, CultureInfo.InvariantCulture,
                                out chunkCount) ||
                chunkCount < 1 || chunkCount > MaxLobbyManifestChunks)
                return null;

            StringBuilder text = new StringBuilder();
            for (int index = 0; index < chunkCount; index++)
            {
                string chunk = SteamMatchmaking.GetLobbyData(
                    lobbyId,
                    LobbyManifestChunkPrefix + index
                );
                if (String.IsNullOrEmpty(chunk)) return null;
                text.Append(chunk);
            }

            ProfileDocument profile;
            return TryParseProfile(text.ToString(), out profile) && profile.Token.StartsWith("3:") &&
                profile.Token == SteamMatchmaking.GetLobbyData(lobbyId, LobbyProfileKey) ? profile : null;
        }

        private string L(string chinese, string english)
        {
            return useChinese ? chinese : english;
        }

        private string BuildVisitorMismatchMessage(ProfileDocument hostProfile, bool leftLobby)
        {
            Dictionary<string, ProfileMod> hostById = new Dictionary<string, ProfileMod>(
                StringComparer.Ordinal
            );
            Dictionary<string, ProfileMod> localById = new Dictionary<string, ProfileMod>(
                StringComparer.Ordinal
            );
            for (int index = 0; index < hostProfile.Mods.Count; index++)
                hostById[hostProfile.Mods[index].Id] = hostProfile.Mods[index];
            for (int index = 0; index < localMods.Count; index++)
                localById[localMods[index].Id] = localMods[index];

            List<string> missing = new List<string>();
            List<string> extra = new List<string>();
            List<string> versions = new List<string>();
            for (int index = 0; index < hostProfile.Mods.Count; index++)
            {
                ProfileMod hostMod = hostProfile.Mods[index];
                ProfileMod localMod;
                if (!localById.TryGetValue(hostMod.Id, out localMod))
                    missing.Add(FormatMod(hostMod));
                else if (!String.Equals(hostMod.Version, localMod.Version,
                                        StringComparison.Ordinal))
                    versions.Add(
                        hostMod.Name + " — " +
                        L("房主 v", "Host v") + hostMod.Version +
                        L("，你的版本 v", ", your version v") + localMod.Version
                    );
            }
            for (int index = 0; index < localMods.Count; index++)
            {
                ProfileMod localMod = localMods[index];
                if (!hostById.ContainsKey(localMod.Id)) extra.Add(FormatMod(localMod));
            }

            StringBuilder message = new StringBuilder();
            message.AppendLine(L("无法进入房间：Mod 列表不匹配", "Cannot join: Mod lists do not match"));
            message.AppendLine();
            AppendList(
                message,
                L("房主有、你没有：", "On host, missing locally:"),
                missing,
                L("无", "None")
            );
            AppendList(
                message,
                L("你多出来的 Mod：", "Extra local Mods:"),
                extra,
                L("无", "None")
            );
            AppendList(
                message,
                L("版本不同：", "Version differences:"),
                versions,
                L("无", "None")
            );
            message.AppendLine();
            List<string> hostList = new List<string>();
            for (int index = 0; index < hostProfile.Mods.Count; index++)
                hostList.Add(FormatMod(hostProfile.Mods[index]));
            AppendList(
                message,
                L("房主当前启用的完整 Mod 清单：", "Host's complete enabled Mod list:"),
                hostList,
                L("没有启用 Mod", "No enabled Mods")
            );
            if (leftLobby)
                message.AppendLine().Append(
                    L("你已退出该房间，请在 Mod 管理器中自行调整后重试。",
                      "You have left the lobby. Adjust your Mods in the manager and try again.")
                );
            else
                message.AppendLine().Append(
                    L("请在 Mod 管理器中自行调整后刷新房间列表。",
                      "Adjust your Mods in the manager, then refresh the lobby list.")
                );
            return message.ToString();
        }

        private static void AppendList(
            StringBuilder message,
            string title,
            List<string> items,
            string emptyText
        )
        {
            message.AppendLine(title);
            if (items.Count == 0)
                message.AppendLine("  " + emptyText);
            else
                for (int index = 0; index < items.Count; index++)
                    message.AppendLine("  • " + items[index]);
        }

        private static string FormatMod(ProfileMod mod)
        {
            return mod.Name + "  v" + mod.Version;
        }

        private void ValidateMembersAsHost(
            Platform_Multiplayer platform,
            Platform_Multiplayer.MPLobby lobby
        )
        {
            platform.GetActiveLobbyMembers(false);
            CSteamID localId = SteamUser.GetSteamID();
            int memberCount = SteamMatchmaking.GetNumLobbyMembers(lobby.id);
            bool allCompatible = true;
            activeMemberIds.Clear();

            for (int index = 0; index < memberCount; index++)
            {
                CSteamID memberId = SteamMatchmaking.GetLobbyMemberByIndex(lobby.id, index);
                if (memberId == localId) continue;
                activeMemberIds.Add(memberId.m_SteamID);

                string remoteToken = SteamMatchmaking.GetLobbyMemberData(
                    lobby.id,
                    memberId,
                    MemberProfileKey
                );
                if (String.Equals(remoteToken, localToken, StringComparison.Ordinal))
                {
                    missingProfileSince.Remove(memberId.m_SteamID);
                    handledMembers.Remove(memberId.m_SteamID);
                    continue;
                }

                allCompatible = false;
                float firstSeen;
                if (!missingProfileSince.TryGetValue(memberId.m_SteamID, out firstSeen))
                {
                    missingProfileSince[memberId.m_SteamID] = Time.unscaledTime;
                    string playerName = SteamFriends.GetFriendPersonaName(memberId);
                    ShowOverlay(
                        L(
                            "正在验证 " + playerName + " 的 Mod 列表；验证完成前不能开始游戏。",
                            "Verifying " + playerName + "'s Mod list; the game cannot start yet."
                        ),
                        true
                    );
                    continue;
                }
                if (Time.unscaledTime - firstSeen < MemberHandshakeGraceSeconds) continue;
                RejectMember(platform, lobby, memberId, String.IsNullOrEmpty(remoteToken));
            }

            staleMemberIds.Clear();
            foreach (ulong memberId in handledMembers)
                if (!activeMemberIds.Contains(memberId)) staleMemberIds.Add(memberId);
            for (int index = 0; index < staleMemberIds.Count; index++)
                handledMembers.Remove(staleMemberIds[index]);
            staleMemberIds.Clear();
            foreach (ulong memberId in missingProfileSince.Keys)
                if (!activeMemberIds.Contains(memberId)) staleMemberIds.Add(memberId);
            for (int index = 0; index < staleMemberIds.Count; index++)
                missingProfileSince.Remove(staleMemberIds[index]);

            lobbyCompatible = allCompatible;
            if (allCompatible && overlayIsVerification) ClearOverlay();
        }

        private void ValidateHostAsClient(
            Platform_Multiplayer platform,
            Platform_Multiplayer.MPLobby lobby
        )
        {
            lobbyCompatible = false;
            ProfileDocument hostProfile = ReadHostProfile(lobby.id);
            if (hostProfile == null) return;

            if (String.Equals(hostProfile.Token, localToken, StringComparison.Ordinal))
            {
                lobbyCompatible = true;
                return;
            }

            lobbyCompatible = false;
            if (handledClientLobbyId == lobby.id.m_SteamID)
            {
                platform.LeaveLobby(false);
                return;
            }
            handledClientLobbyId = lobby.id.m_SteamID;
            string reason = BuildVisitorMismatchMessage(hostProfile, true);
            ShowOverlay(reason);
            Logger.LogWarning("Left Lobby because the host Mod list did not match.");
            platform.LeaveLobby(false);
        }

        private void RejectMember(
            Platform_Multiplayer platform,
            Platform_Multiplayer.MPLobby lobby,
            CSteamID memberId,
            bool missingDetector
        )
        {
            Platform_Multiplayer.MPLobbyMember member = null;
            if (lobby.members != null)
            {
                for (int index = 0; index < lobby.members.Count; index++)
                {
                    if (lobby.members[index].id == memberId)
                    {
                        member = lobby.members[index];
                        break;
                    }
                }
            }
            if (member == null)
            {
                Logger.LogWarning("Incompatible Steam member was not present in the refreshed lobby list.");
                return;
            }

            bool firstRejection = handledMembers.Add(memberId.m_SteamID);
            platform.KickMemberFromLobby(member);
            if (!firstRejection) return;

            string playerName = SteamFriends.GetFriendPersonaName(memberId);
            string reason = missingDetector
                ? L(
                    "已阻止 " + playerName + " 加入：对方未运行联机 Mod 检测。",
                    "Blocked " + playerName + ": compatibility detection was not found."
                  )
                : L(
                    "已阻止 " + playerName + " 加入：Mod 列表或对应版本不一致。",
                    "Blocked " + playerName + ": Mod list or corresponding versions differ."
                  );
            ShowOverlay(reason);
            Logger.LogWarning(reason.Replace('\n', ' '));
        }

        private void ResetLobbyState()
        {
            currentLobbyId = 0;
            publishedMemberLobbyId = 0;
            publishedHostLobbyId = 0;
            handledClientLobbyId = 0;
            localProfileErrorLobbyId = 0;
            lobbyCompatible = false;
            missingProfileSince.Clear();
            handledMembers.Clear();
        }

        private void ClearOverlay()
        {
            overlayMessage = "";
            overlayContent.text = "";
            overlayScroll = Vector2.zero;
            overlayIsVerification = false;
        }

        private void ShowOverlay(string message, bool verification = false)
        {
            overlayMessage = message;
            overlayContent.text = message;
            overlayScroll = Vector2.zero;
            overlayIsVerification = verification;
            overlayUntil = Time.unscaledTime + OverlayDurationSeconds;
        }

        internal bool AllowMultiplayerStart()
        {
            if (singlePlayerSkirmishSetup) return true;
            PollLobby();
            if (currentLobbyId == 0) return true;
            if (lobbyCompatible) return true;

            ShowOverlay(
                L(
                    "尚未确认房间内所有玩家的 Mod 一致，不能开始游戏。",
                    "Cannot start until every player's Mod profile is verified."
                )
            );
            return false;
        }

        private void QueuePendingJoin(
            Platform_Multiplayer platform,
            Platform_Multiplayer.MPLobby lobby,
            Action lobbyJoined,
            Action<string, string, int> lobbyChat,
            bool keepAutoJoinLobby
        )
        {
            joinGeneration++;
            pendingJoinPlatform = platform;
            pendingJoinLobby = lobby;
            pendingLobbyJoined = lobbyJoined;
            pendingLobbyChat = lobbyChat;
            pendingKeepAutoJoinLobby = keepAutoJoinLobby;
            pendingJoinProcessAt = Time.unscaledTime + PendingJoinMinimumDisplaySeconds;
            pendingJoinExpiresAt = Time.unscaledTime + PendingJoinWaitSeconds;
            ShowOverlay(L("正在验证 Mod 列表…", "Verifying Mod list..."), true);
            overlayUntil = pendingJoinExpiresAt;
            Logger.LogInfo(
                "Waiting for the host's complete Mod manifest before joining Lobby "
                    + lobby.id.m_SteamID
                    + "."
            );
        }

        private void ClearPendingJoin()
        {
            pendingJoinPlatform = null;
            pendingJoinLobby = null;
            pendingLobbyJoined = null;
            pendingLobbyChat = null;
            pendingKeepAutoJoinLobby = false;
            pendingJoinProcessAt = 0f;
            pendingJoinExpiresAt = 0f;
        }

        internal void CancelPendingJoin()
        {
            joinGeneration++;
            if (pendingJoinLobby != null)
                Logger.LogInfo("Cancelled pending Lobby join because the multiplayer screen was closed.");
            ClearPendingJoin();
            if (overlayIsVerification) ClearOverlay();
        }

        internal void SetSkirmishSetup(bool skirmishSetup)
        {
            singlePlayerSkirmishSetup = skirmishSetup;
            if (!skirmishSetup) return;
            CancelPendingJoin();
            ResetLobbyState();
        }

        private void ProcessPendingJoin()
        {
            if (pendingJoinLobby == null) return;
            if (Time.unscaledTime < pendingJoinProcessAt) return;
            if (Time.unscaledTime > pendingJoinExpiresAt)
            {
                Logger.LogWarning(
                    "Cancelled pending Lobby join after the host Mod manifest timed out."
                );
                ClearPendingJoin();
                ShowOverlay(
                    L(
                        "无法获取房主的 Mod 列表，已取消加入。",
                        "The host's Mod list could not be retrieved; joining was cancelled."
                    )
                );
                return;
            }

            ProfileDocument hostProfile = ReadHostProfile(pendingJoinLobby.id);
            if (hostProfile == null) return;
            if (!RefreshRuntimeProfile()) return;

            Platform_Multiplayer platform = pendingJoinPlatform;
            Platform_Multiplayer.MPLobby lobby = pendingJoinLobby;
            Action lobbyJoined = pendingLobbyJoined;
            Action<string, string, int> lobbyChat = pendingLobbyChat;
            bool keepAutoJoinLobby = pendingKeepAutoJoinLobby;
            int generation = joinGeneration;
            ClearPendingJoin();

            if (!String.Equals(hostProfile.Token, localToken, StringComparison.Ordinal))
            {
                ShowOverlay(BuildVisitorMismatchMessage(hostProfile, false));
                return;
            }

            if (overlayIsVerification) ClearOverlay();
            Action guardedLobbyJoined = delegate
            {
                if (generation != joinGeneration || singlePlayerSkirmishSetup)
                {
                    platform.LeaveLobby(false);
                    return;
                }
                if (lobbyJoined != null) lobbyJoined();
            };
            Action<string, string, int> guardedLobbyChat = delegate(
                string name,
                string message,
                int colourId
            )
            {
                if (generation == joinGeneration && !singlePlayerSkirmishSetup && lobbyChat != null)
                    lobbyChat(name, message, colourId);
            };
            allowNextJoinLobbyId = lobby.id.m_SteamID;
            try
            {
                platform.JoinLobby(
                    lobby,
                    guardedLobbyJoined,
                    guardedLobbyChat,
                    keepAutoJoinLobby
                );
            }
            finally
            {
                allowNextJoinLobbyId = 0;
            }
        }

        internal bool AllowLobbyJoin(
            Platform_Multiplayer platform,
            Platform_Multiplayer.MPLobby lobby,
            Action lobbyJoined,
            Action<string, string, int> lobbyChat,
            bool keepAutoJoinLobby
        )
        {
            if (lobby == null) return true;
            if (singlePlayerSkirmishSetup) return true;
            if (allowNextJoinLobbyId == lobby.id.m_SteamID)
            {
                allowNextJoinLobbyId = 0;
                return true;
            }
            if (!RefreshRuntimeProfile())
            {
                ShowOverlay(
                    L(
                        "管理器清单或 Script Extender 运行时未就绪，不能进入多人房间。",
                        "The manager profile or Script Extender runtime is not ready; the lobby cannot be joined."
                    )
                );
                return false;
            }

            QueuePendingJoin(platform, lobby, lobbyJoined, lobbyChat, keepAutoJoinLobby);
            return false;
        }

        internal void RuntimeOnGUI()
        {
            if (String.IsNullOrEmpty(overlayMessage) || Time.unscaledTime > overlayUntil) return;
            EnsureOverlayBackground();

            float width = Math.Min(1000f, Screen.width - 60f);
            float height = Math.Min(680f, Screen.height - 60f);
            Rect area = new Rect(
                (Screen.width - width) / 2f,
                (Screen.height - height) / 2f,
                width,
                height
            );
            GUI.depth = -1000;
            GUI.DrawTexture(area, overlayBackground, ScaleMode.StretchToFill);

            if (overlayTextStyle == null)
            {
                overlayTextStyle = new GUIStyle(GUI.skin.label);
                overlayTextStyle.fontStyle = FontStyle.Normal;
                overlayTextStyle.normal.textColor = Color.white;
                overlayTextStyle.wordWrap = true;
                overlayTextStyle.richText = false;
                overlayTextStyle.alignment = TextAnchor.UpperLeft;
            }
            overlayTextStyle.fontSize = Math.Max(18, Math.Min(28, Screen.width / 55));

            const float padding = 34f;
            float contentWidth = width - padding * 2f - 18f;
            float contentHeight = Math.Max(
                height - padding * 2f,
                overlayTextStyle.CalcHeight(overlayContent, contentWidth) + 12f
            );
            Rect viewport = new Rect(
                area.x + padding,
                area.y + padding,
                width - padding * 2f,
                height - padding * 2f
            );
            overlayScroll = GUI.BeginScrollView(
                viewport,
                overlayScroll,
                new Rect(0f, 0f, contentWidth, contentHeight)
            );
            GUI.Label(
                new Rect(0f, 0f, contentWidth, contentHeight),
                overlayContent,
                overlayTextStyle
            );
            GUI.EndScrollView();
        }

        private void EnsureOverlayBackground()
        {
            if (overlayBackground != null) return;
            overlayBackground = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            overlayBackground.hideFlags = HideFlags.HideAndDontSave;
            overlayBackground.SetPixel(0, 0, new Color(0.035f, 0.04f, 0.04f, 0.97f));
            overlayBackground.Apply();
        }
    }

    internal sealed class ProfileDocument
    {
        internal string Token = "";
        internal readonly List<ProfileMod> Mods = new List<ProfileMod>();
    }

    internal sealed class ProfileMod
    {
        internal ProfileMod(string id, string version, string name)
        {
            Id = id;
            Version = version;
            Name = String.IsNullOrEmpty(name) ? id : name;
        }

        internal string Id { get; private set; }
        internal string Version { get; private set; }
        internal string Name { get; private set; }
    }

    internal sealed class CompatibilityRuntime : MonoBehaviour
    {
        internal SCDEMultiplayerCompatibilityPlugin Owner { get; set; }

        private void Update()
        {
            SCDEMultiplayerCompatibilityPlugin owner = Owner;
            if (!ReferenceEquals(owner, null)) owner.RuntimeUpdate();
        }

        private void OnGUI()
        {
            SCDEMultiplayerCompatibilityPlugin owner = Owner;
            if (!ReferenceEquals(owner, null)) owner.RuntimeOnGUI();
        }

        private void OnDestroy()
        {
            SCDEMultiplayerCompatibilityPlugin owner = Owner;
            Owner = null;
            if (!ReferenceEquals(owner, null)) owner.RuntimeDestroyed(this);
        }
    }

    internal static class SettingsLoadGuard
    {
        private const string BackupEnvironmentVariable = "SCDEModManagerSettingsBackup";
        private const int RetryCount = 8;
        private const int RetryDelayMilliseconds = 75;
        private static bool retryingLoad;
        private static bool loadSucceeded;

        internal static void PrepareForLoad()
        {
            if (retryingLoad) return;
            string settingsPath = Path.Combine(Application.persistentDataPath, "settings.cfg");
            if (IsCompleteSettingsFile(settingsPath)) return;

            for (int attempt = 0; attempt < RetryCount; attempt++)
            {
                Thread.Sleep(RetryDelayMilliseconds);
                if (!IsCompleteSettingsFile(settingsPath)) continue;
                SCDEMultiplayerCompatibilityPlugin.LogSettingsGuardInfo(
                    "SCDE settings became readable after waiting for Steam AutoCloud."
                );
                return;
            }

            if (TryRestoreBackup(settingsPath))
            {
                SCDEMultiplayerCompatibilityPlugin.LogSettingsGuardInfo(
                    "SCDE settings were restored from the current Steam account's last valid manager snapshot."
                );
            }
        }

        internal static void VerifyLoad()
        {
            if (retryingLoad) return;
            if (ConfigSettings.SettingsFileExisted)
            {
                loadSucceeded = true;
                CaptureBackup();
                return;
            }
            string settingsPath = Path.Combine(Application.persistentDataPath, "settings.cfg");
            if (!TryRestoreBackup(settingsPath)) return;

            retryingLoad = true;
            try
            {
                ConfigSettings.LoadSettings();
                if (ConfigSettings.SettingsFileExisted)
                {
                    loadSucceeded = true;
                    CaptureBackup();
                    SCDEMultiplayerCompatibilityPlugin.LogSettingsGuardInfo(
                        "SCDE settings load recovered before the first-run screen could replace player preferences."
                    );
                }
                else
                {
                    SCDEMultiplayerCompatibilityPlugin.LogSettingsGuardWarning(
                        "SCDE settings remained unreadable after restoring the manager snapshot."
                    );
                }
            }
            finally
            {
                retryingLoad = false;
            }
        }

        internal static void RecordSave()
        {
            string backupPath = Environment.GetEnvironmentVariable(BackupEnvironmentVariable);
            if (String.IsNullOrEmpty(backupPath)) return;
            if (!loadSucceeded && File.Exists(backupPath)) return;
            string settingsPath = Path.Combine(Application.persistentDataPath, "settings.cfg");
            TryCopyCompleteSettings(settingsPath, backupPath);
        }

        private static void CaptureBackup()
        {
            string backupPath = Environment.GetEnvironmentVariable(BackupEnvironmentVariable);
            if (String.IsNullOrEmpty(backupPath)) return;
            string settingsPath = Path.Combine(Application.persistentDataPath, "settings.cfg");
            TryCopyCompleteSettings(settingsPath, backupPath);
        }

        private static bool TryRestoreBackup(string settingsPath)
        {
            string backupPath = Environment.GetEnvironmentVariable(BackupEnvironmentVariable);
            return TryCopyCompleteSettings(backupPath, settingsPath);
        }

        private static bool TryCopyCompleteSettings(string sourcePath, string targetPath)
        {
            if (!IsCompleteSettingsFile(sourcePath) || String.IsNullOrEmpty(targetPath)) return false;
            string temporary = targetPath + ".scdemm.tmp";
            try
            {
                string directory = Path.GetDirectoryName(targetPath);
                if (!String.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
                File.Copy(sourcePath, temporary, true);
                File.Copy(temporary, targetPath, true);
                return IsCompleteSettingsFile(targetPath);
            }
            catch
            {
                return false;
            }
            finally
            {
                try
                {
                    if (File.Exists(temporary)) File.Delete(temporary);
                }
                catch
                {
                }
            }
        }

        private static bool IsCompleteSettingsFile(string filePath)
        {
            if (String.IsNullOrEmpty(filePath)) return false;
            try
            {
                string text = File.ReadAllText(filePath, Encoding.UTF8);
                return text.StartsWith("||SETTINGS||", StringComparison.Ordinal) &&
                       CountOccurrences(text, "||SETTINGS||") == 2 &&
                       HasNonEmptySetting(text, "Name:") &&
                       text.IndexOf("PushMapScrolling:", StringComparison.Ordinal) >= 0 &&
                       text.TrimEnd().EndsWith("||KEYS||", StringComparison.Ordinal);
            }
            catch
            {
                return false;
            }
        }

        private static bool HasNonEmptySetting(string text, string prefix)
        {
            foreach (string rawLine in text.Split(new[] { '\r', '\n' }))
            {
                if (rawLine.StartsWith(prefix, StringComparison.Ordinal) &&
                    rawLine.Substring(prefix.Length).Trim().Length > 0)
                    return true;
            }
            return false;
        }

        private static int CountOccurrences(string text, string value)
        {
            int count = 0;
            int index = 0;
            while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += value.Length;
            }
            return count;
        }
    }

    // A sparse key map is valid. Vanilla LoadFromString indexes missing stance keys
    // (R/T/Y), and its caller silently treats that exception as a failed settings load.
    [HarmonyPatch(typeof(KeyManager), "LoadFromString")]
    internal static class SparseNativeKeyMapLoadPatch
    {
        private static IEnumerable<CodeInstruction> Transpiler(
            IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo lookup = AccessTools.PropertyGetter(typeof(Dictionary<int, bool>), "Item");
            MethodInfo contains = AccessTools.Method(typeof(Dictionary<int, bool>), "ContainsKey");
            foreach (CodeInstruction instruction in instructions)
            {
                if (instruction.Calls(lookup)) instruction.operand = contains;
                yield return instruction;
            }
        }
    }

    [HarmonyPatch(typeof(ConfigSettings), "LoadSettings")]
    internal static class SettingsLoadGuardPatch
    {
        private static void Prefix()
        {
            SettingsLoadGuard.PrepareForLoad();
        }

        private static void Postfix()
        {
            SettingsLoadGuard.VerifyLoad();
        }
    }

    [HarmonyPatch(typeof(ConfigSettings), "SaveSettings")]
    internal static class SettingsSaveGuardPatch
    {
        private static void Postfix()
        {
            SettingsLoadGuard.RecordSave();
        }
    }

    [HarmonyPatch(typeof(Platform_Multiplayer), "JoinLobby")]
    internal static class JoinLobbyCompatibilityPatch
    {
        private static bool Prefix(
            Platform_Multiplayer __instance,
            Platform_Multiplayer.MPLobby __0,
            Action __1,
            Action<string, string, int> __2,
            bool __3
        )
        {
            SCDEMultiplayerCompatibilityPlugin plugin =
                SCDEMultiplayerCompatibilityPlugin.Instance;
            return !ReferenceEquals(plugin, null) && plugin.AllowLobbyJoin(__instance, __0, __1, __2, __3);
        }
    }

    [HarmonyPatch(typeof(Platform_Multiplayer), "HostStartGame")]
    internal static class HostStartGameCompatibilityPatch
    {
        private static bool Prefix()
        {
            SCDEMultiplayerCompatibilityPlugin plugin =
                SCDEMultiplayerCompatibilityPlugin.Instance;
            return !ReferenceEquals(plugin, null) && plugin.AllowMultiplayerStart();
        }
    }

    [HarmonyPatch(typeof(CrusaderDE.FRONT_Multiplayer), "doOpen")]
    internal static class MultiplayerScreenModePatch
    {
        private static void Prefix(bool __0)
        {
            SCDEMultiplayerCompatibilityPlugin plugin =
                SCDEMultiplayerCompatibilityPlugin.Instance;
            if (!ReferenceEquals(plugin, null)) plugin.SetSkirmishSetup(__0);
        }
    }

    [HarmonyPatch(typeof(CrusaderDE.FRONT_Multiplayer), "LeaveLobby")]
    internal static class MultiplayerScreenLeavePatch
    {
        private static void Prefix()
        {
            SCDEMultiplayerCompatibilityPlugin plugin =
                SCDEMultiplayerCompatibilityPlugin.Instance;
            if (!ReferenceEquals(plugin, null)) plugin.CancelPendingJoin();
        }
    }
}
