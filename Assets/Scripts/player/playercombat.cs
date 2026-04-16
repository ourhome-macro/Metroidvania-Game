using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Animator), typeof(Rigidbody2D))]
public class PlayerCombat : MonoBehaviour
{
	[Header("Attack")]
	[SerializeField] private Transform attackPoint;
	[SerializeField] private float attackRadius = 0.72f;
	[SerializeField] private float minimumEffectiveAttackRadius = 0.72f;
	[SerializeField] private LayerMask enemyLayer;
	[SerializeField] private int attackDamage = 20;
	[SerializeField] private float attackCooldown = 0.35f;
	[SerializeField] private float attackLockDuration = 0.35f;

	[Header("Health")]
	[SerializeField] private int maxHealth = 100;
	[SerializeField] private float hitStunDuration = 0.2f;
	[SerializeField] private float postHitInvincibleTime = 0.15f;

	[Header("Defend / Parry")]
	[SerializeField] private float defendDamageMultiplier = 0.3f; // 减伤70%
	[SerializeField] private float perfectParryWindow = 0.65f;
	[SerializeField] private float defendMoveMultiplier = 0.35f;

	[Header("Skip (Dodge)")]
	[SerializeField] private float skipInvincibleTime = 0.6f;
	[SerializeField] private float skipDuration = 0.18f;
	[SerializeField] private float skipSpeed = 12f;
	[SerializeField] private float skipCooldown = 1f;

	[Header("Refs")]
	[SerializeField] private PlayerController2D playerController;

	[Header("Debug")]
	[SerializeField] private bool logDefenseResult = true;

	private Animator animator;
	private Rigidbody2D rb;

	private int currentHealth;
	private float lastAttackTime = -999f;
	private float lastSkipTime = -999f;
	private float defendStartTime = -999f;
	private float invincibleUntil = -999f;

	private bool isDead;
	private bool isAttacking;
	private bool isDefending;
	private bool isSkipping;
	private bool isHitStunned;

	private float EffectiveAttackRadius => Mathf.Max(attackRadius, minimumEffectiveAttackRadius);

	public int CurrentHealth => currentHealth;
	public int MaxHealth => maxHealth;
	public bool IsDead => isDead;
	public bool IsDefending => isDefending;
	public bool IsSkipping => isSkipping;
	public bool IsMovementLocked => isDead || isHitStunned || isAttacking || isSkipping;
	public float MovementSpeedMultiplier => isDefending ? defendMoveMultiplier : 1f;

	private void Awake()
	{
		animator = GetComponent<Animator>();
		rb = GetComponent<Rigidbody2D>();

		if (playerController == null)
		{
			playerController = GetComponent<PlayerController2D>();
		}

		currentHealth = Mathf.Max(1, maxHealth);
	}

	private void Update()
	{
		if (isDead)
		{
			return;
		}

		UpdateDefendInput();
		UpdateAttackInput();
		UpdateSkipInput();
	}

	private void UpdateDefendInput()
	{
		if (isAttacking || isSkipping || isHitStunned)
		{
			SetDefending(false);
			return;
		}

		bool defendHeld = Input.GetKey(KeyCode.K);
		SetDefending(defendHeld);
	}

	private void UpdateAttackInput()
	{
		if (!Input.GetKeyDown(KeyCode.J))
		{
			return;
		}

		if (isAttacking || isSkipping || isHitStunned || isDefending)
		{
			return;
		}

		if (Time.time - lastAttackTime < attackCooldown)
		{
			return;
		}

		lastAttackTime = Time.time;
		isAttacking = true;
		animator.SetTrigger("Attack");
		StartCoroutine(AttackLockTimer());
	}

	private IEnumerator AttackLockTimer()
	{
		yield return new WaitForSeconds(attackLockDuration);
		isAttacking = false;
	}

	private void UpdateSkipInput()
	{
		if (!Input.GetKeyDown(KeyCode.LeftShift))
		{
			return;
		}

		if (isAttacking || isSkipping || isHitStunned || isDead)
		{
			return;
		}

		if (Time.time - lastSkipTime < skipCooldown)
		{
			return;
		}

		StartCoroutine(DoSkip());
	}

	private IEnumerator DoSkip()
	{
		lastSkipTime = Time.time;
		isSkipping = true;
		GameEvents.DashStart();
		SetDefending(false);
		invincibleUntil = Time.time + skipInvincibleTime;

		animator.SetTrigger("isSkipping");

		float elapsed = 0f;
		float direction = playerController != null ? playerController.FacingDirection : Mathf.Sign(transform.localScale.x);
		if (Mathf.Abs(direction) < 0.01f)
		{
			direction = 1f;
		}

		while (elapsed < skipDuration)
		{
			rb.velocity = new Vector2(direction * skipSpeed, rb.velocity.y);
			elapsed += Time.deltaTime;
			yield return null;
		}

		isSkipping = false;
		GameEvents.DashEnd();
	}

