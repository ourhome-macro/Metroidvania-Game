using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Animator))]
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
    [SerializeField] private Transform playerTarget;
    [SerializeField] private float engageRange = 2f;

    private Animator animator;
    private int currentHealth;
    private float lastAttackTime = -999f;
    private float currentAttackStartTime = -999f;
    private bool isDead;
    private bool isAttacking;

    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;

    private void Awake()
    {
        animator = GetComponent<Animator>();
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
            StartCoroutine(AttackRoutine());
        }
    }

    private IEnumerator AttackRoutine()
    {
        isAttacking = true;
        lastAttackTime = Time.time;
        currentAttackStartTime = Time.time;

        if (!string.IsNullOrEmpty(attackTriggerName))
        {
            animator.SetTrigger(attackTriggerName);
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

        Collider2D[] cols = Physics2D.OverlapCircleAll(attackPoint.position, attackRange, playerLayer);
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
            animator.SetTrigger(hitTriggerName);
        }
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
