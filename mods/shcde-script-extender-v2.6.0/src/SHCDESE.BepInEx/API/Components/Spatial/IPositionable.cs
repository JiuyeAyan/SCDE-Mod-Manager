using SHCDESE.Interop;
using System;

namespace SHCDESE.API.Components.Spatial;

public unsafe interface IPositionable
{
    UnmanagedVector2<UInt16>* CurrentTilePosition();
}