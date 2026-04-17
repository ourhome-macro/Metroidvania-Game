using System.Collections.Generic;
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Animator), typeof(Rigidbody2D))]
public class PlayerCombat : MonoBehaviour
{
	[Header("Attack")]
	[SerializeField] private Transform attackPoint;
	[SerializeField] private Collider2D attackRange;
	[SerializeField] private string attackRangeObjectName = "Range";
	[SerializeField] private float attackRadius = 0.72f;
	[SerializeField] private float minimumEffectiveAttackRadius = 0.72f;
	[SerializeField] private LayerMask enemyLayer;
	[SerializeField] private int attackDamage = 20;
	[SerializeField] private float attackCooldown = 0.35f;
	[SerializeField] private float attackLockDuration = 0.35f;
	[SerializeField] private float attackHitDelay = 0.12f;
	[SerializeField] private string attackStateName = "Attack";
	[SerializeField] private int attackHitboxStartFrame = 2;
	[SerializeField] private int attackHitboxEndFrame = -1;

	[Header("Health")]
	[SerializeField] private int maxHealth = 100;
	[SerializeField] private float hitStunDuration = 0.2f;
	[SerializeField] private float postHitInvincibleTime = 0.15f;

	[Header("Defend / Parry")]
	[SerializeField] private float defendDamageMultiplier = 0.3f; // 閸戝繋婵€70%
	[SerializeField] private float perfectParryWindow = 0.18f;
	[SerializeField] private float perfectParryInvincibleTime = 0.08f;
	[SerializeField] private float defendMoveMultiplier = 0.35f;

	[Header("Skip (Dodge)")]
	[SerializeField] private float skipInvincibleTime = 0.6f;
	[SerializeField] private float skipDuration = 0.18f;
	[SerializeField] private float skipSpeed = 12f;
	[SerializeField] private float skipCooldown = 1f;

	[Header("Refs")]
	[SerializeField] private PlayerController2D playerController;
	[SerializeField] private PlayerHealth playerHealth;

	[Header("Debug")]
	[SerializeField] private bool logDefenseResult = true;

	private Animator animator;
	private Rigidbody2D rb;
	private Collider2D bodyCollider;

	private int currentHealth;
	private float lastAttackTime = -999f;
	private float lastSkipTime = -999f;
	private float defendStartTime = -999f;
	private float invincibleUntil = -999f;
	private Coroutine attackRoutine;
	private Coroutine hitStunRoutine;

	private bool isDead;
	private bool isAttacking;
	private bool isDefending;
	private bool isSkipping;
	private bool isHitStunned;
	private int attackStateHash;
	private readonly List<Collider2D> overlapResults = new List<Collider2D>(16);
	private readonly HashSet<EnemyCombat> damagedEnemiesThisAttack = new HashSet<EnemyCombat>();
	private readonly List<AnimatorClipInfo> clipInfoBuffer = new List<AnimatorClipInfo>(2);

	private float EffectiveAttackRadius => Mathf.Max(attackRadius, minimumEffectiveAttackRadius);

	public int CurrentHealth => playerHealth != null ? playerHealth.CurrentHealth : currentHealth;
	public int MaxHealth => playerHealth != null ? playerHealth.MaxHealth : maxHealth;
	public bool IsDead => isDead;
	public bool IsAttacking => isAttacking;
	public bool IsDefending => isDefending;
	public bool IsSkipping => isSkipping;
	public bool IsHitStunned => isHitStunned;
	public bool IsMovementLocked => isDead || isHitStunned || isAttacking || isSkipping;
	public float MovementSpeedMultiplier => isDefending ? defendMoveMultiplier : 1f;

