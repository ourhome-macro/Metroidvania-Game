using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(Animator))]
public class PlayerController2D : MonoBehaviour
{
    [Header("Config")]
    [SerializeField] private PlayerConfigSO playerConfig;

    [Header("Move")]
    [SerializeField] private float moveSpeed = 6f;
    [SerializeField] private float acceleration = 50f;
    [SerializeField] private float deceleration = 60f;
    [SerializeField] private float airAcceleration = 40f;

    [Header("Jump")]
    [SerializeField] private float jumpForce = 14f;
    [SerializeField] private float coyoteTime = 0.1f;
    [SerializeField] private float jumpBufferTime = 0.1f;
    [SerializeField] private float fallMultiplier = 2.2f;
    [SerializeField] private float lowJumpMultiplier = 3f;
    [SerializeField] private float maxFallSpeed = 24f;
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundCheckRadius = 0.12f;
    [SerializeField] private LayerMask groundLayer;

    [Header("Refs")]
    [SerializeField] private PlayerCombat playerCombat;

    private Rigidbody2D rb;
    private Animator animator;
    private Collider2D bodyCollider;

    private float moveInput;
    private bool isGrounded;
    private bool facingRight = true;
    private bool jumpHeld;
    private float coyoteTimer;
    private float jumpBufferTimer;

    public bool IsGrounded => isGrounded;
    public int FacingDirection => facingRight ? 1 : -1;
    public float MoveInput => moveInput;
    public Vector2 Velocity => rb != null ? rb.velocity : Vector2.zero;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        bodyCollider = GetComponent<Collider2D>();
        facingRight = transform.localScale.x >= 0f;

        ApplyConfig();
        ResolveGroundCheck();

