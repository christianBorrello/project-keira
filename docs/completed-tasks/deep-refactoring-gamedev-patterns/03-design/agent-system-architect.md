# Agent Report: System Architect

## Task: Deep Refactoring GameDev Patterns
**Agent**: system-architect
**Date**: 2025-12-19
**Status**: Completed

---

## Executive Summary

Architecture design for decomposing PlayerController God Class (920 LOC) into 6 focused components following Single Responsibility Principle.

---

## Target Component Structure

### From Monolith to Components

| Component | Responsibility | Target LOC | Interfaces |
|-----------|---------------|------------|------------|
| **PlayerController** | Facade/Coordinator | ~200 | ICombatant (delegated) |
| **MovementController** | Locomotion, gravity, physics | ~250 | - |
| **CombatController** | Attacks, combos, weapons | ~200 | - |
| **HealthPoiseController** | HP, poise, damage reception | ~150 | IDamageableWithPoise |
| **AnimationController** | Animation parameters, visual feedback | ~150 | - |
| **LockOnController** | Target tracking, camera | ~100 | ILockOnTarget |
| **SmoothingState** | Consolidated smoothing variables | struct | - |

**Total**: 920 LOC → ~1050 LOC (overhead from separation, but each under 300 LOC)

---

## Component Diagram

```
                    ┌─────────────────────────┐
                    │   External Systems      │
                    │ (InputHandler, Combat,  │
                    │  LockOnSystem, etc.)    │
                    └───────────┬─────────────┘
                                │
                                ▼
                    ┌─────────────────────────┐
                    │   PlayerController      │
                    │      (FACADE)           │
                    │   ~200 LOC              │
                    │   Coordinates all       │
                    │   components            │
                    └───────────┬─────────────┘
                                │
        ┌───────────┬───────────┼───────────┬───────────┐
        │           │           │           │           │
        ▼           ▼           ▼           ▼           ▼
┌───────────┐ ┌───────────┐ ┌───────────┐ ┌───────────┐ ┌───────────┐
│ Movement  │ │  Combat   │ │ HealthPoise│ │ Animation │ │  LockOn   │
│ Controller│ │ Controller│ │ Controller │ │ Controller│ │ Controller│
│  ~250 LOC │ │  ~200 LOC │ │  ~150 LOC  │ │  ~150 LOC │ │  ~100 LOC │
│           │ │           │ │IDamageable │ │           │ │ILockOn    │
│FixedUpdate│ │  Update   │ │ WithPoise  │ │LateUpdate │ │Target     │
└─────┬─────┘ └─────┬─────┘ └─────┬─────┘ └─────┬─────┘ └─────┬─────┘
      │             │             │             │             │
      └─────────────┴─────────────┴──────┬──────┴─────────────┘
                                         │
                                         ▼
                               ┌─────────────────┐
                               │ SmoothingState  │
                               │    (struct)     │
                               │ Shared velocity │
                               │   data          │
                               └─────────────────┘
```

---

## Communication Patterns

### 1. Direct References (Tight Coupling - Same GameObject)
```csharp
// Components on same GameObject use direct references
public class MovementController : MonoBehaviour
{
    private AnimationController animController;

    private void Awake()
    {
        animController = GetComponent<AnimationController>();
    }
}
```

### 2. Events (Loose Coupling - Cross-System)
```csharp
// Events for notifications that don't need response
public class HealthPoiseController : MonoBehaviour
{
    public event Action<DamageInfo> OnDamageReceived;
    public event Action OnDeath;
    public event Action OnPoiseBreak;
}
```

### 3. Facade Pattern (External Access)
```csharp
// PlayerController provides stable API to external systems
public class PlayerController : MonoBehaviour
{
    // Delegated interfaces
    public void TakeDamage(DamageInfo info) => healthPoise.TakeDamage(info);
    public void SetLockOnTarget(Transform target) => lockOn.SetTarget(target);
}
```

---

## Update Strategy (Eliminating Update Soup)

### Current (Update Soup)
```csharp
// BAD: Mixed concerns in single Update
private void Update()
{
    UpdateStaminaRegen();           // Resource
    UpdatePoiseRegen();             // Resource
    ApplyGravity();                 // Physics
    UpdateLockedLocomotionLayer();  // Animation
}
```

