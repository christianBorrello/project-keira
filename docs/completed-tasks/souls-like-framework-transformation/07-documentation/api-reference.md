# Souls-Like Combat Framework - API Reference

**Version**: 1.0.0
**Last Updated**: 2025-12-22

---

## Architecture Overview

```
┌─────────────────────────────────────────────────────────┐
│                    COMBAT SYSTEM                         │
│  CombatSystem (Singleton) - Central damage resolution   │
└─────────────────────────────────────────────────────────┘
         │                    │                    │
         ▼                    ▼                    ▼
┌─────────────┐      ┌─────────────┐      ┌─────────────┐
│ StaminaSystem│      │ PoiseSystem │      │ LockOnSystem│
│  (Singleton) │      │ (Singleton) │      │ (Singleton) │
└─────────────┘      └─────────────┘      └─────────────┘
         │
         ▼
┌─────────────────────────────────────────────────────────┐
│                   PLAYER / ENEMY                         │
│  PlayerController ◄─► PlayerStateMachine                │
│  EnemyController  ◄─► EnemyStateMachine                 │
└─────────────────────────────────────────────────────────┘
         │
         ▼
┌─────────────────────────────────────────────────────────┐
│                   HITBOX SYSTEM                          │
│  Hitbox ──► Hurtbox ──► DamageInfo ──► CombatSystem    │
└─────────────────────────────────────────────────────────┘
```

---

## CombatSystem

Central coordinator for all combat interactions.

### Namespace
```csharp
using Systems;
```

### Events

| Event | Signature | Description |
|-------|-----------|-------------|
| `OnDamageDealt` | `Action<ICombatant, ICombatant, DamageInfo, DamageResult>` | Fired when damage is applied |
| `OnCombatantDeath` | `Action<ICombatant>` | Fired when a combatant dies |
| `OnParryOccurred` | `Action<ICombatant, bool>` | Fired on parry (bool = isPerfect) |
| `OnPoiseBreak` | `Action<ICombatant>` | Fired when poise breaks |

### Public Methods

```csharp
// Registration
void RegisterCombatant(ICombatant combatant)
void UnregisterCombatant(ICombatant combatant)

// Damage Processing
DamageResult ProcessDamage(ICombatant attacker, IDamageable target, DamageInfo damage)

// Hitstop
void TriggerHitstop(float duration)

// Queries
ICombatant GetCombatantById(int id)
bool IsCombatantRegistered(int id)
```

### Usage Example
```csharp
// Register on spawn
CombatSystem.Instance.RegisterCombatant(this);

// Process damage from hitbox
var result = CombatSystem.Instance.ProcessDamage(attacker, target, damageInfo);
if (result.WasKillingBlow)
{
    // Handle death
}
```

---

## StaminaSystem

Manages stamina for all combatants.

### Public Methods

```csharp
// Stamina Operations
bool TryConsumeStamina(ICombatant combatant, float amount)
void AddStamina(ICombatant combatant, float amount)
float GetStamina(ICombatant combatant)
float GetMaxStamina(ICombatant combatant)
bool HasEnoughStamina(ICombatant combatant, float amount)

// Regeneration Control
void PauseRegen(ICombatant combatant, float duration)
void ResumeRegen(ICombatant combatant)
```

### Events

| Event | Signature | Description |
|-------|-----------|-------------|
| `OnStaminaChanged` | `Action<ICombatant, float, float>` | (combatant, current, max) |
| `OnStaminaDepleted` | `Action<ICombatant>` | Fired when stamina hits 0 |

### Usage Example
```csharp
// Before attack
if (StaminaSystem.Instance.TryConsumeStamina(this, attackCost))
{
    // Perform attack
    StaminaSystem.Instance.PauseRegen(this, 1.5f);
}
```

---

## PoiseSystem

Handles poise damage and stagger mechanics (Lies of P style).

### Public Methods

```csharp
// Poise Operations
void ApplyPoiseDamage(IDamageableWithPoise target, float poiseDamage)
void ResetPoise(IDamageableWithPoise target)
float GetCurrentPoise(IDamageableWithPoise target)
bool IsPoisebroken(IDamageableWithPoise target)

// Configuration
void SetPoiseRegenDelay(float delay)
void SetPoiseRegenRate(float rate)
```

### Events

| Event | Signature | Description |
|-------|-----------|-------------|
| `OnPoiseBreak` | `Action<IDamageableWithPoise>` | Fired when poise reaches 0 |
| `OnPoiseReset` | `Action<IDamageableWithPoise>` | Fired when poise fully regenerates |

### Poise Mechanics
- Poise is **accumulative** (Lies of P style)
- Damage accumulates until threshold reached
- Breaking poise causes stagger state
- Poise regenerates after delay when not taking damage

---

## LockOnSystem

Camera and targeting system for combat focus.

### Public Methods

```csharp
// Targeting
void ToggleLockOn()
void LockOnToTarget(ILockOnTarget target)
void ReleaseLockOn()
void CycleTarget(int direction) // -1 = left, 1 = right

// Queries
bool IsLockedOn { get; }
ILockOnTarget CurrentTarget { get; }
Transform GetLockOnPoint()

// Target Validation
bool IsValidTarget(ILockOnTarget target)
List<ILockOnTarget> GetTargetsInRange()
```

### Events

| Event | Signature | Description |
|-------|-----------|-------------|
| `OnTargetAcquired` | `Action<ILockOnTarget>` | New target locked |
| `OnTargetLost` | `Action<ILockOnTarget>` | Target no longer valid |
| `OnLockOnDisabled` | `Action` | Lock-on manually released |

---

## InputHandler

Manages input buffering and action queuing.

