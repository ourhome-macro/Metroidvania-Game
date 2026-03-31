using System;
using UnityEngine;

public class Health : MonoBehaviour, IDamageable
{
    [SerializeField] private int _maxHp = 100;
    [SerializeField] private int _currentHp = 100;

    public Action OnDeath;

    public float GetHpRatio => _maxHp <= 0 ? 0f : (float)_currentHp / _maxHp;

    private void Awake()
    {
        _maxHp = Mathf.Max(1, _maxHp);
        _currentHp = Mathf.Clamp(_currentHp, 0, _maxHp);
    }

    public void TakeDamage(DamageInfo info)
    {
        if (_currentHp <= 0)
        {
            return;
        }

        _currentHp = Mathf.Max(0, _currentHp - Mathf.Max(0, info.Damage));

        if (_currentHp <= 0)
        {
            OnDeath?.Invoke();
        }
    }

    public void Heal(int amount)
    {
        if (_currentHp <= 0)
        {
            return;
        }

        _currentHp = Mathf.Clamp(_currentHp + Mathf.Max(0, amount), 0, _maxHp);
    }
}