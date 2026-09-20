using System;
using System.Threading;
using RedBird.Core.Memory;

namespace SHCDESE.API.LowLevel;

/// <summary>
/// Provides a central notification point for when the primary game library (CrusaderDE.dll) has been loaded into memory.
/// </summary>
/// <remarks>
/// This singleton class is a cornerstone of the script extender initialization process.
/// Its primary purpose is to fire the <see cref="LibraryLoaded"/> event at the earliest possible moment after the target DLL is mapped into the process memory. 
/// Other managers, particularly those that need to apply detours or perform memory scanning (AOB), must subscribe to this event to ensure they only execute
/// their logic after the memory they depend on is available and valid.
/// </remarks>
public sealed class CrusaderLibrary
{
    private static readonly Lazy<CrusaderLibrary> lazy = new Lazy<CrusaderLibrary>(() => new CrusaderLibrary());
    private readonly object _sync = new();

    /// <summary>
    /// Gets the singleton instance of the <see cref="CrusaderLibrary"/>.
    /// </summary>
    public static CrusaderLibrary Instance { get { return lazy.Value; } }

    private CrusaderLibrary()
    {

    }

    internal readonly ManualResetEventSlim InitCompleteEvent = new(false);

    internal void SignalInitComplete()
    {
        InitCompleteEvent.Set();
    }

    /// <summary>
    /// Defines the signature for subscribers to the <see cref="LibraryLoaded"/> event.
    /// </summary>
    /// <param name="context">The loaded library context.</param>
    public delegate void OnLibraryLoadedDelegate(CrusaderLibraryLoadContext context);

    /// <summary>
    /// Fires when the core game library is loaded.
    /// If a mod subscribes after the library is already loaded, its handler is called immediately.
    /// </summary>
    public event OnLibraryLoadedDelegate? LibraryLoaded
    {
        add
        {
            if (value is null)
                return;

            CrusaderLibraryLoadContext? context;

            lock (_sync)
            {
                context = _loadContext;

                // Only retain handlers that are still waiting for the initial notification.
                if (context is null)
                    _libraryLoaded += value;
            }

            // Never invoke third-party code while holding the lock.
            if (context is not null)
                value(context);
        }
        remove
        {
            if (value is null)
                return;

            lock (_sync)
                _libraryLoaded -= value;
        }
    }

    private OnLibraryLoadedDelegate? _libraryLoaded;
    private CrusaderLibraryLoadContext? _loadContext;
    private bool _libraryLoadedRaised;

    // The hook sets this; the init thread waits on it.
    internal readonly ManualResetEventSlim LibraryReadyEvent = new(false);

    // Captured at load time and retained for late subscribers and stable scanning.
    internal byte[] LibrarySnapshot = Array.Empty<byte>();

    internal IntPtr LibraryModuleHandle = IntPtr.Zero;

    /// <summary>
    /// Checks if the library is (likely) loaded.
    /// </summary>
    /// <returns>Whether the library is loaded.</returns>
    public bool IsLibraryLoaded()
    {
        lock (_sync)
            return _loadContext is not null;
    }

    internal void SignalLibraryLoaded(IntPtr moduleHandle, ReadOnlySpan<byte> memory)
    {
        byte[] snapshot = memory.ToArray();
        ScanRegion region = ScanRegion.FromMemory(snapshot, unchecked((UInt64)moduleHandle.ToInt64()), "CrusaderDE.dll load snapshot");
        CrusaderLibraryLoadContext context = new(moduleHandle, region);

        lock (_sync)
        {
            if (_loadContext is not null)
                throw new InvalidOperationException("The Crusader library has already been signalled as loaded.");

            LibraryModuleHandle = moduleHandle;
            LibrarySnapshot = snapshot;
            _loadContext = context;
        }

        LibraryReadyEvent.Set();
    }

    /// <summary>
    /// Raises the <see cref="LibraryLoaded"/> event, notifying all subscribers that the game's DLL is ready.
    /// </summary>
    /// <remarks>
    /// This method is marked "internal" and should only be called by the core bootstrap logic of the script extender.
    /// </remarks>
    internal void RaiseLibraryLoaded()
    {
        OnLibraryLoadedDelegate? handlers;
        CrusaderLibraryLoadContext context;

        lock (_sync)
        {
            if (_libraryLoadedRaised)
                throw new InvalidOperationException("The Crusader library loaded event has already been raised.");

            context = _loadContext ?? throw new InvalidOperationException("The Crusader library must be signalled before raising the loaded event.");
            _libraryLoadedRaised = true;
            handlers = _libraryLoaded;
            _libraryLoaded = null;
        }

        handlers?.Invoke(context);
    }
}
