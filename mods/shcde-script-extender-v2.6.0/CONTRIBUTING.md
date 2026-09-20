# Contributing to SHCDE-SE

First off, thank you for considering contributing! Your help is appreciated.

This document provides guidelines for contributing to the project. Please feel free to propose changes to this document in a pull request.

It is important to note: Not everything in this document applies to what you might have in mind when contributing, so please beware that there might be quite a bit of info here that will not concern you.

## How Can I Contribute?

> IMPORTANT NOTICE: If you are a pure viber code -> (in other words: no clue what you're doing aside from typing away at a chatbox and hoping for the best), close this tab and don't even think about opening a MR or issue, UNLESS you TRULY understanding the topic of what you (or rather, the LLM) is talking about. It is extremely disrespectful of anyones time, reading the gibberish output by a chatbot, prompted by someone who doesnt even take their OWN TIME to understand the topic in the first place!

> AI-assisted programming is fine and all, but if the person behind the screen becomes helpless in the face of what they are looking at / """working""" with, it is to the detriment of everyone and everything associated.

There are many ways to contribute, from reporting bugs, writing code to populating the reverse engineering database:

*   **Reporting Bugs:** If you find a bug, please create an issue in the GitLab issue tracker. A good bug report is specific and includes all the necessary details to reproduce it. Also try to include the BepInEx LogOutput.txt file (Which can be found in (GAMEDIR)/BepInEx/LogOutput.txt) if its about errors.
*   **Suggesting Enhancements:** If you have an idea for a new feature or an improvement to an existing one, feel free to create an issue to discuss it. 
*   **Submitting Pull Requests:** If you want to contribute code, you can submit a merge request. Please ensure you have read the development setup instructions below.

### Debugging
If you intend to debug the game: See the [HowTo_RuntimeDebugging.md](./ReverseEngineering/HowTo_RuntimeDebugging.md) file.

## Merge Request Guidelines

*   **Keep it focused:** Please keep your merge requests focused on a single issue or feature.
*   **Provide a clear description:** Explain the "what" and "why" of your changes, not just the "how." Link to any relevant issues.

## Git Guidelines
*   **Conventional Commits:** This project uses the [conventional commits specification](https://www.conventionalcommits.org/en/v1.0.0/#summary).

## Domain-specific Guidelines

This section will describe some specific guidelines for contribution types, including but not limited to: Code, Reverse Engineering, Documentation, etc

### Code:

#### Style

This project's way of dealing with many hooks is to have one static class per related component, which is responsible for setting up all native detours through PolyHook2 or just setting up call-only delegates through it.

Example:
```cs
internal unsafe static class BulkMapEditorDetours
{
    private static bool _isInit = false;

    public static void Apply(ReadOnlySpan<byte> memory)
```
Where you'd check the _isInit field within Apply(...) to prevent duplicate detours.

Typically you'd instantiate a common AOB Detour like so:
```cs
c_game_editor_brush_set_tiletype_hook = new X64ManagedFunctionDetourAOB<c_game_editor_brush_set_tiletype_delegate>(memory, "48 89 5C 24 ?? 44 89 4C 24 ?? 44 89 44 24 ?? 89 54 24 ?? 55 56 57 41 54 41 55 41 56 41 57 48 83 EC ?? 4D 63 E1", c_game_editor_brush_set_tiletype_hook_impl, currentImageBase);
```

Defined as such:
```cs
// IDA or GHIDRA FUNCTION PROTOTYPE HERE
// AOB HERE
// DELEGATE HERE (IF APPLICABLE)
// PUBLIC STATIC X64ManagedFunctionDetourAOB<DELEGATE> HERE
// PUBLIC UNSAFE STATIC HOOK IMPLEMENTATION HERE


// Or in practice:
// __int64 __fastcall c_game_editor_brush_set_tiletype(__int64 pTileManager, int brushSize, int centerTileId, int centerTileY, int a5, int a6, int tileType, char a8)
// 48 89 5C 24 ?? 44 89 4C 24 ?? 44 89 44 24 ?? 89 54 24 ?? 55 56 57 41 54 41 55 41 56 41 57 48 83 EC ?? 4D 63 E1
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
public delegate Int64 c_game_editor_brush_set_tiletype_delegate(IntPtr pTileManager, int brushSize, int centerTileId, int centerTileY, int a5, int tileProperty, int tileType, byte a8);
public static X64ManagedFunctionDetourAOB<c_game_editor_brush_set_tiletype_delegate>? c_game_editor_brush_set_tiletype_hook;
public unsafe static Int64 c_game_editor_brush_set_tiletype_hook_impl(IntPtr pTileManager, int brushSize, int centerTileId, int centerTileY, int a5, int tileProperty, int tileType, byte a8)
{
    // The code to be run before the actual function gets ran.
}
```

This project uses a mixed naming scheme for functions. High level C# functions follow default C# style guidelines, while functions that get invoked directly by the native world may follow the given naming scheme that closely resembles their native function prototype versions (be it from IDA or Ghidra, etc)

The point is to being able to quickly find the native function that interacts with these c# callback functions, IMO it also makes it clear which functions get called directly from unmanaged land.

### Code: Unity Threading and the Native Engine

One of the most critical architectural aspects of this project is the interaction between the **Unity Main Thread** and the **Native Engine Thread**. Understanding this is essential to prevent freezes, crashes, and deadlocks.

*   The **Unity Main Thread** is responsible for all rendering, UI, and interactions with the Unity Engine API (e.g., playing sounds, creating GameObjects).
*   The **Native Engine Thread** is where the core game logic from `CrusaderDE.dll` runs, including Lua script execution via `DLL_RunTick`.

#### The Main Rule: Never Call Unity APIs from the Native Thread

The Unity Engine API is **not thread-safe**. Calling any Unity function or BepInEx logging function directly from code that executes on the native engine thread will lead to unpredictable behavior, race conditions, and crashes.

To solve this, the script extender provides a thread-safe bridge: `UnityMainThreadDispatcher`.

Any time code running on the native thread needs to interact with the Unity world, it **must** dispatch that work to the main thread using this dispatcher. However, it's crucial to use the correct dispatch pattern.

---

#### The Wrong Way: Causing a Deadlock with `EnqueueAndWait`

It is tempting to use `EnqueueAndWait` to get an immediate result back from the Unity thread. **This will cause a complete application freeze (deadlock)**.

The deadlock occurs because:
1.  The Unity Main Thread calls `DLL_RunTick` and waits for it to complete.
2.  Code inside `DLL_RunTick` (on the native thread) calls `EnqueueAndWait`.
3.  Now, the Native Thread is waiting for the Unity Main Thread to process its work.
4.  You have a circular wait: **Unity waits for Native, and Native waits for Unity.** The game freezes.

```csharp
// WRONG - DO NOT DO THIS FROM THE NATIVE THREAD
[LuaApiExport("GetSomeUnityValue")]
public int GetSomeUnityValue()
{
    // This function will FREEZE the game when called from Lua,
    // because Lua runs on the native thread.
    return UnityMainThreadDispatcher.Instance.EnqueueAndWait(() => {
        return SFXManager.instance.play_list.Count;
    });
}
```

---

#### The Right Way: Asynchronous Patterns

You must use non-blocking, asynchronous patterns. The native thread should "fire and forget" its request to the main thread and not wait for a response.

##### Pattern 1: Fire-and-Forget (`Enqueue`)

Use this when you need to *do* something in Unity but don't need a result back.

**Scenario:** Playing a sound from Lua.

```csharp
// CORRECT - Fire-and-forget
[LuaApiExport("Sound_Play")]
public void PlayUnitySFX(int soundId)
{
    // We queue the work and return immediately. The native thread does not block.
    UnityMainThreadDispatcher.Instance.Enqueue(() =>
    {
        // This code will safely run on the main thread later.
        MyAudioManager.instance.playSFX(clip, ...);
    });
}
```

##### Pattern 2: Asynchronous with Callbacks (`Enqueue` + `LuaFunction`)

Use this when you need to perform an action in Unity and get a result back to the native thread (e.g., in Lua). The result is returned later via a callback function.

**Scenario:** Registering a sound from a map archive and returning its new ID to Lua.

**C# Implementation:**
```csharp
// CORRECT - Asynchronous with a callback
// NOTE: This is taken from the core source code as a rough example.
[LuaApiExport("Sound_RegisterFromArchiveAsync")]
public void RegisterSoundAsync(string filePath, NLua.LuaFunction onCompleteCallback)
{
    byte[] soundBytes = GameMapArchiveManagerAPI.Instance.TryReadBinaryFile(filePath);

    // Schedule the work on the main thread.
    UnityMainThreadDispatcher.Instance.Enqueue(() =>
    {
        int newId = -1; // Default to error
        try
        {
            // Do all the Unity-related work here...
            AudioClip clip = LoadAudioClipFromBytes(soundBytes);
            newId = AddClipToSfxManager(clip);
        }
        finally
        {
            // ...then call the Lua function with the result.
            onCompleteCallback?.Call(newId);
        }
    });
}
```

**Lua Usage:**
```lua
local function onSoundRegistered(soundId)
    if soundId > -1 then
        print("Sound registered with ID: " .. soundId)
        _MY_SOUND = soundId
    else
        print("Failed to register sound.")
    end
end

-- Call the async function and provide our function as the callback.
-- The script continues executing immediately.
Sound_RegisterFromArchiveAsync("sounds/mysound.ogg", onSoundRegistered)
```

#### Safety

This project allows untrusted Lua scripts (from workshop maps) to run inside the game environment. That makes **sandboxing and privilege separation critical**. Please keep the following principles in mind when writing C# ↔ C++ interop code and exposing APIs to Lua:

#### What Lua Scripts Must **Never** Get:
* **Raw Pointers:**
  * Lua must not receive direct pointers (`IntPtr`, `void*`, etc.) to Native or C# memory.
  * Always wrap native resources in **safe handles, opaque IDs, or managed wrappers**.
* **Direct OS Access:**
  * No exposure of file system, registry, sockets, or process APIs unless explicitly sandboxed.
  * Never allow Lua to call into system DLLs or unmanaged functions.
* **Privilege Escalation Paths:**
  * Don’t expose APIs that can be chained to break sandbox (e.g., direct memory reads, reflection into .NET internals, unmanaged delegates).

---

#### Safe Design Practices:

* **MitM Wrappers (Mediators):**
  * Always add a **managed layer** between C++ and Lua (Exceptions for performance may apply)
  * Example: Lua → (Sandboxed C# API) → (Validated Interop Layer) → Native.
* **Opaque Handles / Resource IDs:**
  * Expose *IDs* or *tokens* to Lua, not raw memory addresses.
  * Store actual pointers in a secure lookup table managed on the C# side.
* **Whitelisted API Surface:**
  * Expose only explicitly approved functions.
  * No “general-purpose” bindings (e.g., direct `P/Invoke` or arbitrary `DllImport`).
* **Type Safety:**
  * Validate all arguments coming from Lua before passing them into C# or Native code.
  * Reject or sanitize invalid data early.

---

#### Familiarity Terms for Collaborators

Contributors should be familiar with (to some extent, atleast):

* **Sandboxing:** Restricting Lua to a minimal safe standard library and whitelisted APIs.
* **Privilege Escalation:** How unsafe APIs (like raw memory access, unmanaged calls) can be abused to escape the sandbox.
* **Opaque Handles:** Using safe IDs instead of raw pointers to represent resources.
* **MitM (Man-in-the-Middle) Layer:** The managed glue code that validates all Lua <-> native interactions.
* **Capability-based Security:** Only give Lua the capabilities it absolutely needs, nothing more.
* **Memory Safety:** Ensuring unmanaged resources cannot be corrupted or accessed out-of-bounds via Lua.

---

⚠️ **Remember:** Any Lua script is considered *hostile input*. Treat the workshop scripting environment as if it’s running potentially malicious code.

#### Addresses

Short version: **don’t hardcode VAs/RVAs**. Use stable anchors (exported symbols, IAT/EAT, AOB signatures, etc.).

---

Why absolute VAs/RVAs are bad:
* **Not stable across builds** — compiler/optimizer changes, minor game updates, or different distributions can shift code/data.
* **ASLR / rebasing** — modern OSes randomize module bases at load time; using a fixed VA will fail.
* **Version drift** — an RVA that worked in v1 may point to different code/data in v2 (or crash).
* **Hard to audit** — future maintainers won’t know why a raw address was chosen.

### Reverse Engineering

#### ReClass.NET

The core ReClass.NET project can be found within `ReverseEngineering/structs/` inside the main repo directory. It contains the most up-to-date struct layouts the project is aware of. This project usually has more up-to-date data than the IDA Project, simply due to more frequent use.

When mapping new structs or populating existing ones try to follow this naming convention:

| Convention | Description
|:-|:-|
| **r_X**|Confirmed field name without probable doubt about its true purpose.|
| **p_r_X**|Probable field name.|
| **Generic/Unknown_X**|Unknown field name (Can be whatever)|

If you have made huge changes to the ReClass project file, make it a new version (See: `scde-main-1042-v3` -> `scde-main-1042-v4`)

#### IDA Pro

The core IDB file this project uses can be found within `ReverseEngineering\latest\CrusaderDE.7z`, its main purpose is for static analysis purposes and variable/function identification.

> NOTE: The password is "shcdese"

When identifying variables or functions, please refer to the following naming convention:
| Convention | Description
|:-|:-|
| **c_game_X**|Function name.|
| **gX**|Variable name.|
| **Generic/Unknown_X**|Unknown (Can be whatever)|

If migrating versions to a newer version, make sure to re-import the IDA-compatible struct/enum definitions found within `ReverseEngineering/structs/` such as `Custom.h` & `Enums.h`, these are based on the ReClass.NET Project mostly.

There is a experimental version migration script planned to make this more bearable, but for now this is a manual task.

#### Address Terms

When working with executables and disassembly, you'll often see different ways to represent addresses.

* **VA (Virtual Address)**

  * The full memory address as seen by the process at runtime.
  * Example: `0x7FF612341000`
  * Includes the base load address of the module (aka ImageBase)

* **RVA (Relative Virtual Address)**

  * The offset of an address relative to the module’s base.
  * Example: If base = `0x7FF612300000` and VA = `0x7FF612341000`, then RVA = `0x41000`.

* **#Address (File Offset / Raw Offset)**

  * Refers to the position of data/code inside the file on disk.
  * What you see if you open the binary with a hex editor.
  * Example: `#0x200` means “offset 0x200 in the file.”
  * X64Dbg uses: `:#0x00` for such GoTo's

* **\$Address (RVA Shorthand)**

  * In many RE tools, `$` refers to the current **RVA**.
  * Example: `$41000` means RVA `0x41000`, which corresponds to VA `0x7FF612341000` if base = `0x7FF612300000`.
  * X64Dbg uses: `:$0x00` for such GoTo's

⚠️ **Important:**

* Don’t confuse **RVA** with **file offset** -- they are not the same. Conversion between them requires the section alignment info from the executables headers.
* Always clarify whether you're talking about **VA**, **RVA**, or **file offset** when documenting addresses.

### Documentation

When writing documentation, be aware that this project uses DocFX. So in case you want to elaborate on some APIs that needs better documentation use the in-source summary xml comment system for that.

While for standalone pages, refer to the DocFX guidelines or use existing projects as examples (e.g. the docs/guides/ markdown style pages)

Good documentation should try to include at least one usage example in most cases when applicable.