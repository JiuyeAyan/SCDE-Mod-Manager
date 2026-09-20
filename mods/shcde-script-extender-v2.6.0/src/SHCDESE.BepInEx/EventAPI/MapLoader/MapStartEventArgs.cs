using System;

namespace SHCDESE.EventAPI.MapLoader;

public class MapStartEventArgs : EventHookBase
{
    // --- Input/Output Parameters ---
    public IntPtr Unknown1 { get; }
    public byte bMultiplayerSave { get; }

    public UInt64 Unknown3 { get; }

    public int CampaignMapId { get; }

    // --- Return Value ---
    public UInt64 ReturnValue { get; set; } = 0;

    public MapStartEventArgs(EventHookPhase phase, IntPtr unknown1, byte bMultiplayerSave, UInt64 unknown3, int campaignMapId = 0)
    {
        Phase = phase;
        Unknown1 = unknown1;
        this.bMultiplayerSave = bMultiplayerSave;
        Unknown3 = unknown3;
        CampaignMapId = campaignMapId;
    }
}