	private void OnDisable()
	{
		if (isSkipping)
		{
			isSkipping = false;
			GameEvents.DashEnd();
		}
	}

	/// <summary>
	/// 动画事件调用：在 atk_Clip 的有效帧打点。
	/// </summary>
	public void DealAttackDamage()
	{
		if (attackPoint == null)
		{
			Debug.LogWarning("[PlayerCombat] attackPoint 未配置，无法造成攻击伤害。", this);
			return;
		}

		int mask = enemyLayer.value == 0 ? Physics2D.AllLayers : enemyLayer.value;
		Collider2D[] hitResults = Physics2D.OverlapCircleAll(attackPoint.position, EffectiveAttackRadius, mask);
		for (int i = 0; i < hitResults.Length; i++)
		{
			EnemyCombat enemy = hitResults[i].GetComponentInParent<EnemyCombat>();
			if (enemy != null)
			{
				enemy.TakeDamage(attackDamage, this);
			}
		}
	}

	/// <summary>
	/// 动画事件可选调用：攻击动画结束时解锁（比纯定时更稳）。
	/// </summary>
	public void OnAttackAnimationFinished()
	{
		isAttacking = false;
	}

	/// <summary>
	/// 敌人攻击玩家时调用。核心逻辑：无敌 -> 完美弹反 -> 普通防御 -> 普通受击。
	/// </summary>
	public void ReceiveAttack(AttackData attackData)
	{
		if (isDead)
		{
			return;
		}

		if (Time.time <= invincibleUntil || isSkipping)
		{
			return;
		}

		int incomingDamage = Mathf.Max(0, attackData.Damage);
		if (incomingDamage <= 0)
		{
			return;
		}

		if (isDefending)
		{
			if (IsPerfectParry(attackData.AttackStartTime))
			{
				PerfectParry(attackData);
				return;
			}

			int reduced = Mathf.RoundToInt(incomingDamage * defendDamageMultiplier);
			if (logDefenseResult)
			{
				Debug.Log($"[PlayerCombat] 防御：完美格挡失效，但抵挡了伤害。原伤害={incomingDamage}，减免后={reduced}。", this);
			}
			ApplyDamage(reduced, false);
			return;
		}

		ApplyDamage(incomingDamage, true);
	}

	/// <summary>
	/// 兼容旧调用：例如已有敌人代码直接调 TakeDamage(int)。
	/// </summary>
	public void TakeDamage(int damage)
	{
		AttackData fallback = new AttackData(damage, null, Time.time - 99f, transform.position);
		ReceiveAttack(fallback);
	}

	private bool IsPerfectParry(float enemyAttackStartTime)
	{
		if (!isDefending || defendStartTime < 0f)
		{
			return false;
		}

		float defendHeldTime = Time.time - defendStartTime;
		if (defendHeldTime < 0f || defendHeldTime > perfectParryWindow)
		{
			return false;
		}

		if (enemyAttackStartTime < 0f)
		{
			return true;
		}

		return Time.time >= enemyAttackStartTime;
	}

	private void PerfectParry(AttackData attackData)
	{
		// 本次无伤，并将伤害返还给攻击者。
		GameEvents.PerfectParry();
		if (logDefenseResult)
		{
			Debug.Log($"[PlayerCombat] 防御：完美格挡成功。反弹伤害={attackData.Damage}。", this);
		}
		if (attackData.Attacker != null)
		{
			attackData.Attacker.OnParried(attackData.Damage, transform.position);
		}
	}

	private void ApplyDamage(int damage, bool triggerHitStun)
	{
		if (damage <= 0)
		{
			return;
		}

		currentHealth = Mathf.Max(0, currentHealth - damage);

		if (currentHealth <= 0)
		{
			Die();
			return;
		}

		if (triggerHitStun)
		{
			animator.SetTrigger("isHit");
			StartCoroutine(HitStunRoutine());
		}

		invincibleUntil = Mathf.Max(invincibleUntil, Time.time + postHitInvincibleTime);
	}

	private IEnumerator HitStunRoutine()
	{
		isHitStunned = true;
		SetDefending(false);
		yield return new WaitForSeconds(hitStunDuration);
		isHitStunned = false;
	}

	private void SetDefending(bool value)
	{
		if (isDefending == value)
		{
			return;
		}

		isDefending = value;
		animator.SetBool("isDefending", isDefending);

		if (isDefending)
		{
			defendStartTime = Time.time;
		}
		else
		{
			defendStartTime = -999f;
		}
	}

	private void Die()
	{
		isDead = true;
		isAttacking = false;
		isDefending = false;
		isSkipping = false;
		isHitStunned = false;

		animator.SetBool("isDefending", false);
		rb.velocity = Vector2.zero;
		enabled = false;
	}

	private void OnDrawGizmosSelected()
	{
		if (attackPoint == null)
		{
			return;
		}

		Gizmos.color = Color.red;
		Gizmos.DrawWireSphere(attackPoint.position, EffectiveAttackRadius);
	}
}
