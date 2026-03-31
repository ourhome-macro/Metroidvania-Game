using UnityEngine;

[CreateAssetMenu(fileName = "EnemyConfig", menuName = "Metroidvania Lite/Data/Enemy Config")]
public class EnemyConfigSO : ScriptableObject
{
    [SerializeField] private float moveSpeed = 2.5f;
    [SerializeField] private float chaseRange = 6f;
    [SerializeField] private float attackRange = 1.2f;
    [SerializeField] private int damage = 10;
    [SerializeField] private float attackCooldown = 1f;

    public float MoveSpeed => moveSpeed;
    public float ChaseRange => chaseRange;
    public float AttackRange => attackRange;
    public int Damage => damage;
    public float AttackCooldown => attackCooldown;
}