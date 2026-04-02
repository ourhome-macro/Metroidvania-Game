using UnityEngine;

[CreateAssetMenu(fileName = "PlayerConfig", menuName = "Metroidvania Lite/Data/Player Config")]
public class PlayerConfigSO : ScriptableObject
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 8f;
    [SerializeField] private float groundAcceleration = 80f;
    [SerializeField] private float groundDeceleration = 100f;
    [SerializeField] private float airAcceleration = 50f;

    [Header("Jump")]
    [SerializeField] private float jumpForce = 15f;
    [SerializeField] private float coyoteTime = 0.1f;
    [SerializeField] private float jumpBufferTime = 0.1f;
    [SerializeField] private float fallMultiplier = 2.2f;
    [SerializeField] private float lowJumpMultiplier = 3f;
    [SerializeField] private float maxFallSpeed = 24f;

    [Header("Combat / Stats")]
    [SerializeField] private int maxHp = 100;
    [SerializeField] private float attackDuration = 0.2f;
    [SerializeField] private Vector2 attackKnockback = new Vector2(4f, 2f);

    public float MoveSpeed => moveSpeed;
    public float GroundAcceleration => groundAcceleration;
    public float GroundDeceleration => groundDeceleration;
    public float AirAcceleration => airAcceleration;
    public float JumpForce => jumpForce;
    public float CoyoteTime => coyoteTime;
    public float JumpBufferTime => jumpBufferTime;
    public float FallMultiplier => fallMultiplier;
    public float LowJumpMultiplier => lowJumpMultiplier;
    public float MaxFallSpeed => maxFallSpeed;
    public int MaxHp => maxHp;
    public float AttackDuration => attackDuration;
    public Vector2 AttackKnockback => attackKnockback;
}