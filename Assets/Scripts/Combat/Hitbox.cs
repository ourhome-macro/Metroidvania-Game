using System.Collections.Generic;
using UnityEngine;

public class Hitbox : MonoBehaviour
{
    [SerializeField] private LayerMask targetLayer;
    [SerializeField] private int damage = 10;
    [SerializeField] private Vector2 knockback = new Vector2(2f, 1f);

    private readonly HashSet<Collider2D> _hitTargets = new HashSet<Collider2D>();

    private void OnDisable()
    {
        _hitTargets.Clear();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (((1 << other.gameObject.layer) & targetLayer.value) == 0)
        {
            return;
        }

        if (!_hitTargets.Add(other))
        {
            return;
        }

        Hurtbox hurtbox = other.GetComponent<Hurtbox>();
        if (hurtbox == null)
        {
            hurtbox = other.GetComponentInParent<Hurtbox>();
        }

        if (hurtbox != null)
        {
            DamageInfo info = new DamageInfo(damage, knockback);
            hurtbox.ReceiveHit(info);
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        _hitTargets.Remove(other);
    }
}