using UnityEngine;

public class Hurtbox : MonoBehaviour
{
    private IDamageable _damageable;

    private void Awake()
    {
        _damageable = GetComponentInParent<IDamageable>();
    }

    public void ReceiveHit(DamageInfo info)
    {
        _damageable?.TakeDamage(info);
    }
}