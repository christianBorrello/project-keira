# Agent Report: Explore

## Task: Deep Refactoring GameDev Patterns
**Agent**: Explore
**Date**: 2025-12-19
**Status**: Completed

---

## Executive Summary

Project-Keira e' un gioco action souls-like in Unity con **~9,784 linee di codice C#** organizzate secondo un'architettura basata su **State Machine, Interfaces, e ScriptableObjects**.

---

## Project Structure

```
Assets/
├── _Scripts/                 (22 subdirectories, ~9500 LOC)
│   ├── Camera/              (CinemachineLockOnController, ThirdPersonCameraSystem)
│   ├── Combat/              (Combat system core)
│   │   ├── Data/           (CombatStats, DamageInfo, AttackData)
│   │   ├── Interfaces/     (ICombatant, IDamageable, ILockOnTarget)
│   │   ├── Adapters/       (HealthComponentAdapter)
│   │   └── Hitbox/         (Hitbox, Hurtbox, HitboxController)
│   ├── Enemies/             (AI system, 6 state implementations)
│   ├── Player/              (11 state implementations, PlayerController)
│   ├── Managers/            (ExampleGameManager, ExampleUnitManager)
│   ├── Scriptables/         (5 SO definitions)
│   ├── StateMachine/        (StateAttribute, reflection-based registration)
│   ├── Systems/             (AudioSystem, ResourceSystem)
│   ├── Units/               (UnitBase, HeroUnitBase, EnemyUnitBase)
│   └── Utilities/           (StaticInstance, Singleton, Helpers)
├── Systems/                 (CombatSystem, InputHandler, LockOnSystem, etc.)
├── Ilumisoft/              (Health System plugin - third party)
├── Data/                    (Game data assets)
├── Prefabs/                 (8 prefab directories)
└── Scenes/                  (8 scene files)
```

---

## Design Patterns Identified

### 1. State Machine Pattern (Reflection-Based)
- **PlayerStateMachine**: 11 states (Idle, Walk, Run, Sprint, LightAttack, HeavyAttack, Block, Parry, Dodge, Stagger, Death)
- **EnemyStateMachine**: 6 states (Idle, Alert, Chase, Attack, Stagger, Death)
- Registration via reflection (auto-discovery of derived classes)
- `StateAttribute` for configuration

### 2. Interface-Based Combat System
- `ICombatant`: Base combat entity interface
- `IDamageable`: Receive damage, healing, death
- `IDamageableWithPoise`: Poise system (Lies of P style)
- `ILockOnTarget`: Lock-on targeting
- `IStateWithInput`: Input handling in states
- `IInterruptibleState`: Interruptions (stagger, parry)

### 3. Singleton/StaticInstance Pattern
- `StaticInstance<T>`: Override instance, allows reset
- `Singleton<T>`: Destroy duplicates
- `PersistentSingleton<T>`: Survives scene loads
- Used by: CombatSystem, InputHandler, LockOnSystem, AudioSystem

### 4. Data-Driven Configuration
- `CombatStats` (struct): Immutable base configuration
- `CombatRuntimeData` (class): Mutable runtime state
- `PlayerConfigSO`, `EnemyDataSO`, `WeaponDataSO`: ScriptableObjects

### 5. Input Buffering System
- Buffered input queue (max 5 inputs)
- Buffer window: 0.15s
- Heavy attack hold detection (0.2s threshold)
- Action consumption pattern

---

## Code Smells Identified

### CRITICAL: GOD CLASS - PlayerController (920 LOC)
**Location**: `Assets/_Scripts/Player/PlayerController.cs`
**Violations**:
- Single Responsibility Principle violated
- Combines: Movement, Combat, Health, Animation, Lock-on
- 15+ smoothing velocity variables
- 4 operations in Update()

### HIGH: Update Soup
**Location**: `PlayerController.Update()`
```csharp
private void Update()
{
    UpdateStaminaRegen();           // Stamina system
    UpdatePoiseRegen();             // Poise system
    ApplyGravity();                 // Physics
    UpdateLockedLocomotionLayer();  // Animation
}
```

### HIGH: State Machine Duplication
**Locations**:
- `Assets/_Scripts/Player/PlayerStateMachine.cs` (239 LOC)
- `Assets/_Scripts/Enemies/EnemyStateMachine.cs` (230 LOC)
**Issue**: 90% code duplication

### MEDIUM: Tight Coupling AnimationEventBridge
**Location**: `Assets/Systems/AnimationEventBridge.cs`
**Issue**: Hardcoded dependency on PlayerController

### MEDIUM: Magic Numbers
**Location**: `PlayerController.cs`
```csharp
private const int LockedLocomotionLayerIndex = 1;
```

### LOW: UnitBase Incomplete
**Location**: `Assets/_Scripts/Units/UnitBase.cs`
**Issue**: Empty implementation, likely obsoleted by interfaces

---

## Metrics Summary

| Metric | Value |
|--------|-------|
| Total LOC | ~9,784 |
| C# Files | 67+ |
| MonoBehaviour classes | 12 |
| Interfaces | 6+ |
| ScriptableObjects | 5 |
| Player States | 11 |
| Enemy States | 6 |
| Largest class | 920 LOC (PlayerController) |

---

## Key Files for Refactoring

1. **PlayerController.cs** (920 LOC) - Primary refactoring target
2. **PlayerStateMachine.cs** (239 LOC) - State machine consolidation
3. **EnemyStateMachine.cs** (230 LOC) - State machine consolidation
4. **AnimationEventBridge.cs** - Decoupling needed
5. **UnitBase.cs** - Cleanup candidate

---

## Recommendations

1. **Decompose PlayerController** into:
   - `PlayerMovementSystem`
   - `PlayerCombatController`
   - `PlayerHealthManager`
   - `PlayerAnimationController`

2. **Create Generic State Machine**:
   - `BaseStateMachine<TEnum, TState>`
   - Inherit for Player and Enemy

3. **Extract Smoothing State**:
   - `SmoothingState` struct with all velocity fields

4. **Implement Update Orchestrator**:
   - Remove business logic from Update()
   - Use dedicated systems or event-driven updates
