using System.Collections;
using UnityEngine;

public class EnemyCombat : MonoBehaviour
{
    [Header("Health")]
    [SerializeField] private int maxHealth = 60;

    [Header("Attack")]
    [SerializeField] private int attackDamage = 15;
    [SerializeField] private float attackCooldown = 1.2f;
    [SerializeField] private float attackWindup = 0.2f;
    [SerializeField] private Transform attackPoint;
    [SerializeField] private float attackRange = 0.8f;
    [SerializeField] private LayerMask playerLayer;

    [Header("Animator Params")]
    [SerializeField] private string attackTriggerName = "Attack";
    [SerializeField] private string hitTriggerName = "isHit";

    [Header("AI")]
    [SerializeField] private bool useInternalAi = true;
    [SerializeField] private Transform playerTarget;
    [SerializeField] private float engageRange = 2f;

    [Header("Refs")]
    [SerializeField] private Animator animator;

    private int currentHealth;
    private float lastAttackTime = -999f;
    private float currentAttackStartTime = -999f;
    private bool isDead;
    private bool isAttacking;
    private bool animatorParamsCached;
    private bool hasAttackTriggerParam;
    private bool hasHitTriggerParam;
    private bool loggedMissingAttackTrigger;
    private bool loggedMissingHitTrigger;

    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;
    public bool IsDead => isDead;
    public bool IsAttacking => isAttacking;

    private void Awake()
    {
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
        if (!useInternalAi)
        {
            return;
        }

        if (isDead || isAttacking || playerTarget == null)
        {
            return;
        }

        if (Time.time - lastAttackTime < attackCooldown)
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

    public bool TryStartAttack()
    {
        if (isDead || isAttacking)
        {
            return false;
        }

        if (Time.time - lastAttackTime < attackCooldown)
        {
            return false;
        }

        StartCoroutine(AttackRoutine());
        return true;
    }

    private IEnumerator AttackRoutine()
    {
        isAttacking = true;
        lastAttackTime = Time.time;
        currentAttackStartTime = Time.time;

        if (!string.IsNullOrEmpty(attackTriggerName))
        {
            TrySetTrigger(attackTriggerName, ref loggedMissingAttackTrigger, hasAttackTriggerParam);
        }

        yield return new WaitForSeconds(attackWindup);
        DealDamageToPlayer();

        isAttacking = false;
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
        Collider2D[] cols = Physics2D.OverlapCircleAll(attackPoint.position, attackRange, mask);
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

        isAttacking = false;
        TakeDamage(reflectedDamage, null);
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
        isDead = true;
        Destroy(gameObject);
    }

    private void OnDrawGizmosSelected()
    {
        if (attackPoint == null)
        {
            return;
        }

        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(attackPoint.position, attackRange);

        if (playerTarget != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, engageRange);
        }
    }
}
