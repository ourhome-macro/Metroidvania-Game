using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(Animator))]
public class PlayerController2D : MonoBehaviour
{
    [Header("Move")]
    [SerializeField] private float moveSpeed = 6f;
    [SerializeField] private float acceleration = 50f;
    [SerializeField] private float deceleration = 60f;

    [Header("Jump")]
    [SerializeField] private float jumpForce = 14f;
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundCheckRadius = 0.12f;
    [SerializeField] private LayerMask groundLayer;

    [Header("Refs")]
    [SerializeField] private PlayerCombat playerCombat;

    private Rigidbody2D rb;
    private Animator animator;

    private float moveInput;
    private bool jumpPressed;
    private bool isGrounded;
    private bool facingRight = true;

    public bool IsGrounded => isGrounded;
    public int FacingDirection => facingRight ? 1 : -1;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();

        if (playerCombat == null)
        {
            playerCombat = GetComponent<PlayerCombat>();
        }
    }

    private void Update()
    {
        moveInput = Input.GetAxisRaw("Horizontal");

        if (Input.GetKeyDown(KeyCode.Space))
        {
            jumpPressed = true;
        }

        isGrounded = IsOnGround();

        if (playerCombat != null && playerCombat.IsMovementLocked)
        {
            moveInput = 0f;
            jumpPressed = false;
        }

        if (jumpPressed && isGrounded)
        {
            rb.velocity = new Vector2(rb.velocity.x, jumpForce);
            jumpPressed = false;
            isGrounded = false;
        }

        jumpPressed = false;

        UpdateFacing();
        UpdateAnimator();
    }

    private void FixedUpdate()
    {
        if (playerCombat != null && playerCombat.IsMovementLocked)
        {
            return;
        }

        float speedMultiplier = playerCombat != null ? playerCombat.MovementSpeedMultiplier : 1f;
        float targetSpeed = moveInput * moveSpeed * speedMultiplier;
        float delta = Mathf.Abs(targetSpeed) > 0.01f ? acceleration : deceleration;

        float newX = Mathf.MoveTowards(rb.velocity.x, targetSpeed, delta * Time.fixedDeltaTime);
        rb.velocity = new Vector2(newX, rb.velocity.y);
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
        if (groundCheck == null)
        {
            return false;
        }

        return Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer) != null;
    }

    private void UpdateAnimator()
    {
        bool isRunning = Mathf.Abs(rb.velocity.x) > 0.1f && isGrounded;
        animator.SetBool("isRunning", isRunning);
        animator.SetBool("isJumping", !isGrounded);
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
