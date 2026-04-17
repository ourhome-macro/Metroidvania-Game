using UnityEngine;

public static class JianmuMenuBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureUi()
    {
        if (Object.FindObjectOfType<JianmuMenuController>() != null)
        {
            return;
        }

        GameObject root = new GameObject("JianmuMenusUI");
        root.AddComponent<JianmuMenuController>();
    }
}
