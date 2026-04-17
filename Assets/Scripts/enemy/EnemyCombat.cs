using System.Collections.Generic;
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
    [SerializeField] private Transform attackPoint1;
    [SerializeField] private Collider2D attackRange1;
    [SerializeField] private Collider2D groundSlamRange2;
    [SerializeField] private Collider2D attack2Range;
    [SerializeField] private int attackRange1StartFrame = 6;
    [SerializeField] private int attackRange1EndFrame = -1;
    [SerializeField] private int groundSlamRange2StartFrame = 9;
    [SerializeField] private int groundSlamRange2EndFrame = 17;
    [SerializeField] private int attack2RangeStartFrame = 12;
    [SerializeField] private int attack2RangeEndFrame = 29;

    [Header("Facing")]
    [SerializeField] private bool mirrorHitPointsWithFacing = true;

    [Header("Contact Damage")]
    [SerializeField] private bool enableContactDamage = true;
    [SerializeField] private int contactDamage = 6;
    [SerializeField] private float contactDamageCooldown = 0.8f;
    [SerializeField] private float contactDamageRange = 0.45f;
    [SerializeField] private float maximumEffectiveContactDamageRange = 0.75f;
    [SerializeField] private float contactBodyRangeFactor = 0.3f;
    [SerializeField] private float contactPushbackDistance = 0.4f;
    [SerializeField] private float contactPushbackSpeed = 4.8f;
    [SerializeField] private Transform contactPoint;
    [SerializeField] private bool requirePhysicalTouchForContactDamage = true;

    [Header("Hit VFX")]
    [SerializeField] private GameObject hitBloodFxPrefab;
    [SerializeField] private Vector3 hitBloodFxOffset = new Vector3(0f, 0.35f, 0f);
    [SerializeField] private float hitBloodFxSpawnRadius = 0.2f;
    [SerializeField] private float hitBloodFxLifetime = 1.5f;
    [SerializeField] private float hitBloodFxScaleMultiplier = 1.65f;
    [SerializeField] private bool hitBloodFxUseEnemyLayer = true;
    [SerializeField] private bool alignHitBloodFxSortingWithEnemy = true;
    [SerializeField] private int hitBloodFxSortingOrderBoost = 6;

    [Header("Animator Params")]
    [SerializeField] private string attackTriggerName = "Attack";
    [SerializeField] private string attack2TriggerName = "Attack2";
    [SerializeField] private string groundSlamTriggerName = "GroundSlam";
    [SerializeField] private string hitTriggerName = "isHit";

    [Header("Boss Skill Routing")]
    [SerializeField] private float attack2MinDistance = 2.2f;
    [SerializeField] private float groundSlamMinDistance = 4.2f;
    [SerializeField] private float groundSlamCooldown = 3.4f;
    [SerializeField] private float specialAttackVerticalTolerance = 2.8f;

    [Header("Ground Slam Jump")]
    [SerializeField] private float groundSlamJumpDuration = 0.5f;
    [SerializeField] private float groundSlamJumpHeight = 2.1f;
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private float groundProbeDistance = 14f;
    [SerializeField] private float groundLandingPadding = 0.06f;

    [Header("Death")]
    [SerializeField] private float deathDestroyDelay = 1.1f;
    [SerializeField] private bool disableCollidersOnDeath = true;
    [SerializeField] private bool keepBossSolidCollidersOnDeath = true;
    [SerializeField] private bool freezeBossRigidbodyOnDeath = true;
    [SerializeField] private string deathBoolName = "isDead";
    [SerializeField] private string deathTriggerName = "Die";

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
    private bool hasDeathBoolParam;
    private bool hasDeathTriggerParam;
    private bool loggedMissingAttackTrigger;
    private bool loggedMissingAttack2Trigger;
    private bool loggedMissingGroundSlamTrigger;
    private bool loggedMissingHitTrigger;
    private bool loggedMissingDeathTrigger;
    private bool loggedMissingHitBloodFx;
    private Coroutine attackRoutine;
    private int attackToken;
    private bool contactDamageActiveByAi = true;
    private float nextContactDamageAllowedTime = -999f;
    private Vector3 attackPointBaseLocalPosition;
    private Vector3 attackPoint1BaseLocalPosition;
    private Vector3 contactPointBaseLocalPosition;
    private bool cachedHitPointLocalOffsets;
    private int facingDirection = 1;
    private Rigidbody2D rb;
    private float nextGroundSlamAllowedTime = -999f;
    private bool deathSequenceStarted;
    private readonly List<Collider2D> attackOverlapResults = new List<Collider2D>(16);
    private readonly HashSet<PlayerCombat> damagedPlayersThisAttack = new HashSet<PlayerCombat>();
    private readonly List<AnimatorClipInfo> clipInfoBuffer = new List<AnimatorClipInfo>(2);

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

        attackPoint = ResolveNamedChildTransform(attackPoint, "AttackPoint");
        attackPoint1 = ResolveNamedChildTransform(attackPoint1, "attackpoint1");
        contactPoint = ResolveNamedChildTransform(contactPoint, "ContactPoint");
        ResolveAttackRangeColliders();
        bodyCollider = ResolveBodyCollider();

        CacheAnimatorParams();
        CacheHitPointOffsets();

        SetFacingDirection(facingDirection);
        SetAttackPointVisible(!hideAttackPointOutsideAttack);
        SetAllAttackHitboxesEnabled(false);

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

    public bool ShouldStartSpecialAttack(float horizontalDistance, float verticalDistance)
    {
        if (isDead || isAttacking || Time.time < nextAttackAllowedTime)
        {
            return false;
        }

        if (verticalDistance > Mathf.Max(0f, specialAttackVerticalTolerance))
        {
            return false;
        }

        if (hasGroundSlamTriggerParam && horizontalDistance >= Mathf.Max(0f, groundSlamMinDistance) && Time.time >= nextGroundSlamAllowedTime)
        {
            return true;
        }

        if (hasAttack2TriggerParam && horizontalDistance >= Mathf.Max(0f, attack2MinDistance))
        {
            return true;
        }

        return false;
    }

    private IEnumerator AttackRoutine(int token, AttackMode mode)
    {
        isAttacking = true;
        currentAttackStartTime = Time.time;
        nextAttackAllowedTime = float.MaxValue;
        if (hideAttackPointOutsideAttack)
        {
            SetAttackPointVisible(false);
        }
        SetAllAttackHitboxesEnabled(false);
        damagedPlayersThisAttack.Clear();

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

        if (token == attackToken && !isDead)
        {
            if (HasFrameWindowHitbox(mode))
            {
                yield return RunFrameBasedAttackWindow(token, mode);
            }
            else
            {
                yield return RunLegacyAttackWindow(token, mode);
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
            SetAllAttackHitboxesEnabled(false);

            if (hideAttackPointOutsideAttack)
            {
                SetAttackPointVisible(false);
            }
        }
    }

    private IEnumerator RunLegacyAttackWindow(int token, AttackMode mode)
    {
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
    }

    private IEnumerator RunFrameBasedAttackWindow(int token, AttackMode mode)
    {
        if (mode == AttackMode.GroundSlam)
        {
            yield return PerformGroundSlamJump();
            nextGroundSlamAllowedTime = Time.time + Mathf.Max(0.25f, groundSlamCooldown);
        }

        bool enteredAttackState = false;
        float stateEnterDeadline = Time.time + Mathf.Max(0.3f, attackWindup + 1f);
        string stateName = GetStateNameForMode(mode);
        SetHitboxActiveForMode(mode, false);
        while (token == attackToken && !isDead)
        {
            if (!TryGetAnimatorState(stateName, out AnimatorStateInfo stateInfo, out AnimationClip clip))
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
            GetFrameWindow(mode, totalFrames, out int startFrame, out int endFrame);
            bool hitboxActive = currentFrame >= startFrame && currentFrame <= endFrame;
            SetHitboxActiveForMode(mode, hitboxActive);
            if (hitboxActive)
            {
                DealDamageToPlayer(mode);
            }

            if (stateInfo.normalizedTime >= 1f && !animator.IsInTransition(0))
            {
                break;
            }

            yield return null;
        }

        if (!enteredAttackState && token == attackToken && !isDead)
        {
            SetHitboxActiveForMode(mode, true);
            DealDamageToPlayer(mode);
        }

        SetHitboxActiveForMode(mode, false);
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
        float targetX = playerTarget.position.x;
        float targetY = ResolveGroundSlamLandingY(targetX, start.y);
        Vector2 target = new Vector2(targetX, targetY);
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
        if (rb.gravityScale > 0f)
        {
            Vector2 v = rb.velocity;
            rb.velocity = new Vector2(v.x, Mathf.Min(v.y, -1.5f));
        }
    }

    private float ResolveGroundSlamLandingY(float targetX, float fallbackY)
    {
        float probeDistance = Mathf.Max(2f, groundProbeDistance);
        float rayOriginY = Mathf.Max(transform.position.y, playerTarget != null ? playerTarget.position.y : fallbackY) + 2f;
        int mask = groundLayer.value == 0 ? LayerMask.GetMask("Ground") : groundLayer.value;
        if (mask == 0)
        {
            return fallbackY;
        }

        RaycastHit2D[] hits = Physics2D.RaycastAll(new Vector2(targetX, rayOriginY), Vector2.down, probeDistance, mask);
        if (hits == null || hits.Length == 0)
        {
            return fallbackY;
        }

        RaycastHit2D bestHit = default;
        bool found = false;
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hitCollider = hits[i].collider;
            if (hitCollider == null || hitCollider.transform.IsChildOf(transform) || hitCollider.isTrigger)
            {
                continue;
            }

            bestHit = hits[i];
            found = true;
            break;
        }

        if (!found)
        {
            return fallbackY;
        }

        float bottomOffset = 0f;
        if (bodyCollider != null)
        {
            float pivotY = rb != null ? rb.position.y : transform.position.y;
            bottomOffset = Mathf.Max(0f, pivotY - bodyCollider.bounds.min.y);
        }

        return bestHit.point.y + bottomOffset + Mathf.Max(0f, groundLandingPadding);
    }

    /// <summary>
    /// 閸欘垵顫﹂崝銊ф暰娴滃娆㈢拫鍐暏閿涘奔绡冮崣顖滄暠閸楀繒鈻奸崗婊冪俺鐠嬪啰鏁ら妴?
    /// </summary>
    public void DealDamageToPlayer()
    {
        Collider2D activeRange = GetActiveRangeHitbox();
        if (activeRange != null)
        {
            DealDamageToPlayer(activeRange);
            return;
        }

        DealDamageToPlayerByCircle();
    }

    private void DealDamageToPlayer(AttackMode mode)
    {
        Collider2D range = GetRangeHitboxForMode(mode);
        if (range != null)
        {
            DealDamageToPlayer(range);
            return;
        }

        DealDamageToPlayerByCircle();
    }

    private void DealDamageToPlayerByCircle()
    {
        attackPoint = ResolveNamedChildTransform(attackPoint, "AttackPoint");
        if (attackPoint == null)
        {
            Debug.LogWarning("[EnemyCombat] attackPoint is not assigned, cannot attack player.", this);
            return;
        }

        int mask = playerLayer.value == 0 ? Physics2D.AllLayers : playerLayer.value;
        Collider2D[] cols = Physics2D.OverlapCircleAll(attackPoint.position, EffectiveAttackRange, mask);
        for (int i = 0; i < cols.Length; i++)
        {
            PlayerCombat player = cols[i].GetComponentInParent<PlayerCombat>();
            if (player == null || !damagedPlayersThisAttack.Add(player))
            {
                continue;
            }

            SendAttackData(player, attackPoint.position);
        }
    }

    private void DealDamageToPlayer(Collider2D hitbox)
    {
        if (hitbox == null || !hitbox.enabled || !hitbox.gameObject.activeInHierarchy)
        {
            return;
        }

        int mask = playerLayer.value == 0 ? Physics2D.AllLayers : playerLayer.value;
        ContactFilter2D filter = new ContactFilter2D();
        filter.useLayerMask = true;
        filter.layerMask = mask;
        filter.useTriggers = true;
        attackOverlapResults.Clear();
        int count = hitbox.OverlapCollider(filter, attackOverlapResults);
        for (int i = 0; i < count; i++)
        {
            PlayerCombat player = attackOverlapResults[i].GetComponentInParent<PlayerCombat>();
            if (player == null || !damagedPlayersThisAttack.Add(player))
            {
                continue;
            }

            Vector2 hitPoint = hitbox.bounds.center;
            SendAttackData(player, hitPoint);
        }

        attackOverlapResults.Clear();
    }

    private void SendAttackData(PlayerCombat player, Vector2 hitPoint)
    {
        AttackData data = new AttackData(
            attackDamage,
            this,
            currentAttackStartTime,
            hitPoint
        );

        player.ReceiveAttack(data);
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

        SpawnHitBloodFx(source);
        PlayHit();

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    private void SpawnHitBloodFx(PlayerCombat source)
    {
        float radius = Mathf.Max(0f, hitBloodFxSpawnRadius);
        Vector2 randomOffset = radius > 0f ? Random.insideUnitCircle * radius : Vector2.zero;
        Vector3 spawnPosition = ResolveHitBloodSpawnBasePosition() + hitBloodFxOffset + new Vector3(randomOffset.x, randomOffset.y, 0f);

        int hitFromDirection = 1;
        if (source != null)
        {
            hitFromDirection = source.transform.position.x <= transform.position.x ? 1 : -1;
        }
        else if (playerTarget != null)
        {
            hitFromDirection = playerTarget.position.x <= transform.position.x ? 1 : -1;
        }

        Quaternion rotation = hitFromDirection < 0
            ? Quaternion.Euler(0f, 180f, 0f)
            : Quaternion.identity;

        if (hitBloodFxPrefab == null)
        {
            SpawnFallbackHitBloodFx(spawnPosition, hitFromDirection);
            return;
        }

        GameObject fx = Instantiate(hitBloodFxPrefab, spawnPosition, rotation);
        ConfigureHitBloodFxInstance(fx);
        Destroy(fx, Mathf.Max(0.2f, hitBloodFxLifetime));
    }

    private void SpawnFallbackHitBloodFx(Vector3 spawnPosition, int hitFromDirection)
    {
        if (!loggedMissingHitBloodFx)
        {
            loggedMissingHitBloodFx = true;
            Debug.LogWarning("[EnemyCombat] hitBloodFxPrefab is missing, using fallback blood effect.", this);
        }

        GameObject go = new GameObject("BloodHitFallbackFx");
        go.transform.position = spawnPosition;
        go.transform.rotation = hitFromDirection < 0 ? Quaternion.Euler(0f, 180f, 0f) : Quaternion.identity;
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();

        ParticleSystem.MainModule main = ps.main;
        main.loop = false;
        main.playOnAwake = true;
        main.duration = 0.28f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.16f, 0.32f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(3.5f, 6.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.07f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.42f, 0.04f, 0.04f, 0.95f),
            new Color(0.58f, 0.06f, 0.06f, 0.95f)
        );
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 0.65f;
        main.stopAction = ParticleSystemStopAction.Destroy;

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[]
        {
            new ParticleSystem.Burst(0f, (short)10, (short)16, 1, 0.01f)
        });

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 18f;
        shape.radius = 0.02f;
        shape.arc = 22f;
        shape.rotation = new Vector3(0f, -90f, 0f);

        if (renderer != null)
        {
            renderer.sortingOrder = 20;
        }

        ConfigureHitBloodFxInstance(go);
        Destroy(go, Mathf.Max(0.4f, hitBloodFxLifetime));
    }

    private Vector3 ResolveHitBloodSpawnBasePosition()
    {
        if (bodyCollider != null)
        {
            Bounds b = bodyCollider.bounds;
            float y = Mathf.Lerp(b.min.y, b.max.y, 0.62f);
            return new Vector3(b.center.x, y, b.center.z);
        }

        return transform.position;
    }

    private void ConfigureHitBloodFxInstance(GameObject fx)
    {
        if (fx == null)
        {
            return;
        }

        ApplyHitBloodFxTransformAndLayer(fx);
        EnsureHitBloodFxVisible(fx);
        ForcePlayAllParticleSystems(fx);
    }

    private void ApplyHitBloodFxTransformAndLayer(GameObject fx)
    {
        if (fx == null)
        {
            return;
        }

        float scale = Mathf.Max(0.01f, hitBloodFxScaleMultiplier);
        fx.transform.localScale = Vector3.Scale(fx.transform.localScale, new Vector3(scale, scale, 1f));

        if (!hitBloodFxUseEnemyLayer)
        {
            return;
        }

        int layer = gameObject.layer;
        ApplyLayerRecursively(fx.transform, layer);
    }

    private void ApplyLayerRecursively(Transform root, int layer)
    {
        if (root == null)
        {
            return;
        }

        root.gameObject.layer = layer;
        for (int i = 0; i < root.childCount; i++)
        {
            ApplyLayerRecursively(root.GetChild(i), layer);
        }
    }

    private void EnsureHitBloodFxVisible(GameObject fx)
    {
        int boostedOrder = Mathf.Max(1, hitBloodFxSortingOrderBoost);
        int targetOrder = boostedOrder;
        int targetLayerId = 0;
        bool hasTargetLayer = false;
        int highestOwnerSortingOrder = int.MinValue;

        if (alignHitBloodFxSortingWithEnemy)
        {
            SpriteRenderer[] ownerRenderers = GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < ownerRenderers.Length; i++)
            {
                SpriteRenderer ownerRenderer = ownerRenderers[i];
                if (ownerRenderer == null)
                {
                    continue;
                }

                if (ownerRenderer.sortingOrder > highestOwnerSortingOrder)
                {
                    highestOwnerSortingOrder = ownerRenderer.sortingOrder;
                    targetLayerId = ownerRenderer.sortingLayerID;
                    hasTargetLayer = true;
                }

                targetOrder = Mathf.Max(targetOrder, ownerRenderer.sortingOrder + boostedOrder);
            }
        }

        ParticleSystemRenderer[] fxRenderers = fx.GetComponentsInChildren<ParticleSystemRenderer>(true);
        for (int i = 0; i < fxRenderers.Length; i++)
        {
            ParticleSystemRenderer fxRenderer = fxRenderers[i];
            if (fxRenderer == null)
            {
                continue;
            }

            if (hasTargetLayer)
            {
                fxRenderer.sortingLayerID = targetLayerId;
            }

            fxRenderer.sortingOrder = Mathf.Max(fxRenderer.sortingOrder, targetOrder);
        }

        SpriteRenderer[] fxSpriteRenderers = fx.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < fxSpriteRenderers.Length; i++)
        {
            SpriteRenderer fxRenderer = fxSpriteRenderers[i];
            if (fxRenderer == null)
            {
                continue;
            }

            if (hasTargetLayer)
            {
                fxRenderer.sortingLayerID = targetLayerId;
            }

            fxRenderer.sortingOrder = Mathf.Max(fxRenderer.sortingOrder, targetOrder);
        }
    }

    private void ForcePlayAllParticleSystems(GameObject fx)
    {
        ParticleSystem[] allFxParticleSystems = fx.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < allFxParticleSystems.Length; i++)
        {
            ParticleSystem ps = allFxParticleSystems[i];
            if (ps != null)
            {
                ps.Play(true);
            }
        }
    }

    /// <summary>
    /// 鐞氼偄鐣紘搴¤剨閸欏秵妞傜拫鍐暏閿涙俺绻戞潻妯规縺鐎瑰啿鑻熼幍鎾存焽瑜版挸澧犻弨璇插毊閵?
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
        hasDeathBoolParam = false;
        hasDeathTriggerParam = false;

        if (animator == null)
        {
            return;
        }

        AnimatorControllerParameter[] parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            AnimatorControllerParameter parameter = parameters[i];

            if (parameter.type == AnimatorControllerParameterType.Trigger)
            {
                if (parameter.name == attackTriggerName)
                {
                    hasAttackTriggerParam = true;
                }

                if (parameter.name == attack2TriggerName)
                {
                    hasAttack2TriggerParam = true;
                }

                if (parameter.name == groundSlamTriggerName)
                {
                    hasGroundSlamTriggerParam = true;
                }

                if (parameter.name == hitTriggerName)
                {
                    hasHitTriggerParam = true;
                }

                if (parameter.name == deathTriggerName)
                {
                    hasDeathTriggerParam = true;
                }
            }

            if (parameter.type == AnimatorControllerParameterType.Bool && parameter.name == deathBoolName)
            {
                hasDeathBoolParam = true;
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

    private Transform ResolveNamedChildTransform(Transform current, string desiredName)
    {
        if (current != null)
        {
            return current;
        }

        Transform[] transforms = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform candidate = transforms[i];
            if (candidate != null && candidate != transform && candidate.name.Equals(desiredName, System.StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }

        return null;
    }

    private Collider2D ResolveNamedChildCollider(Collider2D current, Transform root, string desiredName)
    {
        if (current != null)
        {
            return current;
        }

        if (root == null)
        {
            return null;
        }

        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform candidate = transforms[i];
            if (candidate == null)
            {
                continue;
            }

            if (!candidate.name.Equals(desiredName, System.StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            Collider2D hitbox = candidate.GetComponent<Collider2D>();
            if (hitbox != null)
            {
                return hitbox;
            }
        }

        return null;
    }

    private void ResolveAttackRangeColliders()
    {
        attackPoint = ResolveNamedChildTransform(attackPoint, "AttackPoint");
        attackPoint1 = ResolveNamedChildTransform(attackPoint1, "attackpoint1");
        attackRange1 = ResolveNamedChildCollider(attackRange1, attackPoint1, "Range1");
        groundSlamRange2 = ResolveNamedChildCollider(groundSlamRange2, attackPoint1, "Range2");
        attack2Range = ResolveNamedChildCollider(attack2Range, attackPoint, "Range");

        if (attackRange1 == null)
        {
            attackRange1 = FindFirstTriggerCollider(attackPoint1);
        }

        if (groundSlamRange2 == null && attackPoint1 != null)
        {
            Collider2D[] candidates = attackPoint1.GetComponentsInChildren<Collider2D>(true);
            for (int i = 0; i < candidates.Length; i++)
            {
                Collider2D candidate = candidates[i];
                if (candidate == null || !candidate.isTrigger || candidate == attackRange1)
                {
                    continue;
                }

                groundSlamRange2 = candidate;
                break;
            }
        }

        if (attack2Range == null)
        {
            attack2Range = FindFirstTriggerCollider(attackPoint);
        }
    }

    private Collider2D FindFirstTriggerCollider(Transform root)
    {
        if (root == null)
        {
            return null;
        }

        Collider2D[] colliders = root.GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider2D candidate = colliders[i];
            if (candidate != null && candidate.isTrigger)
            {
                return candidate;
            }
        }

        return null;
    }

    private bool HasFrameWindowHitbox(AttackMode mode)
    {
        return animator != null && GetRangeHitboxForMode(mode) != null;
    }

    private Collider2D GetRangeHitboxForMode(AttackMode mode)
    {
        ResolveAttackRangeColliders();
        switch (mode)
        {
            case AttackMode.Attack2:
                return attack2Range;
            case AttackMode.GroundSlam:
                return groundSlamRange2;
            default:
                return attackRange1;
        }
    }

    private Transform GetAttackPointForMode(AttackMode mode)
    {
        switch (mode)
        {
            case AttackMode.Attack2:
                return attackPoint;
            case AttackMode.GroundSlam:
            case AttackMode.Attack:
                return attackPoint1 != null ? attackPoint1 : attackPoint;
            default:
                return attackPoint;
        }
    }

    private void SetAllAttackHitboxesEnabled(bool enabled)
    {
        SetHitboxEnabled(attackRange1, enabled);
        SetHitboxEnabled(groundSlamRange2, enabled);
        SetHitboxEnabled(attack2Range, enabled);
    }

    private void SetHitboxActiveForMode(AttackMode mode, bool active)
    {
        Collider2D hitbox = GetRangeHitboxForMode(mode);
        if (hitbox == null)
        {
            return;
        }

        Transform owner = GetAttackPointForMode(mode);
        if (owner != null && hideAttackPointOutsideAttack)
        {
            SetAttackRootVisible(owner, active);
        }

        SetHitboxEnabled(hitbox, active);
    }

    private static void SetHitboxEnabled(Collider2D hitbox, bool enabled)
    {
        if (hitbox == null || hitbox.enabled == enabled)
        {
            return;
        }

        hitbox.enabled = enabled;
    }

    private Collider2D GetActiveRangeHitbox()
    {
        if (attackRange1 != null && attackRange1.enabled && attackRange1.gameObject.activeInHierarchy)
        {
            return attackRange1;
        }

        if (groundSlamRange2 != null && groundSlamRange2.enabled && groundSlamRange2.gameObject.activeInHierarchy)
        {
            return groundSlamRange2;
        }

        if (attack2Range != null && attack2Range.enabled && attack2Range.gameObject.activeInHierarchy)
        {
            return attack2Range;
        }

        return null;
    }

    private string GetStateNameForMode(AttackMode mode)
    {
        switch (mode)
        {
            case AttackMode.Attack2:
                return "Attack2";
            case AttackMode.GroundSlam:
                return "GroundSlam";
            default:
                return "Attack";
        }
    }

    private bool TryGetAnimatorState(string stateName, out AnimatorStateInfo stateInfo, out AnimationClip clip)
    {
        stateInfo = default;
        clip = null;
        if (animator == null || string.IsNullOrEmpty(stateName))
        {
            return false;
        }

        AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(0);
        if (!AnimatorStateMatches(current, stateName))
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

    private static bool AnimatorStateMatches(AnimatorStateInfo stateInfo, string stateName)
    {
        return stateInfo.IsName(stateName) || stateInfo.shortNameHash == Animator.StringToHash(stateName);
    }

    private int GetClipFrameCount(AnimationClip clip)
    {
        if (clip != null)
        {
            float fps = Mathf.Max(1f, clip.frameRate);
            return Mathf.Max(1, Mathf.RoundToInt(clip.length * fps));
        }

        float fallbackFps = Mathf.Max(1f, attackClipFps);
        float fallbackDuration = Mathf.Max(0.1f, attackWindup + attackRecovery + hitboxActiveDuration);
        return Mathf.Max(1, Mathf.RoundToInt(fallbackDuration * fallbackFps));
    }

    private static int GetCurrentFrame(AnimatorStateInfo stateInfo, int totalFrames)
    {
        float normalized = stateInfo.loop
            ? stateInfo.normalizedTime - Mathf.Floor(stateInfo.normalizedTime)
            : Mathf.Clamp01(stateInfo.normalizedTime);
        int frame = Mathf.FloorToInt(normalized * totalFrames) + 1;
        return Mathf.Clamp(frame, 1, totalFrames);
    }

    private void GetFrameWindow(AttackMode mode, int totalFrames, out int startFrame, out int endFrame)
    {
        switch (mode)
        {
            case AttackMode.Attack2:
                startFrame = NormalizeFrameBoundary(attack2RangeStartFrame, totalFrames, 1);
                endFrame = NormalizeFrameBoundary(attack2RangeEndFrame, totalFrames, totalFrames);
                break;
            case AttackMode.GroundSlam:
                startFrame = NormalizeFrameBoundary(groundSlamRange2StartFrame, totalFrames, 1);
                endFrame = NormalizeFrameBoundary(groundSlamRange2EndFrame, totalFrames, totalFrames);
                break;
            default:
                startFrame = NormalizeFrameBoundary(attackRange1StartFrame, totalFrames, 1);
                endFrame = NormalizeFrameBoundary(attackRange1EndFrame, totalFrames, totalFrames);
                break;
        }

        if (endFrame < startFrame)
        {
            endFrame = startFrame;
        }
    }

    private static int NormalizeFrameBoundary(int configuredFrame, int totalFrames, int fallbackValue)
    {
        int frame = configuredFrame <= 0 ? fallbackValue : configuredFrame;
        return Mathf.Clamp(frame, 1, totalFrames);
    }

    private Collider2D ResolveBodyCollider()
    {
        if (bodyCollider != null)
        {
            return bodyCollider;
        }

        Collider2D rootCollider = GetComponent<Collider2D>();
        if (rootCollider != null && !rootCollider.isTrigger)
        {
            return rootCollider;
        }

        Collider2D[] colliders = GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider2D candidate = colliders[i];
            if (candidate == null || candidate.isTrigger)
            {
                continue;
            }

            return candidate;
        }

        return rootCollider;
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
        if (deathSequenceStarted)
        {
            return;
        }

        deathSequenceStarted = true;
        InterruptAttack();
        isDead = true;

        if (rb != null)
        {
            rb.velocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        if (disableCollidersOnDeath)
        {
            ApplyDeathColliderPolicy();
        }

        if (freezeBossRigidbodyOnDeath && IsBossEntity())
        {
            FreezeRigidbodyForDeath();
        }

        PublishDeathEvent();
        PlayDeath();

        float destroyDelay = Mathf.Max(0f, deathDestroyDelay);
        if (destroyDelay <= 0f)
        {
            Destroy(gameObject);
            return;
        }

        StartCoroutine(DestroyAfterDeathDelay(destroyDelay));
    }

    private void PublishDeathEvent()
    {
        if (IsBossEntity())
        {
            GameEvents.BossDeath();
            return;
        }

        GameEvents.EnemyDeath(gameObject);
    }

    private void PlayDeath()
    {
        if (animator == null)
        {
            return;
        }

        if (hasDeathBoolParam && !string.IsNullOrEmpty(deathBoolName))
        {
            animator.SetBool(deathBoolName, true);
        }

        if (!string.IsNullOrEmpty(deathTriggerName))
        {
            TrySetTrigger(deathTriggerName, ref loggedMissingDeathTrigger, hasDeathTriggerParam);
        }
    }

    private IEnumerator DestroyAfterDeathDelay(float delay)
    {
        yield return new WaitForSeconds(delay);

        if (this != null)
        {
            Destroy(gameObject);
        }
    }

    private void ApplyDeathColliderPolicy()
    {
        Collider2D[] colliders = GetComponentsInChildren<Collider2D>(true);
        bool keepSolidColliders = keepBossSolidCollidersOnDeath && IsBossEntity();

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider2D candidate = colliders[i];
            if (candidate == null)
            {
                continue;
            }

            bool keepThisCollider = keepSolidColliders && !candidate.isTrigger;
            candidate.enabled = keepThisCollider;
        }
    }

    private void FreezeRigidbodyForDeath()
    {
        if (rb == null)
        {
            return;
        }

        rb.velocity = Vector2.zero;
        rb.angularVelocity = 0f;
        rb.gravityScale = 0f;
        rb.constraints = RigidbodyConstraints2D.FreezeAll;
    }

    private bool IsBossEntity()
    {
        return string.Equals(tag, "Boss", System.StringComparison.OrdinalIgnoreCase);
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
        SetAllAttackHitboxesEnabled(false);
        damagedPlayersThisAttack.Clear();

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

        if (requirePhysicalTouchForContactDamage && (bodyCollider == null || bodyCollider.isTrigger))
        {
            return;
        }

        Vector2 point = GetContactSamplePoint();
        int mask = playerLayer.value == 0 ? Physics2D.AllLayers : playerLayer.value;
        Collider2D[] cols = Physics2D.OverlapCircleAll(point, GetEffectiveContactQueryRange(), mask);
        for (int i = 0; i < cols.Length; i++)
        {
            PlayerCombat player = cols[i].GetComponentInParent<PlayerCombat>();
            if (player == null)
            {
                continue;
            }

            bool mustTouch = requirePhysicalTouchForContactDamage;
            if (mustTouch && !IsTouchingPlayer(player))
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
            ApplyContactPushback(player);
            nextContactDamageAllowedTime = Time.time + Mathf.Max(0.1f, contactDamageCooldown);
            return;
        }
    }

    private void ApplyContactPushback(PlayerCombat player)
    {
        if (player == null)
        {
            return;
        }

        Rigidbody2D playerRb = player.GetComponent<Rigidbody2D>();
        if (playerRb == null)
        {
            return;
        }

        float pushDistance = Mathf.Max(0f, contactPushbackDistance);
        if (pushDistance <= 0f)
        {
            return;
        }

        float enemyX = rb != null ? rb.position.x : transform.position.x;
        float deltaX = playerRb.position.x - enemyX;
        float dirX = Mathf.Abs(deltaX) > 0.01f ? Mathf.Sign(deltaX) : -facingDirection;
        float pushSpeed = Mathf.Max(0f, contactPushbackSpeed);
        if (pushSpeed > 0f)
        {
            playerRb.velocity = new Vector2(dirX * pushSpeed, playerRb.velocity.y);
        }
        Vector2 target = playerRb.position + new Vector2(dirX * pushDistance, 0f);
        playerRb.MovePosition(target);
    }

    private Vector2 GetContactSamplePoint()
    {
        if (contactPoint != null)
        {
            return contactPoint.position;
        }

        if (bodyCollider != null)
        {
            return bodyCollider.bounds.center;
        }

        return transform.position;
    }

    private float GetEffectiveContactQueryRange()
    {
        float range = EffectiveContactDamageRange;
        if (bodyCollider != null)
        {
            float factor = Mathf.Clamp(contactBodyRangeFactor, 0f, 1f);
            float colliderRange = Mathf.Max(bodyCollider.bounds.extents.x, bodyCollider.bounds.extents.y) * factor;
            range = Mathf.Max(range, colliderRange);
        }

        float maxRange = Mathf.Max(0.08f, maximumEffectiveContactDamageRange);
        return Mathf.Clamp(range, 0.08f, maxRange);
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

        if (attackPoint1 != null)
        {
            attackPoint1BaseLocalPosition = attackPoint1.localPosition;
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

        if (attackPoint1 != null)
        {
            Vector3 p = attackPoint1BaseLocalPosition;
            p.x = Mathf.Abs(p.x) * facingDirection;
            attackPoint1.localPosition = p;
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
        SetAttackRootVisible(attackPoint, visible);
        SetAttackRootVisible(attackPoint1, visible);
    }

    private static void SetAttackRootVisible(Transform attackRoot, bool visible)
    {
        if (attackRoot == null)
        {
            return;
        }

        if (attackRoot.gameObject.activeSelf == visible)
        {
            return;
        }

        attackRoot.gameObject.SetActive(visible);
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
            Gizmos.DrawWireSphere(GetContactSamplePoint(), GetEffectiveContactQueryRange());
        }
        else
        {
            Gizmos.color = new Color(1f, 0.55f, 0f, 1f);
            Gizmos.DrawWireSphere(GetContactSamplePoint(), GetEffectiveContactQueryRange());
        }

        if (playerTarget != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, engageRange);
        }
    }
}

