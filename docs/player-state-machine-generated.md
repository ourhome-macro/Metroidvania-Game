# Player State Machine (Generated)

`PlayerStateMachine` lives at `Assets/Scripts/player/PlayerStateMachine.cs`.

## States

- `Idle`
- `Run`
- `Jump`
- `Fall`
- `Defend`
- `Attack`
- `Skip`
- `Hit`
- `Dead`

## Evaluation Priority (high -> low)

1. `Dead`
2. `Hit`
3. `Skip`
4. `Attack`
5. `Defend`
6. `Jump` / `Fall`
7. `Run`
8. `Idle`

## Transition Source

- Combat states come from `PlayerCombat` flags:
  - `IsDead`
  - `IsHitStunned`
  - `IsSkipping`
  - `IsAttacking`
  - `IsDefending`
- Locomotion states come from `PlayerController2D`:
  - `IsGrounded`
  - `Velocity`
  - `MoveInput`

## Diagram

```mermaid
stateDiagram-v2
    [*] --> Idle

    Idle --> Run
    Run --> Idle

    Idle --> Jump
    Run --> Jump
    Jump --> Fall
    Fall --> Idle
    Fall --> Run

    Idle --> Defend
    Run --> Defend
    Jump --> Defend
    Fall --> Defend
    Defend --> Idle
    Defend --> Run

    Idle --> Attack
    Run --> Attack
    Jump --> Attack
    Fall --> Attack
    Defend --> Attack
    Attack --> Idle
    Attack --> Run
    Attack --> Jump
    Attack --> Fall

    Idle --> Skip
    Run --> Skip
    Jump --> Skip
    Fall --> Skip
    Defend --> Skip
    Skip --> Idle
    Skip --> Run
    Skip --> Jump
    Skip --> Fall

    Idle --> Hit
    Run --> Hit
    Jump --> Hit
    Fall --> Hit
    Defend --> Hit
    Attack --> Hit
    Skip --> Hit
    Hit --> Idle
    Hit --> Run
    Hit --> Jump
    Hit --> Fall

    Idle --> Dead
    Run --> Dead
    Jump --> Dead
    Fall --> Dead
    Defend --> Dead
    Attack --> Dead
    Skip --> Dead
    Hit --> Dead
```
