using System;
using UnityEngine;

public static class GameEvents
{
    public static event Action<int, int> OnHpChanged;
    public static void HpChanged(int currentHp, int maxHp)
    {
        OnHpChanged?.Invoke(currentHp, maxHp);
    }

    public static event Action OnPlayerDeath;
    public static void PlayerDeath()
    {
        OnPlayerDeath?.Invoke();
    }

    public static event Action<GameObject> OnEnemyDeath;
    public static void EnemyDeath(GameObject enemy)
    {
        OnEnemyDeath?.Invoke(enemy);
    }

    public static event Action OnBossDeath;
    public static void BossDeath()
    {
        OnBossDeath?.Invoke();
    }

    public static event Action OnDashStart;
    public static void DashStart()
    {
        OnDashStart?.Invoke();
    }

    public static event Action OnDashEnd;
    public static void DashEnd()
    {
        OnDashEnd?.Invoke();
    }

    public static event Action<Transform> OnBossUltStart;
    public static void BossUltStart(Transform bossRoot)
    {
        OnBossUltStart?.Invoke(bossRoot);
    }

    public static event Action OnBossUltEnd;
    public static void BossUltEnd()
    {
        OnBossUltEnd?.Invoke();
    }

    public static event Action OnPerfectParry;
    public static void PerfectParry()
    {
        OnPerfectParry?.Invoke();
    }

    public static event Action OnEliteKilled;
    public static void EliteKilled()
    {
        OnEliteKilled?.Invoke();
    }

    public static event Action<Vector3> OnCheckpointReached;
    public static void CheckpointReached(Vector3 spawnPos)
    {
        OnCheckpointReached?.Invoke(spawnPos);
    }

    public static event Action<string> OnDoorInteracted;
    public static void DoorInteracted(string doorId)
    {
        OnDoorInteracted?.Invoke(doorId);
    }

    public static event Action<bool> OnGamePaused;
    public static void GamePaused(bool isPaused)
    {
        OnGamePaused?.Invoke(isPaused);
    }
}
