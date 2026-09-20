namespace SHCDESE.EventAPI.Projectiles;

public class ProjectileDeleteEventArgs : EventHookBase
{
    // --- Input/Output Parameters ---
    public int ProjectileId { get; }

    public ProjectileDeleteEventArgs(EventHookPhase phase, int projectileId)
    {
        Phase = phase;
        ProjectileId = projectileId;
    }
}