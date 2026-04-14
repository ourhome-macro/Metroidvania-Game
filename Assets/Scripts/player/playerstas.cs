using System;
using UnityEngine;

/// <summary>
/// 玩家运行时状态面板：只负责“数值状态”（血量、攻击力）
/// 挂在玩家根物体上
/// </summary>
public class PlayerStats : MonoBehaviour
{
    [Header("可选：用于自动初始化（也可在外部手动调用 Initialize）")]
    [SerializeField] private PlayerConfigSO defaultConfig;

    [Header("调试只读（运行时观察）")]
    [SerializeField] private float maxHealth;
    [SerializeField] private float currentHealth;
    [SerializeField] private float attackPower = 10f; // 当前配置里没有攻击力字段，先给默认值

    // 只读访问：外部脚本只能读取，不能直接改写
    public float MaxHealth => maxHealth;
    public float CurrentHealth => currentHealth;
    public float AttackPower => attackPower;

    // 事件机制：血量变化时主动通知 UI/系统，避免每帧轮询
    // 参数：当前血量、最大血量
    public event Action<float, float> OnHealthChanged;

    private bool initialized;

    private void Awake()
    {
        if (defaultConfig != null)
        {
            Initialize(defaultConfig);
        }
    }

    /// <summary>
    /// 从配置初始化运行时状态
    /// </summary>
    public void Initialize(PlayerConfigSO config)
    {
        if (config == null)
        {
            Debug.LogError("[PlayerStats] Initialize 失败：config 为 null。");
            return;
        }

        maxHealth = Mathf.Max(1f, config.MaxHp);
        currentHealth = maxHealth;

        // PlayerConfigSO 目前没有 AttackPower 字段，这里保留现有 attackPower（可在 Inspector 调）
        attackPower = Mathf.Max(0f, attackPower);

        initialized = true;
        RaiseHealthChanged();
    }

    /// <summary>
    /// 扣血：内部做边界保护（0 ~ MaxHealth）
    /// </summary>
    public void TakeDamage(float amount)
    {
        EnsureInitialized();

        if (amount <= 0f || currentHealth <= 0f)
            return;

        float old = currentHealth;
        currentHealth = Mathf.Clamp(currentHealth - amount, 0f, maxHealth);

        if (!Mathf.Approximately(old, currentHealth))
        {
            RaiseHealthChanged();
        }
    }

    /// <summary>
    /// 回血：内部做边界保护（0 ~ MaxHealth）
    /// </summary>
    public void Heal(float amount)
    {
        EnsureInitialized();

        if (amount <= 0f || currentHealth <= 0f)
            return;

        float old = currentHealth;
        currentHealth = Mathf.Clamp(currentHealth + amount, 0f, maxHealth);

        if (!Mathf.Approximately(old, currentHealth))
        {
            RaiseHealthChanged();
        }
    }

    /// <summary>
    /// 安全修改攻击力接口（可选）
    /// </summary>
    public void SetAttackPower(float value)
    {
        EnsureInitialized();
        attackPower = Mathf.Max(0f, value);
    }

    private void RaiseHealthChanged()
    {
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    private void EnsureInitialized()
    {
        if (initialized) return;

        // 如果没显式初始化，给一个兜底值，避免空状态导致异常
        maxHealth = Mathf.Max(1f, maxHealth <= 0f ? 100f : maxHealth);
        currentHealth = Mathf.Clamp(currentHealth <= 0f ? maxHealth : currentHealth, 0f, maxHealth);
        attackPower = Mathf.Max(0f, attackPower);
        initialized = true;
        RaiseHealthChanged();
    }
}