	private void Awake()
	{
		animator = GetComponent<Animator>();
		rb = GetComponent<Rigidbody2D>();
		bodyCollider = GetComponent<Collider2D>();

		if (playerController == null)
		{
			playerController = GetComponent<PlayerController2D>();
		}

		if (playerHealth == null)
		{
			playerHealth = GetComponent<PlayerHealth>();
		}

		ResolveAttackPoint();
		ResolveAttackRange();
		SetAttackRangeEnabled(false);
		attackStateHash = Animator.StringToHash(string.IsNullOrWhiteSpace(attackStateName) ? "Attack" : attackStateName);
		if (playerHealth != null)
		{
			playerHealth.Initialize(Mathf.Max(1, maxHealth), true);
			currentHealth = playerHealth.CurrentHealth;
		}
		else
		{
			currentHealth = Mathf.Max(1, maxHealth);
			GameEvents.HpChanged(currentHealth, maxHealth);
		}
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
		damagedEnemiesThisAttack.Clear();
		animator.SetTrigger("Attack");
		if (attackRoutine != null)
		{
			StopCoroutine(attackRoutine);
		}
		attackRoutine = StartCoroutine(AttackLockTimer());
	}

	private IEnumerator AttackLockTimer()
	{
		ResolveAttackRange();
		SetAttackRangeEnabled(false);
		bool enteredAttackState = false;
		bool enteredHitWindow = false;
		float stateEnterDeadline = Time.time + Mathf.Max(0.25f, attackLockDuration + 0.8f);
		while (!isDead)
		{
			if (!TryGetAttackState(out AnimatorStateInfo stateInfo, out AnimationClip clip))
			{
				if (enteredAttackState || Time.time >= stateEnterDeadline)
				{
					break;
				}

				yield return null;
				continue;
			}

			enteredAttackState = true;
			int totalFrames = GetClipFrameCount(clip);
			int currentFrame = GetCurrentFrame(stateInfo, totalFrames);
			int startFrame = Mathf.Clamp(Mathf.Max(1, attackHitboxStartFrame), 1, totalFrames);
			int endFrame = attackHitboxEndFrame <= 0
				? totalFrames
				: Mathf.Clamp(attackHitboxEndFrame, startFrame, totalFrames);
			bool hitboxActive = currentFrame >= startFrame && currentFrame <= endFrame;
			SetAttackRangeEnabled(hitboxActive);
			if (hitboxActive)
			{
				enteredHitWindow = true;
				DealAttackDamage();
			}

			if (stateInfo.normalizedTime >= 1f && !animator.IsInTransition(0))
			{
				break;
			}

			yield return null;
		}

		if (!enteredHitWindow && !isDead)
		{
			bool hadRange = attackRange != null;
			if (hadRange)
			{
				SetAttackRangeEnabled(true);
			}

			DealAttackDamage();

			if (hadRange)
			{
				SetAttackRangeEnabled(false);
			}
		}

		SetAttackRangeEnabled(false);
		attackRoutine = null;
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
		if (attackRoutine != null)
		{
			StopCoroutine(attackRoutine);
			attackRoutine = null;
		}

		if (hitStunRoutine != null)
		{
			StopCoroutine(hitStunRoutine);
			hitStunRoutine = null;
			isHitStunned = false;
		}

		if (isSkipping)
		{
			isSkipping = false;
			GameEvents.DashEnd();
		}

		SetAttackRangeEnabled(false);
		damagedEnemiesThisAttack.Clear();
	}

	/// <summary>
	/// 閸斻劎鏁炬禍瀣╂鐠嬪啰鏁ら敍姘躬 atk_Clip 閻ㄥ嫭婀侀弫鍫濇姎閹垫挾鍋ｉ妴?
	/// </summary>
	public void DealAttackDamage()
	{
		ResolveAttackPoint();
		ResolveAttackRange();
		if (attackPoint == null)
		{
			Debug.LogWarning("[PlayerCombat] attackPoint is not assigned, cannot deal attack damage.", this);
			return;
		}

		int mask = enemyLayer.value == 0 ? Physics2D.AllLayers : enemyLayer.value;
		if (attackRange != null)
		{
			if (!attackRange.enabled || !attackRange.gameObject.activeInHierarchy)
			{
				return;
			}

			ContactFilter2D filter = new ContactFilter2D();
			filter.useLayerMask = true;
			filter.layerMask = mask;
			filter.useTriggers = true;
			overlapResults.Clear();
			int count = attackRange.OverlapCollider(filter, overlapResults);
			for (int i = 0; i < count; i++)
			{
				EnemyCombat enemy = overlapResults[i].GetComponentInParent<EnemyCombat>();
				if (enemy != null && damagedEnemiesThisAttack.Add(enemy))
				{
					enemy.TakeDamage(attackDamage, this);
				}
			}
			overlapResults.Clear();
			return;
		}

		Collider2D[] hitResults = Physics2D.OverlapCircleAll(attackPoint.position, EffectiveAttackRadius, mask);
		for (int i = 0; i < hitResults.Length; i++)
		{
			EnemyCombat enemy = hitResults[i].GetComponentInParent<EnemyCombat>();
			if (enemy != null && damagedEnemiesThisAttack.Add(enemy))
			{
				enemy.TakeDamage(attackDamage, this);
			}
		}
	}

