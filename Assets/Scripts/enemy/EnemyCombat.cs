using System.Collections;
using UnityEngine;

public class EnemyCombat : MonoBehaviour
{
    private enum AttackMode
    {
        Attack,
        Attack2,
        GroundSlam
    }

    [Header("Health")]
    [SerializeField] private int maxHealth = 60;

    [Header("Attack")]
    [SerializeField] private int attackDamage = 15;
    [SerializeField] private float attackCooldown = 1.6f;
    [SerializeField] private float attackWindup = 0.35f;
    [SerializeField] private float attackRecovery = 0.3f;
    [SerializeField] private float minimumEffectiveAttackCooldown = 2.2f;
    [SerializeField] private float minimumEffectiveRecovery = 0.45f;
    [SerializeField] private float minimumEffectiveAttackRange = 1.15f;
    [SerializeField] private Transform attackPoint;
    [SerializeField] private float attackRange = 0.8f;
    [SerializeField] private LayerMask playerLayer;

    [Header("Fast/Slow Slash")]
    [SerializeField, Range(0f, 1f)] private float slowSlashChance = 0.35f;
    [SerializeField] private float raisePoseTime = 0.33f;
    [SerializeField] private Vector2 slowSlashPauseRange = new Vector2(0.18f, 0.32f);

    [Header("Attack Hit Timing")]
    [SerializeField] private float attackClipFps = 12f;
    [SerializeField] private int attackHitFrame = 5;
    [SerializeField] private float hitboxActiveDuration = 0.08f;
    [SerializeField] private bool hideAttackPointOutsideAttack = true;

    [Header("Facing")]
    [SerializeField] private bool mirrorHitPointsWithFacing = true;

    [Header("Contact Damage")]
    [SerializeField] private bool enableContactDamage = true;
    [SerializeField] private int contactDamage = 6;
    [SerializeField] private float contactDamageCooldown = 0.8f;
    [SerializeField] private float contactDamageRange = 0.45f;
    [SerializeField] private float maximumEffectiveContactDamageRange = 0.75f;
    [SerializeField] private Transform contactPoint;
    [SerializeField] private bool requirePhysicalTouchForContactDamage = true;

    [Header("Animator Params")]
    [SerializeField] private string attackTriggerName = "Attack";
    [SerializeField] private string attack2TriggerName = "Attack2";
    [SerializeField] private string groundSlamTriggerName = "GroundSlam";
    [SerializeField] private string hitTriggerName = "isHit";

    [Header("Boss Skill Routing")]
    [SerializeField] private float attack2MinDistance = 1.8f;
    [SerializeField] private float groundSlamMinDistance = 3.2f;
    [SerializeField] private float groundSlamCooldown = 2.8f;

    [Header("Ground Slam Jump")]
    [SerializeField] private float groundSlamJumpDuration = 0.4f;
    [SerializeField] private float groundSlamJumpHeight = 1.6f;

    [Header("AI")]
    [SerializeField] private bool useInternalAi = true;
    [SerializeField] private Transform playerTarget;
    [SerializeField] private float engageRange = 2f;

    [Header("Refs")]
    [SerializeField] private Animator animator;
    [SerializeField] private Collider2D bodyCollider;

    private int currentHealth;
    private float nextAttackAllowedTime = -999f;
    private float currentAttackStartTime = -999f;
    private bool isDead;
    private bool isAttacking;
    private bool animatorParamsCached;
    private bool hasAttackTriggerParam;
    private bool hasAttack2TriggerParam;
    private bool hasGroundSlamTriggerParam;
    private bool hasHitTriggerParam;
    private bool loggedMissingAttackTrigger;
    private bool loggedMissingAttack2Trigger;
    private bool loggedMissingGroundSlamTrigger;
    private bool loggedMissingHitTrigger;
    private Coroutine attackRoutine;
    private int attackToken;
    private bool contactDamageActiveByAi = true;
    private float nextContactDamageAllowedTime = -999f;
    private Vector3 attackPointBaseLocalPosition;
    private Vector3 contactPointBaseLocalPosition;
    private bool cachedHitPointLocalOffsets;
    private int facingDirection = 1;
    private bool attackPointVisible;
    private Rigidbody2D rb;
    private float nextGroundSlamAllowedTime = -999f;

    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;
    public bool IsDead => isDead;
    public bool IsAttacking => isAttacking;

