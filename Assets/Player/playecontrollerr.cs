using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class playecontrollerr : MonoBehaviour
{
	[Header("Data")]
	[SerializeField] private PlayerConfigSO playerConfig;

	[Header("Ground Check")]
	[SerializeField] private Transform groundCheck;
	[SerializeField] private float groundCheckRadius = 0.12f;
	[SerializeField] private LayerMask groundLayer;

	private Rigidbody2D rb;
	private float baseGravityScale;

	private float moveInput;
	private bool jumpPressed;
	private bool jumpHeld;
	private bool isGrounded;
	private bool facingRight = true;

	private float coyoteTimer;
	private float jumpBufferTimer;

	private void Awake()
	{
		rb = GetComponent<Rigidbody2D>();
		baseGravityScale = rb.gravityScale;
	}

	private void Update()
	{
		if (playerConfig == null)
		{
			return;
		}

		moveInput = Input.GetAxisRaw("Horizontal");
		jumpHeld = Input.GetButton("Jump");

		if (Input.GetButtonDown("Jump"))
		{
			jumpPressed = true;
		}

		isGrounded = IsGrounded();
		UpdateJumpTimers();
		TryJump();
		UpdateFacing();
	}

	private void FixedUpdate()
	{
		if (playerConfig == null)
		{
			return;
		}

		ApplyHorizontalMovement();
		ApplyBetterGravity();
	}

	private void UpdateJumpTimers()
	{
		if (isGrounded)
		{
			coyoteTimer = playerConfig.CoyoteTime;
		}
		else
		{
			coyoteTimer -= Time.deltaTime;
		}

		if (jumpPressed)
		{
			jumpBufferTimer = playerConfig.JumpBufferTime;
			jumpPressed = false;
		}
		else
		{
			jumpBufferTimer -= Time.deltaTime;
		}
	}

	private void TryJump()
	{
		if (jumpBufferTimer > 0f && coyoteTimer > 0f)
		{
			rb.velocity = new Vector2(rb.velocity.x, playerConfig.JumpForce);
			jumpBufferTimer = 0f;
			coyoteTimer = 0f;
		}
	}

	private void ApplyHorizontalMovement()
	{
		float targetSpeed = moveInput * playerConfig.MoveSpeed;
		float currentSpeed = rb.velocity.x;

		float acceleration;
		if (isGrounded)
		{
			acceleration = Mathf.Abs(targetSpeed) > 0.01f
				? playerConfig.GroundAcceleration
				: playerConfig.GroundDeceleration;
		}
		else
		{
			acceleration = playerConfig.AirAcceleration;
		}

		float newSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, acceleration * Time.fixedDeltaTime);
		rb.velocity = new Vector2(newSpeed, rb.velocity.y);
	}

	private void ApplyBetterGravity()
	{
		float gravityScale = baseGravityScale;

		if (rb.velocity.y < 0f)
		{
			gravityScale *= playerConfig.FallMultiplier;
		}
		else if (rb.velocity.y > 0f && !jumpHeld)
		{
			gravityScale *= playerConfig.LowJumpMultiplier;
		}

		rb.gravityScale = gravityScale;

		if (rb.velocity.y < -playerConfig.MaxFallSpeed)
		{
			rb.velocity = new Vector2(rb.velocity.x, -playerConfig.MaxFallSpeed);
		}
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

	private bool IsGrounded()
	{
		if (groundCheck == null)
		{
			return false;
		}

		return Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);
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
