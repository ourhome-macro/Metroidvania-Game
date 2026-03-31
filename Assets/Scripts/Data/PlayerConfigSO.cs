using UnityEngine;

[CreateAssetMenu(fileName = "PlayerConfig", menuName = "Metroidvania Lite/Data/Player Config")]
public class PlayerConfigSO : ScriptableObject
{
    [SerializeField] private float moveSpeed = 6f;
    [SerializeField] private float jumpForce = 12f;
    [SerializeField] private int maxHp = 100;
    [SerializeField] private float attackDuration = 0.2f;
    [SerializeField] private Vector2 attackKnockback = new Vector2(4f, 2f);

    public float MoveSpeed => moveSpeed;
    public float JumpForce => jumpForce;
    public int MaxHp => maxHp;
    public float AttackDuration => attackDuration;
    public Vector2 AttackKnockback => attackKnockback;
}