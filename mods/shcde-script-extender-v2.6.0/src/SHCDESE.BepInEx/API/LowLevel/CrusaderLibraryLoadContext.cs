using Microsoft.Extensions.Logging;
using RedBird.Core.Memory;
using RedBird.X64.Memory.Scanners;
using System;

namespace SHCDESE.API.LowLevel;

/// <summary>
/// Provides the loaded Crusader library and its scanning facilities to a <see cref="CrusaderLibrary.LibraryLoaded"/> subscriber.
/// </summary>
/// <remarks>
/// This context and its <see cref="Region"/> remain valid while the game library is loaded. 
/// The region is owned by the script extender and must not be disposed by subscribers. 
/// Scanners created from it are independent per subscriber.
/// </remarks>
public sealed class CrusaderLibraryLoadContext
{
    internal CrusaderLibraryLoadContext(IntPtr moduleHandle, ScanRegion region)
    {
        ModuleHandle = moduleHandle;
        Region = region ?? throw new ArgumentNullException(nameof(region));
    }

    /// <summary>Gets the native module handle for CrusaderDE.dll.</summary>
    public IntPtr ModuleHandle { get; }

    /// <summary>Gets the load-time snapshot region covering the loaded library.</summary>
    public ScanRegion Region { get; }

    /// <summary>Gets the load-time snapshot bytes of the loaded library.</summary>
    public ReadOnlySpan<byte> Memory => Region.Span;

    /// <summary>Creates an independent scanner over the shared library region.</summary>
    /// <param name="logger">An optional logger for scanner diagnostics.</param>
    /// <returns>An independent scanner owned by the subscriber.</returns>
    public DataScanner CreateScanner(ILogger? logger = null) => DataScanner.Create(Region, logger);
}
