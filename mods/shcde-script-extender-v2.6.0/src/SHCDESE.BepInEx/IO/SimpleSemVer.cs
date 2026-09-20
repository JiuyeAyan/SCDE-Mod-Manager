using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SHCDESE.IO;

/// <summary>
/// A helper class to parse SemVer strings that System.Version cannot handle (e.g. 1.0.0-beta).
/// </summary>
internal class SimpleSemVer
{
    private readonly int major;
    private readonly int minor;
    private readonly int patch;
    private readonly string additional;

    public SimpleSemVer(string version)
    {
        try
        {
            string[] parts = version.Split('.');
            major = parts.Length > 0 ? int.Parse(parts[0]) : 0;
            minor = parts.Length > 1 ? int.Parse(parts[1]) : 0;

            string patchPart = parts.Length > 2 ? parts[2] : "0";

            if (patchPart.Contains("-"))
            {
                string[] special = patchPart.Split('-');
                patch = int.Parse(special[0]);
                additional = special.Length > 1 ? special[1] : "";
            }
            else
            {
                patch = int.Parse(patchPart);
                additional = "";
            }
        }
        catch
        {
            // Fallback for completely malformed strings
            major = 0; minor = 0; patch = 0; additional = "";
            throw new FormatException($"Invalid version string: {version}");
        }
    }

    public override string ToString()
    {
        StringBuilder sb = new StringBuilder();
        sb.Append(major);
        sb.Append('.');
        sb.Append(minor);
        sb.Append('.');
        sb.Append(patch);
        if (!string.IsNullOrEmpty(additional))
        {
            sb.Append('-');
            sb.Append(additional);
        }
        return sb.ToString();
    }

    // Checks if the current version has at least the same major, minor, and patch version
    public bool AtleastMajorMinorPatch(SimpleSemVer other)
    {
        if (major > other.major) return true;
        if (major < other.major) return false;

        if (minor > other.minor) return true;
        if (minor < other.minor) return false;

        if (patch > other.patch) return true;
        if (patch < other.patch) return false;

        // If we are here, Major, Minor, and Patch are exactly equal.
        // We consider this "At least", so we return true.
        // Note: This ignores the 'additional' (beta/alpha) tag for equality check, 
        // treating 1.0.0-beta as equal to 1.0.0 for the purpose of "should I update?".
        return true;
    }
}
