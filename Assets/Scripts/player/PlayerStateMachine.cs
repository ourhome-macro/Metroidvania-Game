using System;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerController2D))]
[RequireComponent(typeof(PlayerCombat))]
public class PlayerStateMachine : MonoBehaviour
{
    public enum PlayerState
    {
        Idle = 0,
        Run = 1,
        Jump = 2,
        Fall = 3,
        Defend = 4,
        Attack = 5,
        Skip = 6,
        Hit = 7,
        Dead = 8
    }

    [Header("Refs")]
    [SerializeField] private PlayerController2D playerController;
    [SerializeField] private PlayerCombat playerCombat;
    [SerializeField] private Animator animator;

    [Header("Evaluate")]
    [SerializeField] private float runSpeedThreshold = 0.1f;
    [SerializeField] private float jumpUpSpeedThreshold = 0.05f;

    [Header("Animator Sync")]
    [SerializeField] private bool syncAnimatorIntParameter;
    [SerializeField] private string animatorIntParameterName = "playerState";

    [Header("Debug")]
    [SerializeField] private bool logStateChange;
    [SerializeField] private PlayerState currentState = PlayerState.Idle;

    private int animatorStateParamHash;
    private bool hasAnimatorStateIntParam;

    public PlayerState CurrentState => currentState;
    public event Action<PlayerState, PlayerState> OnStateChanged;

    private void Reset()
    {
        ResolveReferences();
        CacheAnimatorStateParameter();
    }

    private void Awake()
    {
        ResolveReferences();
        CacheAnimatorStateParameter();
        currentState = EvaluateCurrentState();
        SyncAnimatorState(currentState);
    }

    private void OnValidate()
    {
        runSpeedThreshold = Mathf.Max(0f, runSpeedThreshold);
        jumpUpSpeedThreshold = Mathf.Max(0f, jumpUpSpeedThreshold);
    }

    private void LateUpdate()
    {
        PlayerState next = EvaluateCurrentState();
        if (next == currentState)
        {
            return;
        }

        PlayerState previous = currentState;
        currentState = next;
        SyncAnimatorState(next);
        OnStateChanged?.Invoke(previous, next);

        if (logStateChange)
        {
            Debug.Log($"[PlayerStateMachine] {previous} -> {next}", this);
        }
    }

    private PlayerState EvaluateCurrentState()
    {
        if (playerCombat != null)
        {
            if (playerCombat.IsDead)
            {
                return PlayerState.Dead;
            }

            if (playerCombat.IsHitStunned)
            {
                return PlayerState.Hit;
            }

            if (playerCombat.IsSkipping)
            {
                return PlayerState.Skip;
            }

            if (playerCombat.IsAttacking)
            {
                return PlayerState.Attack;
            }

            if (playerCombat.IsDefending)
            {
                return PlayerState.Defend;
            }
        }

        if (playerController == null)
        {
            return PlayerState.Idle;
        }

        Vector2 velocity = playerController.Velocity;
        if (!playerController.IsGrounded)
        {
            return velocity.y > jumpUpSpeedThreshold ? PlayerState.Jump : PlayerState.Fall;
        }

        float horizontalSpeed = Mathf.Abs(velocity.x);
        float horizontalInput = Mathf.Abs(playerController.MoveInput);
        if (horizontalSpeed > runSpeedThreshold || horizontalInput > 0.01f)
        {
            return PlayerState.Run;
        }

        return PlayerState.Idle;
    }

    private void SyncAnimatorState(PlayerState state)
    {
        if (!syncAnimatorIntParameter || animator == null || !hasAnimatorStateIntParam)
        {
            return;
        }

        animator.SetInteger(animatorStateParamHash, (int)state);
    }

    private void ResolveReferences()
    {
        if (playerController == null)
        {
            playerController = GetComponent<PlayerController2D>();
        }

        if (playerCombat == null)
        {
            playerCombat = GetComponent<PlayerCombat>();
        }

        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }
    }

    private void CacheAnimatorStateParameter()
    {
        hasAnimatorStateIntParam = false;
        animatorStateParamHash = 0;

        if (animator == null || string.IsNullOrWhiteSpace(animatorIntParameterName))
        {
            return;
        }

        AnimatorControllerParameter[] parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            AnimatorControllerParameter parameter = parameters[i];
            if (parameter.type == AnimatorControllerParameterType.Int && parameter.name == animatorIntParameterName)
            {
                hasAnimatorStateIntParam = true;
                animatorStateParamHash = Animator.StringToHash(animatorIntParameterName);
                return;
            }
        }
    }
}
