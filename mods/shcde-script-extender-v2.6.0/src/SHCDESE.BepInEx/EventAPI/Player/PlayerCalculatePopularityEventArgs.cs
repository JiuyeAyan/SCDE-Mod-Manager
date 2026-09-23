namespace SHCDESE.EventAPI.Player;

public class PlayerCalculatePopularityEventArgs : EventHookBase
{
    // --- Input/Output Parameters ---
    public int PlayerId { get; }
    public int Popularity { get; set; }

    public PlayerCalculatePopularityEventArgs(EventHookPhase phase, int playerId, int popularity)
    {
        Phase = phase;
        PlayerId = playerId;
        Popularity = popularity;
    }
}