        if (playerCombat == null)
        {
            playerCombat = GetComponent<PlayerCombat>();
        }
    }

    private void Update()
    {
        moveInput = Input.GetAxisRaw("Horizontal");
        bool jumpPressedThisFrame = Input.GetButtonDown("Jump") || Input.GetKeyDown(KeyCode.Space);
        jumpHeld = Input.GetButton("Jump") || Input.GetKey(KeyCode.Space);

        isGrounded = IsOnGround();
        UpdateJumpTimers(jumpPressedThisFrame);

        if (playerCombat != null && playerCombat.IsMovementLocked)
        {
            moveInput = 0f;
            jumpBufferTimer = 0f;
        }

        if (CanConsumeJump())
        {
            rb.velocity = new Vector2(rb.velocity.x, jumpForce);
            jumpBufferTimer = 0f;
            coyoteTimer = 0f;
            isGrounded = false;
        }

        UpdateFacing();
        UpdateAnimator();
    }

    private void FixedUpdate()
    {
        Vector2 velocity = rb.velocity;

        if (playerCombat != null && playerCombat.IsMovementLocked)
        {
            if (!playerCombat.IsSkipping)
            {
                velocity.x = 0f;
            }
        }
        else
        {
            float speedMultiplier = playerCombat != null ? playerCombat.MovementSpeedMultiplier : 1f;
            float targetSpeed = moveInput * moveSpeed * speedMultiplier;
            float activeAcceleration = isGrounded ? acceleration : airAcceleration;
            float activeDeceleration = isGrounded ? deceleration : airAcceleration;
            float delta = Mathf.Abs(targetSpeed) > 0.01f ? activeAcceleration : activeDeceleration;
            velocity.x = Mathf.MoveTowards(velocity.x, targetSpeed, delta * Time.fixedDeltaTime);
        }

        ApplyJumpGravity(ref velocity);
        rb.velocity = velocity;
    }

    private void UpdateFacing()
    {
        if (moveInput > 0f && !facingRight)
        {
            Flip();
        }
        else if (moveInput < 0f && facingRight)
        {
            Flip();
        }
    }

    private void Flip()
    {
        facingRight = !facingRight;
        Vector3 scale = transform.localScale;
        scale.x *= -1f;
        transform.localScale = scale;
    }

    private bool IsOnGround()
    {
        LayerMask effectiveGroundLayer = groundLayer.value == 0 ? LayerMask.GetMask("Ground") : groundLayer;

        if (groundCheck != null)
        {
            return Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, effectiveGroundLayer) != null;
        }

        if (bodyCollider == null)
        {
            return false;
        }

        Bounds b = bodyCollider.bounds;
        Vector2 checkPos = new Vector2(b.center.x, b.min.y - 0.03f);
        Vector2 checkSize = new Vector2(Mathf.Max(0.05f, b.size.x * 0.7f), 0.08f);
        return Physics2D.OverlapBox(checkPos, checkSize, 0f, effectiveGroundLayer) != null;
    }

    private void ApplyConfig()
    {
        if (playerConfig == null)
        {
            return;
        }

        moveSpeed = Mathf.Max(0.01f, playerConfig.MoveSpeed);
        acceleration = Mathf.Max(0f, playerConfig.GroundAcceleration);
        deceleration = Mathf.Max(0f, playerConfig.GroundDeceleration);
        airAcceleration = Mathf.Max(0f, playerConfig.AirAcceleration);
        jumpForce = Mathf.Max(0f, playerConfig.JumpForce);
        coyoteTime = Mathf.Max(0f, playerConfig.CoyoteTime);
        jumpBufferTime = Mathf.Max(0f, playerConfig.JumpBufferTime);
        fallMultiplier = Mathf.Max(1f, playerConfig.FallMultiplier);
        lowJumpMultiplier = Mathf.Max(1f, playerConfig.LowJumpMultiplier);
        maxFallSpeed = Mathf.Max(0f, playerConfig.MaxFallSpeed);
    }

    private void UpdateJumpTimers(bool jumpPressedThisFrame)
    {
        if (jumpPressedThisFrame)
        {
            jumpBufferTimer = jumpBufferTime;
        }
        else
        {
            jumpBufferTimer = Mathf.Max(0f, jumpBufferTimer - Time.deltaTime);
        }

        coyoteTimer = isGrounded
            ? coyoteTime
            : Mathf.Max(0f, coyoteTimer - Time.deltaTime);
    }

    private bool CanConsumeJump()
    {
        if (jumpBufferTimer <= 0f)
        {
            return false;
        }

        if (playerCombat != null && playerCombat.IsMovementLocked)
        {
            return false;
        }

        return isGrounded || coyoteTimer > 0f;
    }

    private void ApplyJumpGravity(ref Vector2 velocity)
    {
        if (isGrounded && velocity.y <= 0f)
        {
            velocity.y = Mathf.Max(velocity.y, -maxFallSpeed);
            return;
        }

        float gravityMultiplier = 1f;
        if (velocity.y < 0f)
        {
            gravityMultiplier = Mathf.Max(1f, fallMultiplier);
        }
        else if (velocity.y > 0f && !jumpHeld)
        {
            gravityMultiplier = Mathf.Max(1f, lowJumpMultiplier);
        }

        if (gravityMultiplier > 1f)
        {
            float extraGravity = Physics2D.gravity.y * rb.gravityScale * (gravityMultiplier - 1f) * Time.fixedDeltaTime;
            velocity.y += extraGravity;
        }

        velocity.y = Mathf.Max(velocity.y, -maxFallSpeed);
    }

    private void ResolveGroundCheck()
    {
        if (groundCheck != null)
        {
            return;
        }

        Transform[] transforms = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform candidate = transforms[i];
            if (candidate != null && candidate != transform && candidate.name.Equals("GroundCheck", System.StringComparison.OrdinalIgnoreCase))
            {
                groundCheck = candidate;
                return;
            }
        }
    }

    private void UpdateAnimator()
    {
        bool isRunning = Mathf.Abs(rb.velocity.x) > 0.1f && isGrounded;
        animator.SetBool("isRunning", isRunning);
        animator.SetBool("isJumping", !isGrounded);
    }

    private void OnValidate()
    {
        moveSpeed = Mathf.Max(0.01f, moveSpeed);
        acceleration = Mathf.Max(0f, acceleration);
        deceleration = Mathf.Max(0f, deceleration);
        airAcceleration = Mathf.Max(0f, airAcceleration);
        jumpForce = Mathf.Max(0f, jumpForce);
        coyoteTime = Mathf.Max(0f, coyoteTime);
        jumpBufferTime = Mathf.Max(0f, jumpBufferTime);
        fallMultiplier = Mathf.Max(1f, fallMultiplier);
        lowJumpMultiplier = Mathf.Max(1f, lowJumpMultiplier);
        maxFallSpeed = Mathf.Max(0f, maxFallSpeed);
        groundCheckRadius = Mathf.Max(0.01f, groundCheckRadius);
    }

    private void OnDrawGizmosSelected()
    {
        if (groundCheck == null)
        {
            return;
        }

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
    }
}
