using System;

namespace SHCDESE.EventAPI.MapLoader;

public class MapLoadEventArgs : EventHookBase
{
    // --- Input/Output Parameters ---
    public UInt32 CampaignMapID { get; }
    public string FileName { get; }
    public IntPtr RetData { get; }
    public string MapName { get; }
    public byte bMultiplayerSave { get; }
    public int TrailType { get; }
    public int TrailID { get;}
    public byte AllowClassicBedouins { get; }

    // --- Return Value ---
    public UInt64 ReturnValue { get; set;  } = 0;

    public MapLoadEventArgs(EventHookPhase phase, UInt32 campaignMapID, string fileName, IntPtr retData, string mapName, byte bMultiplayerSave, int trailType, int trailID, byte allowClassicBedouins)
    {
        Phase = phase;
        CampaignMapID = campaignMapID;
        FileName = fileName;
        RetData = retData;
        MapName = mapName;
        this.bMultiplayerSave = bMultiplayerSave;
        TrailType = trailType;
        TrailID = trailID;
        AllowClassicBedouins = allowClassicBedouins;
    }
}
