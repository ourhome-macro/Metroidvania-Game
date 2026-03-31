# Combat 系统（数值与碰撞）

## 文件清单

- `Assets/Scripts/Combat/IDamageable.cs`
- `Assets/Scripts/Combat/DamageInfo.cs`
- `Assets/Scripts/Combat/Health.cs`
- `Assets/Scripts/Combat/Hurtbox.cs`
- `Assets/Scripts/Combat/Hitbox.cs`

## 结构说明

### IDamageable

- 统一受伤接口：`void TakeDamage(DamageInfo info)`

### DamageInfo

- 伤害数据结构体
- 字段：`int Damage`、`Vector2 Knockback`

### Health

- `MonoBehaviour` + `IDamageable`
- 内部状态：`_maxHp`、`_currentHp`
- 死亡事件：`public Action OnDeath`
- 方法：`TakeDamage(DamageInfo info)`、`Heal(int amount)`
- 属性：`public float GetHpRatio`
- 仅处理血量，不含动画与音频调用

### Hurtbox

- 在 `Awake` 中读取父物体 `IDamageable`
- 对外暴露 `ReceiveHit(DamageInfo info)`，转发到 `TakeDamage`

### Hitbox

- `OnTriggerEnter2D` 时按 `targetLayer` 过滤目标
- 命中后寻找目标 `Hurtbox` 并传入 `DamageInfo`
- 使用 `HashSet<Collider2D>` 防止单次攻击重复判定
- `OnTriggerExit2D` 时移除目标
- 不含特效与音频调用

在后续编写具体的“玩家实体”或“敌人实体”脚本时，你需要写一行简单的桥接代码把它们连起来，例如：


// 在玩家实体初始化时
health.OnDeath += () => GameEvents.PlayerDeath();