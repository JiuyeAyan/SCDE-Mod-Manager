using System;

namespace SHCDESE.EventAPI.Buildings;

public class BuildingRefundEventArgs : EventHookBase
{
    // --- Input/Output Parameters ---
    public int BuildingId { get; set; }
    public int PlayerId { get; set; }
    public int Percentage { get; set; }

    public BuildingRefundEventArgs(EventHookPhase phase, int buildingId, int playerId, int percentage)
    {
        Phase = phase;
        BuildingId = buildingId;
        PlayerId = playerId;
        Percentage = percentage;

    }
}
