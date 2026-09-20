using SHCDESE.Interop;
using SHCDESE.Logging;
using Steamworks;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SHCDESE.API.Components.Workshop;

/// <summary>
/// Manages Steam Workshop item updates by hooking into Platform_Workshop
/// </summary>
public sealed class WorkshopUpdateManager
{
    private static readonly Lazy<WorkshopUpdateManager> _lazy = new(() => new WorkshopUpdateManager());
    public static WorkshopUpdateManager Instance => _lazy.Value;

    internal bool _updateCheckInProgress = false;
    private bool _restartRequired = false;

    private WorkshopUpdateManager()
    {

    }


    internal delegate List<string> Platform_Workshop_GetListOfSubscribedItemsPaths_Delegate(Platform_Workshop instance);

    internal void CheckAndDownloadUpdatesInitial(List<string> result)
    {
        if (!_updateCheckInProgress && !_restartRequired)
        {
            _updateCheckInProgress = true;
            CheckAndDownloadUpdates(result);
        }
    }

    /// <summary>
    /// Checks all subscribed items for updates and initiates downloads
    /// </summary>
    private void CheckAndDownloadUpdates(List<string> installedPaths)
    {
        try
        {
            LogHelper.Information("Checking for Steam Workshop updates...");

            // Get all subscribed items
            uint numSubscribedItems = SteamUGC.GetNumSubscribedItems();
            if (numSubscribedItems == 0)
            {
                LogHelper.Information("No subscribed workshop items found");
                return;
            }

            PublishedFileId_t[] subscribedItems = new PublishedFileId_t[numSubscribedItems];
            SteamUGC.GetSubscribedItems(subscribedItems, numSubscribedItems);

            List<PublishedFileId_t> itemsNeedingUpdate = new List<PublishedFileId_t>();
            bool anyUpdatesAvailable = false;

            // Check each item's state
            foreach (PublishedFileId_t fileId in subscribedItems)
            {
                uint itemState = SteamUGC.GetItemState(fileId);

                // Check if item needs update
                bool needsUpdate = (itemState & (uint)EItemState.k_EItemStateNeedsUpdate) != 0;
                bool isDownloading = (itemState & (uint)EItemState.k_EItemStateDownloading) != 0;
                bool isDownloadPending = (itemState & (uint)EItemState.k_EItemStateDownloadPending) != 0;

                if (needsUpdate && !isDownloading && !isDownloadPending)
                {
                    LogHelper.Information($"Workshop item {fileId.m_PublishedFileId} needs update. Initiating download...");

                    // Request download for this item
                    bool downloadStarted = SteamUGC.DownloadItem(fileId, bHighPriority: true);

                    if (downloadStarted)
                    {
                        itemsNeedingUpdate.Add(fileId);
                        anyUpdatesAvailable = true;
                        LogHelper.Information($"Download started for workshop item {fileId.m_PublishedFileId}");
                    }
                    else
                    {
                        LogHelper.Warning($"Failed to start download for workshop item {fileId.m_PublishedFileId}");
                    }
                }
                else if (isDownloading || isDownloadPending)
                {
                    LogHelper.Information($"Workshop item {fileId.m_PublishedFileId} is already downloading");
                    itemsNeedingUpdate.Add(fileId);
                    anyUpdatesAvailable = true;
                }
            }

            // If any updates were found, monitor downloads
            if (anyUpdatesAvailable)
            {
                LogHelper.Information($"Found {itemsNeedingUpdate.Count} items needing updates. Monitoring downloads...");
                MonitorDownloads(itemsNeedingUpdate);
            }
            else
            {
                LogHelper.Information("All workshop items are up to date");
            }
        }
        catch (Exception ex)
        {
            LogHelper.Error(ex, "Error checking for workshop updates");
        }
    }

    /// <summary>
    /// Monitors download progress and prompts restart when complete
    /// </summary>
    private void MonitorDownloads(List<PublishedFileId_t> itemsToMonitor)
    {
        // Use Unity coroutine to monitor downloads
        if (UnityEngine.Object.FindObjectOfType<MonoBehaviour>() != null)
        {
            UnityEngine.Object.FindObjectOfType<MonoBehaviour>()
                .StartCoroutine(MonitorDownloadsCoroutine(itemsToMonitor));
        }
    }

