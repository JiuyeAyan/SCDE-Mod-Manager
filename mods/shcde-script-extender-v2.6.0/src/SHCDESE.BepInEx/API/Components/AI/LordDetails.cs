namespace SHCDESE.API.Components.AI;

/// <summary>
/// Contains the localized descriptive texts shown for a custom lord in the skirmish lobby.
/// </summary>
public sealed class LordDetails
{
    public string Description { get; set; } = string.Empty;
    public string DifficultyRating { get; set; } = string.Empty;
    public string FavouriteTroops { get; set; } = string.Empty;
    public string Castles { get; set; } = string.Empty;
    public string PlayStyle { get; set; } = string.Empty;
    public string FavouriteSaying { get; set; } = string.Empty;
}
