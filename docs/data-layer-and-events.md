# Metroidvania Lite 数据层与全局事件总线

## 目录结构

- `Assets/Scripts/Data/PlayerConfigSO.cs`
- `Assets/Scripts/Data/EnemyConfigSO.cs`
- `Assets/Scripts/Data/BossConfigSO.cs`
- `Assets/Scripts/Core/GameEvents.cs`

## 数据层配置（ScriptableObject）

### PlayerConfigSO

- 字段：`moveSpeed`、`jumpForce`、`maxHp`、`attackDuration`、`attackKnockback`
- 约束：全部为 `[SerializeField] private`
- 访问：通过只读属性公开
- 资源创建菜单：`Metroidvania Lite/Data/Player Config`

### EnemyConfigSO

- 字段：`moveSpeed`、`chaseRange`、`attackRange`、`damage`、`attackCooldown`
- 约束：全部为 `[SerializeField] private`
- 访问：通过只读属性公开
- 资源创建菜单：`Metroidvania Lite/Data/Enemy Config`

### BossConfigSO

- 字段：`moveSpeed`、`phase2HpThreshold`、`skillCooldown`、`projectileCount`
- 约束：全部为 `[SerializeField] private`
- 访问：通过只读属性公开
- 资源创建菜单：`Metroidvania Lite/Data/Boss Config`

## 全局事件总线（强类型）

`GameEvents` 使用纯静态类实现，不继承 `MonoBehaviour`，按业务定义独立事件与触发方法：

- 生命值变化：`OnHpChanged(int currentHp, int maxHp)`
- 角色死亡：`OnPlayerDeath()`、`OnEnemyDeath(GameObject enemy)`、`OnBossDeath()`
- 精英怪击杀：`OnEliteKilled()`
- 场景与交互：`OnCheckpointReached(Vector3 spawnPos)`、`OnDoorInteracted(string doorId)`
- 游戏流程：`OnGamePaused(bool isPaused)`

## 使用方式

- 订阅：在业务脚本中订阅对应强类型事件
- 触发：通过 `GameEvents` 的同名业务触发方法调用
- 取消订阅：在生命周期结束时取消订阅，避免重复监听