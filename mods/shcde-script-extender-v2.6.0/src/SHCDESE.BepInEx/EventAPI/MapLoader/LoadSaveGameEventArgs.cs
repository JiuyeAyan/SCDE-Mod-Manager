using System;

namespace SHCDESE.EventAPI.MapLoader;

public class LoadSaveGameEventArgs : EventHookBase
{
    // --- Input/Output Parameters ---
    public string FileName { get; }
    public IntPtr RetData { get; }
    public bool LoadingEditorMap { get; }

    // --- Return Value ---
    public Int64 ReturnValue { get; set;  } = 0;

    public LoadSaveGameEventArgs(EventHookPhase phase, string data, IntPtr retData, bool loadingEditorMap)
    {
        Phase = phase;
        FileName = data;
        RetData = retData;
        LoadingEditorMap = loadingEditorMap;
    }
}
