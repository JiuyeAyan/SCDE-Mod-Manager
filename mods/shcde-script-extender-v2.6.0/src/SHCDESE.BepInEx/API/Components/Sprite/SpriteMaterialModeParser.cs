using SHCDESE.API.Components.Sprite;
using System;

namespace SHCDESE.API.Components.Sprite;

internal static class SpriteMaterialModeParser
{
    internal static bool TryParse(string? value, out SpriteMaterialMode mode)
    {
        mode = SpriteMaterialMode.Auto;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        // Accept the American spelling as a convenience while keeping the public name aligned with the game's shader.
        if (string.Equals(value, "TeamColor", StringComparison.OrdinalIgnoreCase))
        {
            mode = SpriteMaterialMode.TeamColour;
            return true;
        }

        return Enum.TryParse(value, true, out mode) && Enum.IsDefined(typeof(SpriteMaterialMode), mode);
    }
}
