using UnityEngine;

[CreateAssetMenu(fileName = "BossConfig", menuName = "Metroidvania Lite/Data/Boss Config")]
public class BossConfigSO : ScriptableObject
{
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private int phase2HpThreshold = 50;
    [SerializeField] private float skillCooldown = 2.5f;
    [SerializeField] private int projectileCount = 5;

    public float MoveSpeed => moveSpeed;
    public int Phase2HpThreshold => phase2HpThreshold;
    public float SkillCooldown => skillCooldown;
    public int ProjectileCount => projectileCount;
}