### Input Buffer
- **Buffer Window**: 150ms (configurable)
- **Queue System**: Stores inputs during animations
- **Priority**: Most recent input takes precedence

### Public Properties

```csharp
Vector2 MoveInput { get; }
bool IsSprintHeld { get; }
bool IsWalkHeld { get; }
```

### Public Methods

```csharp
// Input Queries
bool WasAttackPressed()
bool WasHeavyAttackPressed()
bool WasParryPressed()
bool WasDodgePressed()
bool WasLockOnPressed()

// Buffer Management
void ConsumeInput(InputAction action)
void ClearBuffer()
InputSnapshot CreateSnapshot()
```

### Input Bindings

| Action | Keyboard | Gamepad |
|--------|----------|---------|
| Move | WASD | Left Stick |
| Light Attack | LMB | Square/X |
| Heavy Attack | RMB | R2/RT |
| Parry | Q | L1/LB |
| Dodge | Space | Circle/B |
| Lock-On | Tab | R3 |
| Sprint | Shift | L3 |

---

## Interfaces

### ICombatant
```csharp
public interface ICombatant
{
    int CombatantId { get; }
    string CombatantName { get; }
    CombatStats Stats { get; }
    bool IsAlive { get; }
    Transform Transform { get; }
}
```

### IDamageable
```csharp
public interface IDamageable
{
    void TakeDamage(DamageInfo damage);
    bool CanTakeDamage { get; }
}
```

### IDamageableWithPoise
```csharp
public interface IDamageableWithPoise : IDamageable
{
    float CurrentPoise { get; }
    float MaxPoise { get; }
    void TakePoiseDamage(float amount);
    void OnPoiseBreak();
}
```

### ILockOnTarget
```csharp
public interface ILockOnTarget
{
    Transform LockOnPoint { get; }
    bool IsValidTarget { get; }
    float TargetPriority { get; }
}
```

---

## Data Structures

### DamageInfo
```csharp
public struct DamageInfo
{
    public float FinalDamage;
    public float PoiseDamage;
    public DamageType Type;
    public Vector3 HitPoint;
    public Vector3 HitDirection;
    public ICombatant Source;
    public AttackData AttackData;
}
```

### DamageResult
```csharp
public struct DamageResult
{
    public float DamageDealt;
    public bool WasBlocked;
    public bool WasParried;
    public bool IsPerfectParry;
    public bool CausedPoiseBreak;
    public bool WasKillingBlow;
}
```

### AttackData
```csharp
public struct AttackData
{
    public float DamageMultiplier;
    public float PoiseDamage;
    public float StaminaCost;
    public float AnimationDuration;
    public float HitboxActiveStart;
    public float HitboxActiveEnd;
    public bool CanBeParried;
}
```

---

## ScriptableObjects

### CombatConfigSO
Global combat settings.
```csharp
float ParryWindowDuration = 0.2f;
float PerfectParryWindow = 0.1f;
float HitstopDuration = 0.05f;
float PoiseRegenDelay = 3f;
float PoiseRegenRate = 10f;
```

### WeaponDataSO
Weapon configuration with combo support.
```csharp
string WeaponName;
float BaseDamage;
float AttackSpeed;
AttackData[] LightCombo;
AttackData HeavyAttack;
```

### PlayerConfigSO
Player stats and tuning.
```csharp
float MaxHealth = 100f;
float MaxStamina = 100f;
float MaxPoise = 100f;
float BaseDefense = 10f;
```

### EnemyDataSO
Enemy configuration with AI settings.
```csharp
float MaxHealth;
float MaxPoise;
float AttackRange;
float DetectionRange;
float ChaseSpeed;
```

---

## State Machine

### Player States

| State | Entry Condition | Exit Condition |
|-------|-----------------|----------------|
| Idle | No input | Any action input |
| Walk | Move input | Sprint/Stop/Action |
| Sprint | Shift + Move | Release/Action |
| LightAttack | Attack pressed | Animation complete |
| HeavyAttack | Heavy pressed | Animation complete |
| Parry | Parry pressed | Window expires |
| Dodge | Dodge pressed | Animation complete |
| Stagger | Poise broken | Recovery complete |
| Death | Health <= 0 | (terminal) |

### Enemy States

| State | Behavior | Transition |
|-------|----------|------------|
| Idle | Patrol/Wait | Player detected → Alert |
| Alert | Face player | In range → Chase |
| Chase | Move to player | Attack range → Attack |
| Attack | Execute attack | Complete → Chase/Idle |
| Stagger | Recovery animation | Complete → Chase |
| Death | Death handling | (terminal) |

---

## Integration with KCC

The souls-like framework integrates with the KCC movement system:

```csharp
// In PlayerController
MovementController.ApplyMovement(input, mode);

// For knockback (via ExternalForces)
MovementController.ExternalForces.AddKnockback(direction, distance);

// Cancel turn-in-place during combat
MovementController.CancelTurnInPlace();
```

---

## Quick Start

### 1. Scene Setup
```
1. Create test scene
2. Add ground plane with collider
3. Configure lighting
4. Bake NavMesh
```

### 2. Player Setup
```
1. Create Player prefab
2. Add components: PlayerController, MovementController, PlayerStateMachine
3. Configure Animator with combat states
4. Add Hitbox/Hurtbox colliders
```

### 3. Enemy Setup
```
1. Create Enemy prefab
2. Add components: EnemyController, NavMeshAgent, EnemyStateMachine
3. Configure Animator
4. Add Hitbox/Hurtbox colliders
5. Assign EnemyDataSO
```

### 4. Test Combat
```
1. Enter Play mode
2. Use Tab to lock on
3. LMB for light attack
4. Q for parry (200ms window)
5. Space for dodge
```

