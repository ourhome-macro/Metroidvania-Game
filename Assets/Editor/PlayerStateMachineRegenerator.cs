using UnityEditor;
using UnityEngine;

public static class PlayerStateMachineRegenerator
{
    [MenuItem("Tools/Player/Rebuild Player State Machine (All)")]
    public static void RebuildAll()
    {
        PlayerAnimatorStateMachineSetup.SetupStateMachine();
        PlayerStateMachineQuickSetupTool.SetupSelectedPlayer();
        Debug.Log("[PlayerStateMachineRegenerator] Rebuild complete.");
    }

    [MenuItem("Tools/Player/Rebuild Player State Machine (All)", true)]
    private static bool ValidateRebuildAll()
    {
        return !EditorApplication.isPlaying;
    }
}