	/// <summary>
	/// 閸斻劎鏁炬禍瀣╂閸欘垶鈧鐨熼悽顭掔窗閺€璇插毊閸斻劎鏁剧紒鎾存将閺冩儼袙闁夸緤绱欏В鏃傚嚱鐎规碍妞傞弴瀵盖旈敍澶堚偓?
	/// </summary>
	public void OnAttackAnimationFinished()
	{
		if (attackRoutine != null)
		{
			StopCoroutine(attackRoutine);
			attackRoutine = null;
		}
		SetAttackRangeEnabled(false);
		isAttacking = false;
	}

	/// <summary>
	/// 閺佸奔姹夐弨璇插毊閻溾晛顔嶉弮鎯扮殶閻劊鈧倹鐗宠箛鍐偓鏄忕帆閿涙碍妫ら弫?-> 鐎瑰瞼绶ㄥ鐟板冀 -> 閺咁噣鈧岸妲诲?-> 閺咁噣鈧艾褰堥崙姹団偓?
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
				Debug.Log($"[PlayerCombat] Blocked incoming damage. Original={incomingDamage}, Reduced={reduced}.", this);
			}
			ApplyDamage(reduced, false, attackData);
			return;
		}

		ApplyDamage(incomingDamage, true, attackData);
	}

	/// <summary>
	/// 閸忕厧顔愰弮褑鐨熼悽顭掔窗娓氬顩у鍙夋箒閺佸奔姹夋禒锝囩垳閻╁瓨甯寸拫?TakeDamage(int)閵?
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
		GameEvents.PerfectParry();
		invincibleUntil = Mathf.Max(invincibleUntil, Time.time + Mathf.Max(0f, perfectParryInvincibleTime));
		// This parry takes no damage and reflects it back to the attacker.
		if (logDefenseResult)
		{
				Debug.Log($"[PlayerCombat] Perfect parry succeeded. Reflected={attackData.Damage}.", this);
		}
		if (attackData.Attacker != null)
		{
			attackData.Attacker.OnParried(attackData.Damage, transform.position);
		}
	}

	private void ApplyDamage(int damage, bool triggerHitStun, AttackData attackData)
	{
		if (damage <= 0)
		{
			return;
		}

		bool damageApplied = true;
		bool becameDead = false;
		if (playerHealth != null)
		{
			PlayerHealth.DamageRequest request = new PlayerHealth.DamageRequest(damage, attackData, triggerHitStun);
			damageApplied = playerHealth.TryTakeDamage(request, out becameDead);
			currentHealth = playerHealth.CurrentHealth;
		}
		else
		{
			currentHealth = Mathf.Max(0, currentHealth - damage);
			GameEvents.HpChanged(currentHealth, maxHealth);
			becameDead = currentHealth <= 0;
		}

		if (!damageApplied)
		{
			return;
		}

		if (becameDead || currentHealth <= 0)
		{
			Die();
			return;
		}

		if (triggerHitStun)
		{
			animator.SetTrigger("isHit");
			if (hitStunRoutine != null)
			{
				StopCoroutine(hitStunRoutine);
			}

			hitStunRoutine = StartCoroutine(HitStunRoutine());
		}

		invincibleUntil = Mathf.Max(invincibleUntil, Time.time + postHitInvincibleTime);
	}

	private IEnumerator HitStunRoutine()
	{
		isHitStunned = true;
		SetDefending(false);
		yield return new WaitForSeconds(hitStunDuration);
		isHitStunned = false;
		hitStunRoutine = null;
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
		if (isDead)
		{
			return;
		}

		isDead = true;
		isAttacking = false;
		isDefending = false;
		isSkipping = false;
		isHitStunned = false;
		SetAttackRangeEnabled(false);

		animator.SetBool("isDefending", false);
		rb.velocity = Vector2.zero;
		GameEvents.PlayerDeath();
		enabled = false;
	}

	private void ResolveAttackPoint()
	{
		if (attackPoint != null)
		{
			return;
		}

		Transform[] transforms = GetComponentsInChildren<Transform>(true);
		for (int i = 0; i < transforms.Length; i++)
		{
			Transform candidate = transforms[i];
			if (candidate == null || candidate == transform)
			{
				continue;
			}

			if (candidate.name.Equals("AttackPoint", System.StringComparison.OrdinalIgnoreCase))
			{
				attackPoint = candidate;
				return;
			}
		}

		GameObject point = new GameObject("AttackPoint");
		attackPoint = point.transform;
		attackPoint.SetParent(transform, false);

		float forwardOffset = 0.68f;
		if (bodyCollider != null)
		{
			float lossyX = Mathf.Max(0.01f, Mathf.Abs(transform.lossyScale.x));
			forwardOffset = Mathf.Max(0.45f, bodyCollider.bounds.extents.x / lossyX);
		}

		attackPoint.localPosition = new Vector3(forwardOffset, -0.01f, 0f);
	}

	private void ResolveAttackRange()
	{
		if (attackRange != null)
		{
			return;
		}

		ResolveAttackPoint();
		if (attackPoint == null)
		{
			return;
		}

		Transform[] transforms = attackPoint.GetComponentsInChildren<Transform>(true);
		for (int i = 0; i < transforms.Length; i++)
		{
			Transform candidate = transforms[i];
			if (candidate == null)
			{
				continue;
			}

			if (!candidate.name.Equals(attackRangeObjectName, System.StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}

			attackRange = candidate.GetComponent<Collider2D>();
			if (attackRange != null)
			{
				return;
			}
		}

		Collider2D[] colliders = attackPoint.GetComponentsInChildren<Collider2D>(true);
		for (int i = 0; i < colliders.Length; i++)
		{
			Collider2D candidate = colliders[i];
			if (candidate == null || !candidate.isTrigger)
			{
				continue;
			}

			attackRange = candidate;
			return;
		}
	}

	private void SetAttackRangeEnabled(bool enabled)
	{
		if (attackRange == null)
		{
			return;
		}

		if (attackRange.enabled == enabled)
		{
			return;
		}

		attackRange.enabled = enabled;
	}

	private bool TryGetAttackState(out AnimatorStateInfo stateInfo, out AnimationClip clip)
	{
		stateInfo = default;
		clip = null;
		if (animator == null)
		{
			return false;
		}

		AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(0);
		if (current.shortNameHash != attackStateHash)
		{
			return false;
		}

		stateInfo = current;
		animator.GetCurrentAnimatorClipInfo(0, clipInfoBuffer);
		if (clipInfoBuffer.Count > 0)
		{
			clip = clipInfoBuffer[0].clip;
		}
		clipInfoBuffer.Clear();
		return true;
	}

	private int GetClipFrameCount(AnimationClip clip)
	{
		if (clip != null)
		{
			float fps = Mathf.Max(1f, clip.frameRate);
			return Mathf.Max(1, Mathf.RoundToInt(clip.length * fps));
		}

		float fallbackDuration = Mathf.Max(0.1f, attackLockDuration);
		return Mathf.Max(1, Mathf.RoundToInt(fallbackDuration * 12f));
	}

	private static int GetCurrentFrame(AnimatorStateInfo stateInfo, int totalFrames)
	{
		float normalized = stateInfo.loop
			? stateInfo.normalizedTime - Mathf.Floor(stateInfo.normalizedTime)
			: Mathf.Clamp01(stateInfo.normalizedTime);
		int frame = Mathf.FloorToInt(normalized * totalFrames) + 1;
		return Mathf.Clamp(frame, 1, totalFrames);
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

