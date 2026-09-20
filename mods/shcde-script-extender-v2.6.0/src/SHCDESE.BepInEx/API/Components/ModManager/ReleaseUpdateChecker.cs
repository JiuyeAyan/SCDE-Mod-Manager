using System;
using System.Text.Json;
using System.Threading.Tasks;
using UnityEngine.Networking;

namespace SHCDESE.API.Components.ModManager;

/// <summary>
/// Resolves trusted GitHub and GitLab repository URLs to their release APIs and
/// compares the latest release tag with an installed mod version.
/// </summary>
internal static class ReleaseUpdateChecker
{
    private readonly struct ReleaseEndpoint(string url)
    {
        public string Url { get; } = url;
    }

    /// <summary>
    /// Returns the normalized tag for a newer release, or <see langword="null"/> when the installed version is current.
    /// </summary>
    /// <exception cref="FormatException">
    /// The repository URL, installed version, response, or release tag is invalid.
    /// </exception>
    internal static async Task<string?> GetNewerReleaseAsync(string? currentVersion, string? repositoryUrl)
    {
        if (!TryCreateEndpoint(repositoryUrl, out ReleaseEndpoint endpoint))
        {
            throw new FormatException("VersionCheckUrl must be an HTTPS github.com or gitlab.com repository URL.");
        }

        if (!GameAssetModManager.TryParseModVersion(currentVersion, out Version installedVersion))
        {
            throw new FormatException($"Installed mod version [{currentVersion ?? "<null>"}] is not comparable.");
        }

        string json = await SendGetAsync(endpoint.Url);
        if (!TryReadLatestTag(json, out string latestTag) ||
            !GameAssetModManager.TryParseModVersion(latestTag, out Version latestVersion))
        {
            throw new FormatException("The latest release has no comparable semantic-version tag.");
        }

        return latestVersion > installedVersion ? NormalizeVersionTag(latestTag) : null;
    }

    /// <summary>
    /// Accepts repository links only. The request target is derived locally so a mod cannot use update checking to send requests to an arbitrary host.
    /// </summary>
    private static bool TryCreateEndpoint(string? repositoryUrl, out ReleaseEndpoint endpoint)
    {
        endpoint = default;

        if (string.IsNullOrWhiteSpace(repositoryUrl) || repositoryUrl.Length > 2048 ||
            !Uri.TryCreate(repositoryUrl.Trim(), UriKind.Absolute, out Uri? repository) ||
            !string.Equals(repository.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
            !repository.IsDefaultPort ||
            !string.IsNullOrEmpty(repository.UserInfo) ||
            !string.IsNullOrEmpty(repository.Query) ||
            !string.IsNullOrEmpty(repository.Fragment))
        {
            return false;
        }

        string[] pathParts = Uri.UnescapeDataString(repository.AbsolutePath).Split(['/'], StringSplitOptions.RemoveEmptyEntries);

        if (IsHost(repository, "github.com"))
        {
            if (!TryGetGitHubProject(pathParts, out string owner, out string project))
                return false;

            endpoint = new ReleaseEndpoint($"https://api.github.com/repos/{Uri.EscapeDataString(owner)}/{Uri.EscapeDataString(project)}/releases/latest");
            return true;
        }

        if (IsHost(repository, "gitlab.com"))
        {
            if (!TryGetGitLabProject(pathParts, out string projectPath))
                return false;

            endpoint = new ReleaseEndpoint($"https://gitlab.com/api/v4/projects/{Uri.EscapeDataString(projectPath)}/releases/permalink/latest");
            return true;
        }

        return false;
    }

    private static bool IsHost(Uri uri, string host) => string.Equals(uri.Host, host, StringComparison.OrdinalIgnoreCase) || string.Equals(uri.Host, "www." + host, StringComparison.OrdinalIgnoreCase);

    private static bool TryGetGitHubProject(string[] pathParts, out string owner, out string project)
    {
        owner = string.Empty;
        project = string.Empty;

        if (pathParts.Length < 2)
            return false;

        int projectPathLength = pathParts.Length;
        if (pathParts.Length >= 3 && string.Equals(pathParts[2], "releases", StringComparison.OrdinalIgnoreCase))
            projectPathLength = 2;

        if (projectPathLength != 2)
            return false;

        owner = pathParts[0];
        project = TrimGitSuffix(pathParts[1]);
        return IsValidPathPart(owner) && IsValidPathPart(project);
    }

    private static bool TryGetGitLabProject(string[] pathParts, out string projectPath)
    {
        projectPath = string.Empty;
        if (pathParts.Length < 2)
            return false;

        int projectPathLength = pathParts.Length;
        for (int i = 2; i + 1 < pathParts.Length; i++)
        {
            if (pathParts[i] == "-" && string.Equals(pathParts[i + 1], "releases", StringComparison.OrdinalIgnoreCase))
            {
                projectPathLength = i;
                break;
            }
        }

        if (projectPathLength < 2)
            return false;

        string[] projectParts = new string[projectPathLength];
        Array.Copy(pathParts, projectParts, projectPathLength);
        projectParts[^1] = TrimGitSuffix(projectParts[^1]);

        foreach (string part in projectParts)
        {
            if (!IsValidPathPart(part) || part == "-")
                return false;
        }

        projectPath = string.Join("/", projectParts);
        return true;
    }

    private static bool IsValidPathPart(string part) => !string.IsNullOrWhiteSpace(part) && part != "." && part != ".." && part.IndexOfAny(['\r', '\n', '\0']) < 0;

    private static string TrimGitSuffix(string value) => value.EndsWith(".git", StringComparison.OrdinalIgnoreCase) ? value[..^4] : value;

    private static bool TryReadLatestTag(string json, out string tag)
    {
        tag = string.Empty;
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("tag_name", out JsonElement tagElement) || tagElement.ValueKind != JsonValueKind.String)
            return false;

        tag = tagElement.GetString() ?? string.Empty;
        return !string.IsNullOrWhiteSpace(tag);
    }

    private static string NormalizeVersionTag(string tag)
    {
        string normalized = tag.Trim();
        return normalized.Length > 1 && (normalized[0] == 'v' || normalized[0] == 'V') ? normalized[1..] : normalized;
    }

    internal static Task<string> SendGetAsync(string url)
    {
        TaskCompletionSource<string> completion = new();
        UnityWebRequest request = UnityWebRequest.Get(url);
        request.SetRequestHeader("User-Agent", "SHCDE-SE-UpdateCheck");
        request.SetRequestHeader("Accept", "application/json");
        request.timeout = 10;

        UnityWebRequestAsyncOperation operation = request.SendWebRequest();
        operation.completed += _ =>
        {
            try
            {
                if (request.result == UnityWebRequest.Result.Success)
                    completion.TrySetResult(request.downloadHandler.text);
                else
                    completion.TrySetException(new Exception($"UnityWebRequest failed: {request.error}"));
            }
            catch (Exception ex)
            {
                completion.TrySetException(ex);
            }
            finally
            {
                request.Dispose();
            }
        };

        return completion.Task;
    }
}
