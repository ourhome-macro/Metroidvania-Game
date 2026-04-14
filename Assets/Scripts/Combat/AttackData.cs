using UnityEngine;

/// <summary>
/// 一次攻击的上下文信息：用于受击、防御、完美弹反判定。
/// </summary>
[System.Serializable]
public struct AttackData
{
    public int Damage;
    public EnemyCombat Attacker;
    public float AttackStartTime;
    public Vector2 HitPoint;

    public AttackData(int damage, EnemyCombat attacker, float attackStartTime, Vector2 hitPoint)
    {
        Damage = damage;
        Attacker = attacker;
        AttackStartTime = attackStartTime;
        HitPoint = hitPoint;
    }
}
