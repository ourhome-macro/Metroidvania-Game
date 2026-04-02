using System.Collections;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer), typeof(Rigidbody2D))]
public class PlayerCombat : MonoBehaviour
{
	public GameObject hitboxPrefab;
	public Transform attackPoint;
	public float attackDuration = 0.1f;
	public float knockbackForce = 10f;
	public float invincibleDuration = 1.5f;

	private SpriteRenderer sr;
	private Color originalColor;
	private bool isInvincible = false;

	private Rigidbody2D rb;
	private Vector3 originalScale;
	private bool isAttacking;
	private Coroutine attackRoutine;

	private void Start()
	{
		sr = GetComponent<SpriteRenderer>();
		rb = GetComponent<Rigidbody2D>();
		originalColor = sr.color;
		originalScale = transform.localScale;
	}

	private void Update()
	{
		if (isAttacking)
		{
			return;
		}

		if (Input.GetKeyDown(KeyCode.J) || Input.GetMouseButtonDown(0))
		{
			attackRoutine = StartCoroutine(PerformAttack());
		}
	}

	private IEnumerator PerformAttack()
	{
		isAttacking = true;

		transform.localScale = new Vector3(originalScale.x * 1.5f, originalScale.y, originalScale.z);

		GameObject spawnedHitbox = null;
		if (hitboxPrefab != null && attackPoint != null)
		{
			spawnedHitbox = Instantiate(hitboxPrefab, attackPoint.position, Quaternion.identity);

			float facing = Mathf.Sign(transform.localScale.x);
			spawnedHitbox.transform.localScale = new Vector3(
				Mathf.Abs(spawnedHitbox.transform.localScale.x) * facing,
				spawnedHitbox.transform.localScale.y,
				spawnedHitbox.transform.localScale.z
			);
		}

		float previousTimeScale = Time.timeScale;
		Time.timeScale = 0f;
		yield return new WaitForSecondsRealtime(0.05f);
		Time.timeScale = previousTimeScale <= 0f ? 1f : previousTimeScale;

		yield return new WaitForSeconds(attackDuration);

		if (spawnedHitbox != null)
		{
			Destroy(spawnedHitbox);
		}

		transform.localScale = originalScale;
		isAttacking = false;
		attackRoutine = null;
	}

	public void TakeDamage(Vector2 attackerPosition)
	{
		if (isInvincible)
		{
			return;
		}

		isInvincible = true;

		Vector2 knockbackDirection = ((Vector2)transform.position - attackerPosition).normalized;
		if (knockbackDirection.sqrMagnitude < 0.0001f)
		{
			knockbackDirection = new Vector2(transform.localScale.x >= 0f ? 1f : -1f, 0.25f).normalized;
		}

		rb.velocity = Vector2.zero;
		rb.AddForce(knockbackDirection * knockbackForce, ForceMode2D.Impulse);

		transform.localScale = new Vector3(originalScale.x * 0.7f, originalScale.y * 1.3f, originalScale.z);

		StartCoroutine(InvincibilityFlash());
	}

	private IEnumerator InvincibilityFlash()
	{
		float elapsed = 0f;
		bool toggle = false;

		while (elapsed < invincibleDuration)
		{
			toggle = !toggle;
			sr.color = toggle
				? new Color(1f, 0.25f, 0.25f, 1f)
				: new Color(originalColor.r, originalColor.g, originalColor.b, 0.25f);

			yield return new WaitForSeconds(0.1f);
			elapsed += 0.1f;
		}

		sr.color = originalColor;
		transform.localScale = originalScale;
		isInvincible = false;
	}

	private void OnDisable()
	{
		if (attackRoutine != null)
		{
			StopCoroutine(attackRoutine);
			attackRoutine = null;
		}

		Time.timeScale = 1f;
		isAttacking = false;
		isInvincible = false;

		if (sr != null)
		{
			sr.color = originalColor;
		}

		transform.localScale = originalScale == Vector3.zero ? transform.localScale : originalScale;
	}
}
