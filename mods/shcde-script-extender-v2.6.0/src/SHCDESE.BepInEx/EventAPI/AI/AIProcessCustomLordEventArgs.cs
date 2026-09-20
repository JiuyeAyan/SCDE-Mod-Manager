namespace SHCDESE.EventAPI.AI;

public class AIProcessCustomLordEventArgs : EventHookBase
{
    public string CustomPath { get; }
    public CustomisationFileManager.CustomLord CustomLord { get; }

    public AIProcessCustomLordEventArgs(EventHookPhase phase, string customPath, CustomisationFileManager.CustomLord customLord)
    {
        Phase = phase;
        CustomPath = customPath;
        CustomLord = customLord;
    }
}