    /// <summary>
    /// Coroutine that monitors workshop item downloads
    /// </summary>
    private IEnumerator MonitorDownloadsCoroutine(List<PublishedFileId_t> itemsToMonitor)
    {
        const int maxWaitSeconds = 300; // 5 minutes timeout
        float elapsedTime = 0f;

        while (elapsedTime < maxWaitSeconds)
        {
            bool allComplete = true;
            int completedCount = 0;
            int totalCount = itemsToMonitor.Count;

            foreach (PublishedFileId_t fileId in itemsToMonitor)
            {
                uint itemState = SteamUGC.GetItemState(fileId);
                bool isDownloading = (itemState & (uint)EItemState.k_EItemStateDownloading) != 0;
                bool isDownloadPending = (itemState & (uint)EItemState.k_EItemStateDownloadPending) != 0;
                bool isInstalled = (itemState & (uint)EItemState.k_EItemStateInstalled) != 0;

                if (isDownloading || isDownloadPending)
                {
                    allComplete = false;

                    // Get download progress
                    ulong bytesDownloaded, bytesTotal;
                    if (SteamUGC.GetItemDownloadInfo(fileId, out bytesDownloaded, out bytesTotal))
                    {
                        float progress = bytesTotal > 0 ? (float)bytesDownloaded / bytesTotal * 100f : 0f;
                        LogHelper.Debug($"Workshop item {fileId.m_PublishedFileId}: {progress:F1}% ({bytesDownloaded}/{bytesTotal} bytes)");
                    }
                }
                else if (isInstalled)
                {
                    completedCount++;
                }
            }

            if (allComplete)
            {
                LogHelper.Information($"All workshop items updated successfully ({completedCount}/{totalCount})");
                PromptRestart();
                yield break;
            }

            // Log progress every 5 seconds
            if (Mathf.FloorToInt(elapsedTime) % 5 == 0)
            {
                LogHelper.Information($"Workshop download progress: {completedCount}/{totalCount} items complete");
            }

            yield return new WaitForSeconds(1f);
            elapsedTime += 1f;
        }

        // Timeout reached
        LogHelper.Warning($"Workshop download monitoring timed out after {maxWaitSeconds} seconds");

        // Check if any items completed despite timeout
        int finalCompleted = 0;
        foreach (PublishedFileId_t fileId in itemsToMonitor)
        {
            uint itemState = SteamUGC.GetItemState(fileId);
            bool isInstalled = (itemState & (uint)EItemState.k_EItemStateInstalled) != 0;
            if (isInstalled) finalCompleted++;
        }

        if (finalCompleted > 0)
        {
            LogHelper.Information($"{finalCompleted}/{itemsToMonitor.Count} items completed before timeout");
            PromptRestart();
        }
    }

    /// <summary>
    /// Shows a message box prompting the user to restart the game
    /// </summary>
    private void PromptRestart()
    {
        if (_restartRequired)
            return;

        _restartRequired = true;

        try
        {
            LogHelper.Information("Workshop items have been updated. Prompting user to restart...");

            MinWinAPI.MessageBoxA(
                IntPtr.Zero,
                "Steam Workshop items have been updated.\nThe game will now restart to apply changes.",
                "Script Extender",
                0
            );

            RestartGame();
        }
        catch (Exception ex)
        {
            LogHelper.Error(ex, "Failed to prompt restart after workshop update");
        }
    }

    /// <summary>
    /// Restarts the game via Steam launcher
    /// </summary>
    private void RestartGame()
    {
        try
        {
            // Use Steam's URL protocol to restart the game through Steam
            string steamLaunchUrl = "steam://run/3024040"; 

            LogHelper.Information($"Restarting game via Steam: {steamLaunchUrl}");

            // Start the Steam URL which will launch the game
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = steamLaunchUrl,
                UseShellExecute = true
            });

            // Small delay to ensure Steam processes the launch command
            //System.Threading.Thread.Sleep(500);

            // Exit current instance
            System.Diagnostics.Process.GetCurrentProcess().Kill();
        }
        catch (Exception ex)
        {
            LogHelper.Fatal(ex, "Failed to restart game via Steam after workshop update");
        }
    }
}
