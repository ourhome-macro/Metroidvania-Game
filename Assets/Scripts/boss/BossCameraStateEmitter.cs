using UnityEngine;

public class BossCameraStateEmitter : MonoBehaviour
{
    [SerializeField] private Transform bossRoot;
    [SerializeField] private bool autoSignalEndOnDisable = true;

    private bool ultActive;

    private void Awake()
    {
        if (bossRoot == null)
        {
            bossRoot = transform;
        }
    }

    public void SignalBossUltStart()
    {
        if (ultActive)
        {
            return;
        }

        ultActive = true;
        GameEvents.BossUltStart(bossRoot != null ? bossRoot : transform);
    }

    public void SignalBossUltEnd()
    {
        if (!ultActive)
        {
            return;
        }

        ultActive = false;
        GameEvents.BossUltEnd();
    }

    private void OnDisable()
    {
        if (!autoSignalEndOnDisable || !ultActive)
        {
            return;
        }

        ultActive = false;
        GameEvents.BossUltEnd();
    }
}