### Target (Distributed Responsibility)
```csharp
// GOOD: Each component manages its own lifecycle

// MovementController.cs - Physics in FixedUpdate
private void FixedUpdate()
{
    ApplyGravity();
    ApplyMovement();
}

// HealthPoiseController.cs - Resources in Update
private void Update()
{
    UpdateStaminaRegen();
    UpdatePoiseRegen();
}

// AnimationController.cs - Animation in LateUpdate
private void LateUpdate()
{
    UpdateLockedLocomotionLayer();
    SyncAnimationParameters();
}
```

**Benefits**:
- Clear ownership of each operation
- Easier to profile and optimize
- Unity lifecycle respected (FixedUpdate for physics, LateUpdate for animation)

---

## SmoothingState Struct Design

```csharp
/// <summary>
/// Consolidates all smoothing/velocity variables into single struct.
/// Passed by reference between components to avoid allocations.
/// </summary>
[System.Serializable]
public struct SmoothingState
{
    // Movement smoothing
    public Vector3 currentVelocity;
    public Vector3 targetVelocity;
    public float velocitySmoothTime;

    // Rotation smoothing
    public float currentRotationVelocity;
    public float rotationSmoothTime;

    // Animation smoothing
    public float currentAnimSpeed;
    public float animSpeedSmoothVelocity;

    // Lock-on smoothing
    public float lockOnBlendVelocity;

    // Factory method for initialization
    public static SmoothingState CreateDefault()
    {
        return new SmoothingState
        {
            velocitySmoothTime = 0.1f,
            rotationSmoothTime = 0.12f,
            // ... other defaults
        };
    }
}
```

---

## Migration Path (Risk-Ordered)

### Phase 1: Extract SmoothingState (LOW RISK)
**Effort**: 2-3 hours
- Create SmoothingState struct
- Replace 15+ variables with single struct
- No behavioral changes

### Phase 2: Extract HealthPoiseController (LOW RISK)
**Effort**: 3-4 hours
- Extract health/poise logic
- Implement IDamageableWithPoise
- Wire up damage events

### Phase 3: Extract AnimationController (LOW-MEDIUM RISK)
**Effort**: 4-5 hours
- Extract animation parameter management
- Move to LateUpdate
- Handle animator parameter caching

### Phase 4: Extract CombatController (MEDIUM RISK)
**Effort**: 5-6 hours
- Extract attack logic
- Wire up to state machine
- Preserve combo system

### Phase 5: Extract LockOnController (MEDIUM RISK)
**Effort**: 3-4 hours
- Extract lock-on logic
- Implement ILockOnTarget
- Coordinate with camera system

### Phase 6: Extract MovementController (HIGH RISK)
**Effort**: 6-8 hours
- Extract core movement (most complex)
- Move to FixedUpdate
- Preserve input responsiveness

### Phase 7: Finalize PlayerController Facade (LOW RISK)
**Effort**: 2-3 hours
- Clean up remaining code
- Ensure stable external API
- Verify all delegations work

---

## Component Dependencies

```
MovementController
├── requires: SmoothingState, AnimationController
├── provides: movement state to StateMachine
└── update: FixedUpdate

CombatController
├── requires: AnimationController, HealthPoiseController
├── provides: attack execution
└── update: Update (frame-based)

HealthPoiseController
├── requires: AnimationController (damage feedback)
├── provides: IDamageableWithPoise implementation
└── update: Update (regen timers)

AnimationController
├── requires: Animator reference
├── provides: animation state to all components
└── update: LateUpdate

LockOnController
├── requires: MovementController (rotation target)
├── provides: ILockOnTarget implementation
└── update: Update
```

---

## Validation Checklist

- [ ] Each component under 300 LOC (AC-01)
- [ ] Unity editor compiles without errors (AC-04)
- [ ] All existing interfaces still work
- [ ] State machine transitions unchanged
- [ ] Input responsiveness unchanged
- [ ] No GC allocations in Update loops
- [ ] Gameplay feel identical to before

---

## Risks and Mitigations

| Risk | Mitigation |
|------|------------|
| Component reference null | Validate in Awake(), log errors |
| Event subscription leaks | Unsubscribe in OnDisable() |
| Timing issues (Update order) | Use Unity execution order or explicit dependency |
| State machine integration | Keep state machine reference in facade |

---

## Success Metrics

- **LOC per component**: Max 300 (from 920 monolith)
- **Update operations in facade**: 0 (from 4)
- **Smoothing variables**: 1 struct (from 15+ fields)
- **Compilation**: Zero errors
- **Gameplay**: Identical behavior
