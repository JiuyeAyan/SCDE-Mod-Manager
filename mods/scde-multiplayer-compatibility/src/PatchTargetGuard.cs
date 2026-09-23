using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Security.Cryptography;
using HarmonyLib;

namespace JiuyeAyan.SCDEMultiplayerCompatibility
{
    internal static class PatchTargetGuard
    {
        internal static MethodInfo Require(Type type, string name, bool isStatic, Type returns, Type[] parameters)
        {
            MethodInfo target = null;
            int matches = 0;
            foreach (MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic |
                BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                if (method.Name != name) continue;
                target = method;
                matches++;
            }
            if (matches != 1 || target.IsStatic != isStatic || target.ReturnType != returns || target.IsGenericMethod)
                throw new NotSupportedException("Unsupported patch target: " + type.FullName + "." + name);
            ParameterInfo[] actual = target.GetParameters();
            if (actual.Length != parameters.Length) throw new NotSupportedException("Patch parameter count changed: " + target);
            for (int index = 0; index < actual.Length; index++)
                if (actual[index].ParameterType != parameters[index])
                    throw new NotSupportedException("Patch parameter type changed: " + target);
            MethodBody body = target.GetMethodBody();
            if (body == null || body.GetILAsByteArray().Length < 2)
                throw new NotSupportedException("Patch target has no supported IL body: " + target);
            return target;
        }

        internal static void RequireCall(MethodInfo target, string declaringType, string methodName, int expected)
        {
            int count = 0;
            foreach (CodeInstruction instruction in PatchProcessor.GetOriginalInstructions(target))
            {
                MethodInfo called = instruction.operand as MethodInfo;
                if (called != null && instruction.Calls(called) && called.DeclaringType.FullName == declaringType && called.Name == methodName)
                    count++;
            }
            if (count != expected) throw new NotSupportedException("Patch IL anchor changed: " + target + " -> " + declaringType + "." + methodName);
        }

        internal static void ValidateGameTargets()
        {
            MethodInfo key = Require(typeof(KeyManager), "LoadFromString", false, typeof(void), new[] { typeof(string) });
            ValidateSparseKeyMap(new List<CodeInstruction>(PatchProcessor.GetOriginalInstructions(key)));
            RequireCall(Require(typeof(ConfigSettings), "LoadSettings", true, typeof(void), Type.EmptyTypes), "KeyManager", "LoadFromString", 1);
            RequireCall(Require(typeof(ConfigSettings), "SaveSettings", true, typeof(void), new[] { typeof(bool) }), "System.IO.File", "WriteAllText", 1);
            RequireCall(Require(typeof(Platform_Multiplayer), "JoinLobby", false, typeof(void), new[] {
                typeof(Platform_Multiplayer.MPLobby), typeof(Action), typeof(Action<string, string, int>), typeof(bool)
            }), "Steamworks.SteamMatchmaking", "JoinLobby", 1);
            RequireCall(Require(typeof(Platform_Multiplayer), "HostStartGame", false, typeof(void), Type.EmptyTypes), "Steamworks.SteamMatchmaking", "SetLobbyData", 1);
            RequireCall(Require(typeof(CrusaderDE.FRONT_Multiplayer), "doOpen", false, typeof(void), new[] {
                typeof(bool), typeof(bool), typeof(CrusaderDE.HUD_IngameMenu.RestartSkirmishMapInfo), typeof(bool), typeof(bool), typeof(int), typeof(int)
            }), "CrusaderDE.MainViewModel", "set_SkirmishSetupMode", 1);
            RequireCall(Require(typeof(CrusaderDE.FRONT_Multiplayer), "LeaveLobby", false, typeof(void), new[] { typeof(bool), typeof(bool) }),
                "Platform_Multiplayer", "LeaveLobby", 1);
            // Provenance only: supported structure, not one whole-file hash, gates patches.
            try
            {
                using (Stream stream = File.OpenRead(typeof(KeyManager).Assembly.Location))
                using (SHA256 sha = SHA256.Create())
                    SCDEMultiplayerCompatibilityPlugin.LogSettingsGuardInfo("MMC_GAME_ASSEMBLY_SHA256: " + BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", ""));
            }
            catch (IOException error) { SCDEMultiplayerCompatibilityPlugin.LogSettingsGuardWarning("Game assembly provenance unavailable: " + error.Message); }
            catch (UnauthorizedAccessException error) { SCDEMultiplayerCompatibilityPlugin.LogSettingsGuardWarning("Game assembly provenance unavailable: " + error.Message); }
        }

        internal static void ValidateSparseKeyMap(List<CodeInstruction> instructions)
        {
            MethodInfo lookup = AccessTools.PropertyGetter(typeof(Dictionary<int, bool>), "Item");
            int[] expectedKeys = { 114, 116, 121 };
            int count = 0;
            for (int index = 0; index < instructions.Count; index++)
            {
                if (!instructions[index].Calls(lookup)) continue;
                if (count >= expectedKeys.Length || index == 0 || index + 1 >= instructions.Count ||
                    !instructions[index - 1].LoadsConstant(expectedKeys[count]) ||
                    (instructions[index + 1].opcode != OpCodes.Brtrue && instructions[index + 1].opcode != OpCodes.Brtrue_S))
                    throw new NotSupportedException("Sparse key-map IL shape changed; refusing transpiler.");
                count++;
            }
            if (count != expectedKeys.Length) throw new NotSupportedException("Sparse key-map lookup count changed; refusing transpiler.");
        }
    }
}
