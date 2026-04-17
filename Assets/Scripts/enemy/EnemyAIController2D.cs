using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class EnemyAIController2D : MonoBehaviour
{
    private enum EnemyState
    {
        IdlePatrol,
        Chase,
        Attack
    }

    [Header("Config")]
    [SerializeField] private EnemyConfigSO config;

    [Header("Refs")]
    [SerializeField] private Transform playerTarget;
    [SerializeField] private EnemyCombat enemyCombat;
    [SerializeField] private Animator animator;
    [SerializeField] private Transform visualRoot;

    [Header("Detection")]
    [SerializeField] private bool autoFindPlayerByTag = true;
    [SerializeField] private float discoveryRangeOverride = -1f;
    [SerializeField] private float meleeRangeOverride = -1f;
    [SerializeField] private float meleeRangeBuffer = 0.35f;
    [SerializeField] private float chaseStopBuffer = 0.2f;
    [SerializeField] private float attackVerticalTolerance = 2.5f;
    [SerializeField] private float loseTargetRangeMultiplier = 1.25f;

    [Header("Patrol")]
    [SerializeField] private bool enablePatrol = true;
    [SerializeField] private float patrolDistance = 2f;
    [SerializeField] private float patrolIdleDuration = 0.8f;
    [SerializeField] private float patrolSpeedMultiplier = 0.5f;
    [SerializeField] private float arriveThreshold = 0.1f;

    [Header("Turning")]
    [SerializeField] private float turnLockDuration = 0.22f;

    [Header("Animator Params")]
    [SerializeField] private string runningBoolName = "isRunning";

    private Rigidbody2D rb;
    private EnemyState currentState = EnemyState.IdlePatrol;
    private Vector2 spawnPosition;
    private int patrolDirection = 1;
    private float patrolIdleTimer;
    private bool hasRunningParam;
    private int facingDirection = 1;
    private int pendingFacingDirection = 1;
    private float turnLockTimer;
    private bool attackFacingLocked;
    private int attackFacingDirection = 1;

    private float DiscoveryRange => discoveryRangeOverride > 0f ? discoveryRangeOverride : (config != null ? config.ChaseRange : 6f);
    private float MeleeRange => meleeRangeOverride > 0f ? meleeRangeOverride : (config != null ? config.AttackRange : 1.2f);
    private float ChaseSpeed => config != null ? config.MoveSpeed : 2.5f;
    private float PatrolSpeed => ChaseSpeed * Mathf.Clamp01(patrolSpeedMultiplier);

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        spawnPosition = transform.position;

        if (enemyCombat == null)
        {
            enemyCombat = GetComponent<EnemyCombat>();
        }

        if (animator == null)
        {
            animator = FindBestAnimator();
        }

        if (animator != null)
        {
            Animator preferred = FindBestAnimator();
            if (preferred != null)
            {
                animator = preferred;
            }
        }

        if (visualRoot == null)
        {
            visualRoot = FindVisualRoot();
        }

        if (enemyCombat != null)
        {
            enemyCombat.SetInternalAiEnabled(false);
        }

        if (visualRoot != null)
        {
            facingDirection = visualRoot.localScale.x >= 0f ? 1 : -1;
            pendingFacingDirection = facingDirection;
        }

        if (enemyCombat != null)
        {
            enemyCombat.SetFacingDirection(facingDirection);
        }

        CacheAnimatorParams();
    }

    private void Update()
    {
        if (enemyCombat != null && enemyCombat.IsDead)
        {
            StopHorizontal();
            SetRunning(false);
            return;
        }

        EnsurePlayerTarget();
        TickTurnTimer();
        UpdateState();
        PushCombatStateFlags();
        TickState();
        UpdateAnimator();
    }

    private void EnsurePlayerTarget()
    {
        if (playerTarget != null || !autoFindPlayerByTag)
        {
            return;
        }

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            playerTarget = playerObj.transform;
        }
    }

    private void UpdateState()
    {
        if (enemyCombat != null && enemyCombat.IsAttacking)
        {
            currentState = EnemyState.Attack;
            return;
        }

        if (playerTarget == null)
        {
            currentState = EnemyState.IdlePatrol;
            return;
        }

        float distance = Vector2.Distance(transform.position, playerTarget.position);
        float horizontalDistance = Mathf.Abs(playerTarget.position.x - transform.position.x);
        float verticalDistance = Mathf.Abs(playerTarget.position.y - transform.position.y);
        float discoverRange = DiscoveryRange;
        float loseRange = discoverRange * Mathf.Max(1f, loseTargetRangeMultiplier);

        if (IsInMeleeRange())
        {
            currentState = EnemyState.Attack;
            return;
        }

        if (enemyCombat != null && enemyCombat.ShouldStartSpecialAttack(horizontalDistance, verticalDistance))
        {
            currentState = EnemyState.Attack;
            return;
        }

        if (distance <= discoverRange)
        {
            currentState = EnemyState.Chase;
            return;
        }

        if (currentState == EnemyState.Chase && distance <= loseRange)
        {
            return;
        }

        currentState = EnemyState.IdlePatrol;
    }

    private void TickState()
    {
        switch (currentState)
        {
            case EnemyState.IdlePatrol:
                TickIdlePatrol();
                break;

            case EnemyState.Chase:
                TickChase();
                break;

            case EnemyState.Attack:
                TickAttack();
                break;
        }
    }

    private void PushCombatStateFlags()
    {
        if (enemyCombat == null)
        {
            return;
        }

        enemyCombat.SetContactDamageActive(currentState == EnemyState.Chase);
    }

    private void TickIdlePatrol()
    {
        if (!enablePatrol)
        {
            StopHorizontal();
            return;
        }

        if (patrolIdleTimer > 0f)
        {
            patrolIdleTimer -= Time.deltaTime;
            StopHorizontal();
            return;
        }

        float targetX = spawnPosition.x + patrolDirection * Mathf.Max(0.1f, patrolDistance);
        float deltaX = targetX - transform.position.x;

        if (Mathf.Abs(deltaX) <= arriveThreshold)
        {
            patrolDirection *= -1;
            patrolIdleTimer = Mathf.Max(0f, patrolIdleDuration);
            StopHorizontal();
            return;
        }

        float direction = Mathf.Sign(deltaX);
        if (!EnsureFacingDirection(direction))
        {
            StopHorizontal();
            return;
        }

        MoveHorizontal(direction, PatrolSpeed);
    }

    private void TickChase()
    {
        if (playerTarget == null)
        {
            StopHorizontal();
            return;
        }

        float closeStopDistance = MeleeRange + Mathf.Max(0f, meleeRangeBuffer) + Mathf.Max(0f, chaseStopBuffer);
        float distance = Vector2.Distance(transform.position, playerTarget.position);
        if (distance <= closeStopDistance)
        {
            StopHorizontal();
            return;
        }

        float deltaX = playerTarget.position.x - transform.position.x;
        if (Mathf.Abs(deltaX) <= arriveThreshold)
        {
            StopHorizontal();
            return;
        }

        float direction = Mathf.Sign(deltaX);
        if (!EnsureFacingDirection(direction))
        {
            StopHorizontal();
            return;
        }

        MoveHorizontal(direction, ChaseSpeed);
    }

    private void TickAttack()
    {
        StopHorizontal();

        if (enemyCombat == null)
        {
            return;
        }

        if (enemyCombat.IsAttacking)
        {
            if (attackFacingLocked)
            {
                enemyCombat.SetFacingDirection(attackFacingDirection);
            }

            return;
        }

        if (attackFacingLocked)
        {
            attackFacingLocked = false;
        }

        if (playerTarget != null)
        {
            float deltaX = playerTarget.position.x - transform.position.x;
            if (Mathf.Abs(deltaX) > 0.01f && !EnsureFacingDirection(Mathf.Sign(deltaX)))
            {
                return;
            }
        }

        if (enemyCombat.TryStartAttack())
        {
            attackFacingLocked = true;
            attackFacingDirection = facingDirection;
            enemyCombat.SetFacingDirection(attackFacingDirection);
        }
    }

    private void MoveHorizontal(float direction, float speed)
    {
        Vector2 velocity = rb.velocity;
        velocity.x = direction * Mathf.Max(0f, speed);
        rb.velocity = velocity;
    }

    private void StopHorizontal()
    {
        Vector2 velocity = rb.velocity;
        velocity.x = 0f;
        rb.velocity = velocity;
    }

    private void UpdateAnimator()
    {
        bool running = turnLockTimer <= 0f && (currentState == EnemyState.Chase || Mathf.Abs(rb.velocity.x) > 0.05f);

        if (hasRunningParam)
        {
            SetRunning(running);
        }
    }

    private bool IsInMeleeRange()
    {
        if (playerTarget == null)
        {
            return false;
        }

        Vector2 delta = playerTarget.position - transform.position;
        float horizontal = Mathf.Abs(delta.x);
        float vertical = Mathf.Abs(delta.y);
        return horizontal <= MeleeRange + Mathf.Max(0f, meleeRangeBuffer) && vertical <= Mathf.Max(0f, attackVerticalTolerance);
    }

    private bool EnsureFacingDirection(float desiredDirection)
    {
        if (Mathf.Abs(desiredDirection) <= 0.01f)
        {
            return true;
        }

        int desiredSign = desiredDirection > 0f ? 1 : -1;
        if (desiredSign == facingDirection)
        {
            return true;
        }

        if (turnLockTimer > 0f)
        {
            return false;
        }

        pendingFacingDirection = desiredSign;
        turnLockTimer = Mathf.Max(0f, turnLockDuration);

        if (turnLockTimer <= 0f)
        {
            ApplyFacing(pendingFacingDirection);
        }

        return false;
    }

    private void TickTurnTimer()
    {
        if (turnLockTimer <= 0f)
        {
            return;
        }

        turnLockTimer -= Time.deltaTime;
        if (turnLockTimer <= 0f)
        {
            ApplyFacing(pendingFacingDirection);
        }
    }

    private void ApplyFacing(int sign)
    {
        facingDirection = sign >= 0 ? 1 : -1;

        if (enemyCombat != null)
        {
            enemyCombat.SetFacingDirection(facingDirection);
        }

        if (visualRoot == null)
        {
            return;
        }

        Vector3 scale = visualRoot.localScale;
        scale.x = Mathf.Abs(scale.x) * facingDirection;
        visualRoot.localScale = scale;
    }

    private Animator FindBestAnimator()
    {
        Transform bossVisual = transform.Find("BossVisual");
        if (bossVisual != null)
        {
            Animator visualAnimator = bossVisual.GetComponent<Animator>();
            if (visualAnimator != null)
            {
                return visualAnimator;
            }
        }

        Animator[] animators = GetComponentsInChildren<Animator>(true);
        for (int i = 0; i < animators.Length; i++)
        {
            if (animators[i] != null && animators[i].gameObject != gameObject)
            {
                return animators[i];
            }
        }

        return GetComponent<Animator>();
    }

    private Transform FindVisualRoot()
    {
        Transform bossVisual = transform.Find("BossVisual");
        if (bossVisual != null)
        {
            return bossVisual;
        }

        SpriteRenderer childRenderer = GetComponentInChildren<SpriteRenderer>(true);
        if (childRenderer != null)
        {
            return childRenderer.transform;
        }

        return transform;
    }

    private void CacheAnimatorParams()
    {
        hasRunningParam = false;
        if (animator == null || string.IsNullOrEmpty(runningBoolName))
        {
            return;
        }

        AnimatorControllerParameter[] parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].name == runningBoolName && parameters[i].type == AnimatorControllerParameterType.Bool)
            {
                hasRunningParam = true;
                return;
            }
        }
    }

    private void SetRunning(bool isRunning)
    {
        if (!hasRunningParam || animator == null)
        {
            return;
        }

        animator.SetBool(runningBoolName, isRunning);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, DiscoveryRange);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, MeleeRange);

        if (!enablePatrol)
        {
            return;
        }

        Vector3 left = new Vector3(transform.position.x - patrolDistance, transform.position.y, transform.position.z);
        Vector3 right = new Vector3(transform.position.x + patrolDistance, transform.position.y, transform.position.z);
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(left, right);
        Gizmos.DrawWireSphere(left, 0.08f);
        Gizmos.DrawWireSphere(right, 0.08f);
    }
}
