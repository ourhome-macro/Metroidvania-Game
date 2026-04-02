# Player 模块技术文档

## 范围

本文档描述 `Assets/Player` 目录下脚本的职责、运行流程、对外接口与挂载约束：

- `playecontrollerr.cs`（移动控制）
- `playercombat.cs`（攻击与受击表现）
- `playerstas.cs`（运行时状态面板）

> 说明：当前文件名存在拼写不规范（`playecontrollerr` / `playerstas`），功能不受影响，但建议后续统一命名规范。

---

## 1. `playecontrollerr`（玩家移动控制）

### 职责

- 读取输入并驱动 `Rigidbody2D` 水平移动
- 处理跳跃手感：土狼时间、跳跃缓冲、可变跳高
- 处理角色朝向翻转
- 使用 `groundCheck` + `LayerMask` 判定是否在地面

### 依赖

- 组件：`Rigidbody2D`（`[RequireComponent]`）
- 数据：`PlayerConfigSO playerConfig`
- 场景引用：`Transform groundCheck`、`LayerMask groundLayer`

### 核心流程

1. `Update`
   - 读取 `Horizontal` 和 `Jump`
   - 更新地面状态
   - 更新 `coyoteTimer` / `jumpBufferTimer`
   - 在窗口期满足条件时执行起跳
   - 按输入方向更新朝向
2. `FixedUpdate`
   - 按地面/空中参数加减速到目标速度
   - 按上升/下落阶段应用重力倍率
   - 限制最大下落速度

### 关键参数来源（`PlayerConfigSO`）

- 移动：`MoveSpeed`、`GroundAcceleration`、`GroundDeceleration`、`AirAcceleration`
- 跳跃：`JumpForce`、`CoyoteTime`、`JumpBufferTime`
- 重力手感：`FallMultiplier`、`LowJumpMultiplier`、`MaxFallSpeed`

---

## 2. `PlayerCombat`（攻击与受击表现）

### 职责

- 监听攻击输入（`J` / 鼠标左键）
- 执行一次完整攻击协程：拉伸、生成 hitbox、顿帧、回收
- 提供受击入口：`TakeDamage(Vector2 attackerPosition)`
- 处理无敌闪烁、受击压扁、击退

### 依赖

- 组件：`SpriteRenderer`、`Rigidbody2D`（`[RequireComponent]`）
- 资源：`GameObject hitboxPrefab`
- 场景引用：`Transform attackPoint`

### 攻击流程（`PerformAttack`）

1. 标记 `isAttacking = true`
2. 玩家 X 轴拉伸（攻击前探）
3. 在 `attackPoint` 实例化 `hitboxPrefab`
4. 执行 HitStop：`Time.timeScale = 0` 持续 `0.05s`（Realtime）
5. 等待 `attackDuration`
6. 销毁本次 hitbox
7. 恢复原始缩放，结束攻击状态

### 受击流程（`TakeDamage` + `InvincibilityFlash`）

1. 若 `isInvincible` 为真，忽略本次受击
2. 计算击退方向（攻击者 -> 玩家 的反方向）并施加 `Impulse`
3. 玩家受击压扁（`x0.7 / y1.3`）
4. 开启闪烁协程，持续 `invincibleDuration`
5. 结束后恢复颜色、缩放并退出无敌

### 公开可调参数

- `attackDuration`
- `knockbackForce`
- `invincibleDuration`

---

## 3. `PlayerStats`（玩家运行时状态面板）

### 职责

- 管理核心运行时数值：`MaxHealth`、`CurrentHealth`、`AttackPower`
- 对外仅提供只读访问器，禁止外部直接修改字段
- 通过安全方法修改数值并做边界保护
- 在血量变化时发出事件供 UI/系统订阅

### 对外接口

- 初始化：`Initialize(PlayerConfigSO config)`
- 扣血：`TakeDamage(float amount)`
- 回血：`Heal(float amount)`
- 攻击力修改（安全接口）：`SetAttackPower(float value)`
- 事件：`OnHealthChanged(float current, float max)`

### 数值约束

- `MaxHealth >= 1`
- `0 <= CurrentHealth <= MaxHealth`
- `AttackPower >= 0`

### 初始化策略

- 若设置了 `defaultConfig`，在 `Awake` 自动初始化
- 未初始化时，首次调用修改接口会进行兜底初始化

---

## 4. Player 目录内部协作关系

```text
playecontrollerr
  └─ 只负责移动/跳跃，不处理血量和伤害

PlayerCombat
  ├─ 负责攻击输入与攻击判定体生成
  └─ 负责受击表现（击退/闪烁/无敌）

PlayerStats
  ├─ 负责运行时数值状态
  └─ 通过 OnHealthChanged 驱动 UI 更新
```

设计上三者是解耦的：

- `Controller` 管“怎么动”
- `Combat` 管“怎么打/怎么表现受击”
- `Stats` 管“数值状态与事件”

---

## 5. 与 Combat 通用层（`Assets/Scripts/Combat`）关系

当前项目中还存在一套通用受击链路：

`Hitbox -> Hurtbox -> IDamageable.TakeDamage(DamageInfo) -> Health`

`PlayerCombat.TakeDamage(Vector2)` 属于“表现层受击入口”；
`Health/IDamageable` 属于“通用数值受击入口”。

建议后续通过桥接脚本统一两条链路（例如在玩家对象上把 `DamageInfo` 转发到 `PlayerStats` 与 `PlayerCombat`），避免重复维护。

---

## 6. 挂载与场景配置建议

### 玩家根物体

- `Rigidbody2D`
- `SpriteRenderer`
- `playecontrollerr`
- `PlayerCombat`
- `PlayerStats`

### 子物体

- `groundCheck`：位于脚底
- `attackPoint`：位于角色前方

### Inspector 必填

- `playecontrollerr.playerConfig` -> `PlayerConfigSO`
- `playecontrollerr.groundCheck` / `groundLayer`
- `PlayerCombat.hitboxPrefab` / `attackPoint`
- `PlayerStats.defaultConfig`（可选）

---

## 7. 已知风险与改进建议

1. **命名规范**
   - 建议重命名：
     - `playecontrollerr` -> `PlayerController`
     - `playerstas.cs` -> `PlayerStats.cs`
2. **TimeScale 冲突风险**
   - `PlayerCombat` 在 HitStop 时直接改 `Time.timeScale`，若后续有全局暂停系统，建议统一由时间管理器托管。
3. **战斗链路统一**
   - 推荐把 `PlayerCombat.TakeDamage(Vector2)` 与 `IDamageable.TakeDamage(DamageInfo)` 通过桥接合并到单入口。
4. **测试建议**
   - 白盒阶段重点验证：土狼时间、跳跃缓冲、无敌帧期间重复受击过滤、HitStop 与暂停系统兼容性。
