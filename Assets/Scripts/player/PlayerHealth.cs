using System;
using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    [Serializable]
    public struct DamageRequest
    {
        public int Damage;
        public AttackData AttackData;
        public bool TriggerHitStun;

        public DamageRequest(int damage, AttackData attackData, bool triggerHitStun)
        {
            Damage = damage;
            AttackData = attackData;
            TriggerHitStun = triggerHitStun;
        }
    }

    [Header("Health")]
    [SerializeField] private int maxHealth = 100;

    private int currentHealth;
    private bool initialized;
    private bool isDead;

    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;
    public bool IsDead => isDead;

    // Return true to block this incoming damage.
    public event Func<DamageRequest, bool> OnBeforeTakeDamage;
    public event Action<DamageRequest> OnDamageTaken;
    public event Action OnDeath;

    private void Awake()
    {
        if (!initialized)
        {
            Initialize(maxHealth, true);
        }
    }

    public void Initialize(int desiredMaxHealth, bool resetCurrentHealth)
    {
        maxHealth = Mathf.Max(1, desiredMaxHealth);

        if (!initialized || resetCurrentHealth)
        {
            currentHealth = maxHealth;
            isDead = false;
        }
        else
        {
            currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
            if (currentHealth == 0)
            {
                isDead = true;
            }
        }

        initialized = true;
        GameEvents.HpChanged(currentHealth, maxHealth);
    }

    public bool TryTakeDamage(DamageRequest request, out bool becameDead)
    {
        becameDead = false;

        if (!initialized)
        {
            Initialize(maxHealth, true);
        }

        if (isDead || request.Damage <= 0)
        {
            return false;
        }

        if (ShouldBlockDamage(request))
        {
            return false;
        }

        currentHealth = Mathf.Max(0, currentHealth - request.Damage);
        GameEvents.HpChanged(currentHealth, maxHealth);
        OnDamageTaken?.Invoke(request);

        if (currentHealth <= 0)
        {
            isDead = true;
            becameDead = true;
            OnDeath?.Invoke();
        }

        return true;
    }

    private bool ShouldBlockDamage(DamageRequest request)
    {
        if (OnBeforeTakeDamage == null)
        {
            return false;
        }

        Delegate[] listeners = OnBeforeTakeDamage.GetInvocationList();
        for (int i = 0; i < listeners.Length; i++)
        {
            Func<DamageRequest, bool> callback = (Func<DamageRequest, bool>)listeners[i];
            bool blocked = false;
            try
            {
                blocked = callback.Invoke(request);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex, this);
            }

            if (blocked)
            {
                return true;
            }
        }

        return false;
    }
}