    private float EffectiveAttackCooldown => Mathf.Max(attackCooldown, minimumEffectiveAttackCooldown);
    private float EffectiveAttackRecovery => Mathf.Max(attackRecovery, minimumEffectiveRecovery);
    private float EffectiveAttackRange => Mathf.Max(attackRange, minimumEffectiveAttackRange);
    private float EffectiveContactDamageRange => Mathf.Min(Mathf.Max(0.05f, contactDamageRange), Mathf.Max(0.05f, maximumEffectiveContactDamageRange));

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        if (animator == null)
        {
            animator = FindBestAnimator();
        }
        else
        {
            Animator preferred = FindBestAnimator();
            if (preferred != null)
            {
                animator = preferred;
            }
        }

        CacheAnimatorParams();
        CacheHitPointOffsets();

        if (bodyCollider == null)
        {
            bodyCollider = GetComponent<Collider2D>();
        }

        SetFacingDirection(facingDirection);
        if (attackPoint != null)
        {
            attackPointVisible = attackPoint.gameObject.activeSelf;
        }
        SetAttackPointVisible(!hideAttackPointOutsideAttack);

        currentHealth = Mathf.Max(1, maxHealth);

        if (playerTarget == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                playerTarget = playerObj.transform;
            }
        }
    }

    private void Update()
    {
        TickContactDamage();

        if (!useInternalAi)
        {
            return;
        }

        if (isDead || isAttacking || playerTarget == null)
        {
            return;
        }

        if (Time.time < nextAttackAllowedTime)
        {
            return;
        }

        float distance = Vector2.Distance(transform.position, playerTarget.position);
        if (distance <= engageRange)
        {
            TryStartAttack();
        }
    }

    public void SetInternalAiEnabled(bool enabled)
    {
        useInternalAi = enabled;
    }

    public void SetContactDamageActive(bool active)
    {
        contactDamageActiveByAi = active;

        if (active == false && hideAttackPointOutsideAttack && !isAttacking)
        {
            SetAttackPointVisible(false);
        }
    }

    public void SetFacingDirection(int direction)
    {
        facingDirection = direction >= 0 ? 1 : -1;

        if (!mirrorHitPointsWithFacing)
        {
            return;
        }

        CacheHitPointOffsets();
        ApplyHitPointMirror();
    }

    public bool TryStartAttack()
    {
        if (isDead || isAttacking)
        {
            return false;
        }

        if (Time.time < nextAttackAllowedTime)
        {
            return false;
        }

        AttackMode mode = ChooseAttackMode();
        attackRoutine = StartCoroutine(AttackRoutine(++attackToken, mode));
        return true;
    }

    private IEnumerator AttackRoutine(int token, AttackMode mode)
    {
        isAttacking = true;
        currentAttackStartTime = Time.time;
        nextAttackAllowedTime = float.MaxValue;
        SetAttackPointVisible(false);

        string triggerName = GetTriggerName(mode);
        bool triggerExists = GetTriggerExists(mode);

        if (!string.IsNullOrEmpty(triggerName))
        {
            switch (mode)
            {
                case AttackMode.Attack2:
                    TrySetTrigger(triggerName, ref loggedMissingAttack2Trigger, triggerExists);
                    break;
                case AttackMode.GroundSlam:
                    TrySetTrigger(triggerName, ref loggedMissingGroundSlamTrigger, triggerExists);
                    break;
                default:
                    TrySetTrigger(triggerName, ref loggedMissingAttackTrigger, triggerExists);
                    break;
            }
        }

        float preHitRaise = Mathf.Min(Mathf.Max(0f, raisePoseTime), Mathf.Max(0f, attackWindup));
        if (preHitRaise > 0f)
        {
            yield return new WaitForSeconds(preHitRaise);
        }

        if (slowSlashChance > 0f && Random.value <= slowSlashChance)
        {
            float minPause = Mathf.Min(slowSlashPauseRange.x, slowSlashPauseRange.y);
            float maxPause = Mathf.Max(slowSlashPauseRange.x, slowSlashPauseRange.y);
            float randomPause = Random.Range(Mathf.Max(0f, minPause), Mathf.Max(0f, maxPause));
            if (randomPause > 0f)
            {
                yield return new WaitForSeconds(randomPause);
            }
        }

        float baseHitDelay = Mathf.Max(0f, attackHitFrame) / Mathf.Max(1f, attackClipFps);
        float configuredWindup = Mathf.Max(0f, attackWindup);
        float remainWindup = Mathf.Max(configuredWindup, baseHitDelay) - preHitRaise;

        if (mode == AttackMode.GroundSlam)
        {
            if (remainWindup > 0f)
            {
                yield return new WaitForSeconds(Mathf.Min(remainWindup, 0.1f));
            }

            yield return PerformGroundSlamJump();
            nextGroundSlamAllowedTime = Time.time + Mathf.Max(0.25f, groundSlamCooldown);
        }
        else if (remainWindup > 0f)
        {
            yield return new WaitForSeconds(remainWindup);
        }

        if (token == attackToken && !isDead)
        {
            SetAttackPointVisible(true);
            DealDamageToPlayer();

            float activeTime = Mathf.Max(0f, hitboxActiveDuration);
            if (activeTime > 0f)
            {
                yield return new WaitForSeconds(activeTime);
            }

            if (hideAttackPointOutsideAttack)
            {
                SetAttackPointVisible(false);
            }
        }

        float recovery = EffectiveAttackRecovery;
        if (recovery > 0f)
        {
            yield return new WaitForSeconds(recovery);
        }

        if (token == attackToken)
        {
            attackRoutine = null;
            isAttacking = false;
            nextAttackAllowedTime = Time.time + Mathf.Max(0f, EffectiveAttackCooldown);

            if (hideAttackPointOutsideAttack)
            {
                SetAttackPointVisible(false);
            }
        }
    }

    private AttackMode ChooseAttackMode()
    {
        float distance = playerTarget != null
            ? Vector2.Distance(transform.position, playerTarget.position)
            : 0f;

        if (hasGroundSlamTriggerParam && distance >= Mathf.Max(0f, groundSlamMinDistance) && Time.time >= nextGroundSlamAllowedTime)
        {
            return AttackMode.GroundSlam;
        }

        if (hasAttack2TriggerParam && distance >= Mathf.Max(0f, attack2MinDistance))
        {
            return AttackMode.Attack2;
        }

        return AttackMode.Attack;
    }

    private string GetTriggerName(AttackMode mode)
    {
        switch (mode)
        {
            case AttackMode.Attack2:
                return attack2TriggerName;
            case AttackMode.GroundSlam:
                return groundSlamTriggerName;
            default:
                return attackTriggerName;
        }
    }

    private bool GetTriggerExists(AttackMode mode)
    {
        switch (mode)
        {
            case AttackMode.Attack2:
                return hasAttack2TriggerParam;
            case AttackMode.GroundSlam:
                return hasGroundSlamTriggerParam;
            default:
                return hasAttackTriggerParam;
        }
    }

    private IEnumerator PerformGroundSlamJump()
    {
        if (rb == null || playerTarget == null)
        {
            yield break;
        }

        float duration = Mathf.Max(0.08f, groundSlamJumpDuration);
        float height = Mathf.Max(0f, groundSlamJumpHeight);
        Vector2 start = rb.position;
        Vector2 target = new Vector2(playerTarget.position.x, playerTarget.position.y);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float t = Mathf.Clamp01(elapsed / duration);
            float x = Mathf.Lerp(start.x, target.x, t);
            float y = Mathf.Lerp(start.y, target.y, t) + 4f * height * t * (1f - t);
            rb.MovePosition(new Vector2(x, y));
            elapsed += Time.deltaTime;
            yield return null;
        }

        rb.MovePosition(target);
    }

    /// <summary>
    /// 可被动画事件调用，也可由协程兜底调用。
    /// </summary>
    public void DealDamageToPlayer()
    {
        if (attackPoint == null)
        {
            Debug.LogWarning("[EnemyCombat] attackPoint 未配置，无法攻击玩家。", this);
            return;
        }

        int mask = playerLayer.value == 0 ? Physics2D.AllLayers : playerLayer.value;
        Collider2D[] cols = Physics2D.OverlapCircleAll(attackPoint.position, EffectiveAttackRange, mask);
        for (int i = 0; i < cols.Length; i++)
        {
            PlayerCombat player = cols[i].GetComponentInParent<PlayerCombat>();
            if (player == null)
            {
                continue;
            }

            AttackData data = new AttackData(
                attackDamage,
                this,
                currentAttackStartTime,
                attackPoint.position
            );

            player.ReceiveAttack(data);
        }
    }

    public void TakeDamage(int damage, PlayerCombat source)
    {
        if (isDead)
        {
            return;
        }

        int finalDamage = Mathf.Max(0, damage);
        if (finalDamage <= 0)
        {
            return;
        }

        currentHealth = Mathf.Max(0, currentHealth - finalDamage);

        PlayHit();

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    /// <summary>
    /// 被完美弹反时调用：返还伤害并打断当前攻击。
    /// </summary>
    public void OnParried(int reflectedDamage, Vector2 parrySourcePosition)
    {
        if (isDead)
        {
            return;
        }

        InterruptAttack();
        isAttacking = false;
        TakeDamage(reflectedDamage, null);
        nextAttackAllowedTime = Time.time + Mathf.Max(0.35f, EffectiveAttackCooldown * 0.6f);
    }

    public void PlayHit()
    {
        if (!string.IsNullOrEmpty(hitTriggerName))
        {
            TrySetTrigger(hitTriggerName, ref loggedMissingHitTrigger, hasHitTriggerParam);
        }
    }

    private void CacheAnimatorParams()
    {
        animatorParamsCached = true;
        hasAttackTriggerParam = false;
        hasAttack2TriggerParam = false;
        hasGroundSlamTriggerParam = false;
        hasHitTriggerParam = false;

        if (animator == null)
        {
            return;
        }

        AnimatorControllerParameter[] parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].type != AnimatorControllerParameterType.Trigger)
            {
                continue;
            }

            if (parameters[i].name == attackTriggerName)
            {
                hasAttackTriggerParam = true;
            }

            if (parameters[i].name == attack2TriggerName)
            {
                hasAttack2TriggerParam = true;
            }

            if (parameters[i].name == groundSlamTriggerName)
            {
                hasGroundSlamTriggerParam = true;
            }

            if (parameters[i].name == hitTriggerName)
            {
                hasHitTriggerParam = true;
            }
        }
    }

    private void TrySetTrigger(string triggerName, ref bool loggedMissing, bool cachedExists)
    {
        if (animator == null || string.IsNullOrEmpty(triggerName))
        {
            return;
        }

        if (!animatorParamsCached)
        {
            CacheAnimatorParams();
        }

        if (!cachedExists)
        {
            if (!loggedMissing)
            {
                loggedMissing = true;
                Debug.LogWarning($"[EnemyCombat] Animator parameter '{triggerName}' not found on {name}.", this);
            }
            return;
        }

        animator.SetTrigger(triggerName);
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

    private void Die()
    {
        InterruptAttack();
        isDead = true;
        Destroy(gameObject);
    }

    private void InterruptAttack()
    {
        attackToken++;

        if (attackRoutine != null)
        {
            StopCoroutine(attackRoutine);
            attackRoutine = null;
        }

        isAttacking = false;

        if (hideAttackPointOutsideAttack)
        {
            SetAttackPointVisible(false);
        }
    }

    private void TickContactDamage()
    {
        if (!enableContactDamage || isDead || isAttacking || !contactDamageActiveByAi)
        {
            return;
        }

        if (Time.time < nextContactDamageAllowedTime)
        {
            return;
        }

        Vector2 point = contactPoint != null ? contactPoint.position : transform.position;
        int mask = playerLayer.value == 0 ? Physics2D.AllLayers : playerLayer.value;
        Collider2D[] cols = Physics2D.OverlapCircleAll(point, EffectiveContactDamageRange, mask);
        for (int i = 0; i < cols.Length; i++)
        {
            PlayerCombat player = cols[i].GetComponentInParent<PlayerCombat>();
            if (player == null)
            {
                continue;
            }

            if (requirePhysicalTouchForContactDamage && !IsTouchingPlayer(player))
            {
                continue;
            }

            AttackData data = new AttackData(
                Mathf.Max(0, contactDamage),
                this,
                Time.time - 99f,
                point
            );

            player.ReceiveAttack(data);
            nextContactDamageAllowedTime = Time.time + Mathf.Max(0.1f, contactDamageCooldown);
            return;
        }
    }

    private bool IsTouchingPlayer(PlayerCombat player)
    {
        if (player == null || bodyCollider == null)
        {
            return false;
        }

        Collider2D[] playerColliders = player.GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < playerColliders.Length; i++)
        {
            Collider2D col = playerColliders[i];
            if (col != null && bodyCollider.IsTouching(col))
            {
                return true;
            }
        }

        return false;
    }

    public void AnimEvent_EnableAttackPoint()
    {
        SetAttackPointVisible(true);
    }

    public void AnimEvent_DisableAttackPoint()
    {
        if (hideAttackPointOutsideAttack)
        {
            SetAttackPointVisible(false);
        }
    }

    public void AnimEvent_DealDamageToPlayer()
    {
        DealDamageToPlayer();
    }

    private void CacheHitPointOffsets()
    {
        if (cachedHitPointLocalOffsets)
        {
            return;
        }

        if (attackPoint != null)
        {
            attackPointBaseLocalPosition = attackPoint.localPosition;
        }

        if (contactPoint != null)
        {
            contactPointBaseLocalPosition = contactPoint.localPosition;
        }

        cachedHitPointLocalOffsets = true;
    }

    private void ApplyHitPointMirror()
    {
        if (attackPoint != null)
        {
            Vector3 p = attackPointBaseLocalPosition;
            p.x = Mathf.Abs(p.x) * facingDirection;
            attackPoint.localPosition = p;
        }

        if (contactPoint != null)
        {
            Vector3 p = contactPointBaseLocalPosition;
            p.x = Mathf.Abs(p.x) * facingDirection;
            contactPoint.localPosition = p;
        }
    }

    private void SetAttackPointVisible(bool visible)
    {
        if (attackPoint == null)
        {
            return;
        }

        if (attackPointVisible == visible)
        {
            return;
        }

        attackPointVisible = visible;
        attackPoint.gameObject.SetActive(visible);
    }

    private void OnDrawGizmosSelected()
    {
        if (attackPoint == null)
        {
            return;
        }

        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(attackPoint.position, EffectiveAttackRange);

        if (contactPoint != null)
        {
            Gizmos.color = new Color(1f, 0.55f, 0f, 1f);
            Gizmos.DrawWireSphere(contactPoint.position, EffectiveContactDamageRange);
        }
        else
        {
            Gizmos.color = new Color(1f, 0.55f, 0f, 1f);
            Gizmos.DrawWireSphere(transform.position, EffectiveContactDamageRange);
        }

        if (playerTarget != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, engageRange);
        }
    }
}
