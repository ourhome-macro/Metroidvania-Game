using UnityEngine;

public static class JianmuHealthBarBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureUi()
    {
        if (Object.FindObjectOfType<JianmuHealthBarController>() != null)
        {
            return;
        }

        GameObject root = new GameObject("JianmuHealthBarUI");
        root.AddComponent<JianmuHealthBarController>();
    }
}
