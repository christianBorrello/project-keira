# Phase 3: Design - Target Architecture

## Task: Deep Refactoring GameDev Patterns
**Status**: Completed
**Depth**: `--ultrathink`
**Date**: 2025-12-19

---

## Executive Summary

This document defines the target architecture for refactoring Project-Keira following GAMEDEV_CODING_PATTERNS.md guidelines. The design addresses three major code smells:

1. **GOD CLASS**: PlayerController (920 LOC) → 6 focused components (~150-250 LOC each)
2. **State Machine Duplication**: 90% → 0% via generic BaseStateMachine
3. **Update Soup**: 4 operations → distributed to appropriate lifecycle methods

---

## Architecture Overview

```
┌──────────────────────────────────────────────────────────────────────────┐
│                         TARGET ARCHITECTURE                               │
├──────────────────────────────────────────────────────────────────────────┤
│                                                                          │
│   ┌─────────────────────────────────────────────────────────────────┐    │
│   │                    EXTERNAL SYSTEMS                              │    │
│   │  InputHandler │ CombatSystem │ LockOnSystem │ AudioSystem       │    │
│   └───────────────────────────────┬─────────────────────────────────┘    │
│                                   │                                       │
│                                   ▼                                       │
│   ┌─────────────────────────────────────────────────────────────────┐    │
│   │                   PlayerController (FACADE)                      │    │
│   │                        ~200 LOC                                  │    │
│   │  • Coordinates components    • Exposes stable API               │    │
│   │  • Implements ICombatant     • Delegates to specialists         │    │
│   └───────────────────────────────┬─────────────────────────────────┘    │
│                                   │                                       │
│     ┌──────────┬──────────┬───────┴───────┬──────────┬──────────┐        │
│     │          │          │               │          │          │        │
│     ▼          ▼          ▼               ▼          ▼          ▼        │
│  ┌──────┐  ┌──────┐  ┌──────────┐  ┌──────────┐  ┌──────┐  ┌────────┐   │
│  │Move- │  │Combat│  │HealthPoise│  │Animation │  │LockOn│  │Smoothing│  │
│  │ment  │  │Ctrl  │  │Controller │  │Controller│  │ Ctrl │  │ State  │   │
│  │~250  │  │~200  │  │  ~150     │  │  ~150    │  │~100  │  │(struct)│   │
│  │LOC   │  │LOC   │  │  LOC      │  │  LOC     │  │LOC   │  │        │   │
│  │      │  │      │  │IDamageable│  │          │  │ILock │  │        │   │
│  │Fixed │  │Update│  │WithPoise  │  │Late      │  │OnTgt │  │        │   │
│  │Update│  │      │  │           │  │Update    │  │      │  │        │   │
│  └──────┘  └──────┘  └───────────┘  └──────────┘  └──────┘  └────────┘   │
│                                                                          │
│   ┌─────────────────────────────────────────────────────────────────┐    │
│   │                    STATE MACHINE LAYER                           │    │
│   │                                                                  │    │
│   │  BaseStateMachine<TContext, TStateEnum, TState>  (~150 LOC)      │    │
│   │                     ▲                  ▲                         │    │
│   │                     │                  │                         │    │
│   │         ┌───────────┴──────┐  ┌───────┴────────┐                │    │
│   │         │PlayerStateMachine│  │EnemyStateMachine│                │    │
│   │         │    (~50 LOC)     │  │   (~50 LOC)    │                │    │
│   │         │   11 states      │  │   6 states     │                │    │
│   │         └──────────────────┘  └────────────────┘                │    │
│   └─────────────────────────────────────────────────────────────────┘    │
│                                                                          │
└──────────────────────────────────────────────────────────────────────────┘
```

---

## Key Components

### Player Components

| Component | Responsibility | LOC | Update Method | Interfaces |
|-----------|---------------|-----|---------------|------------|
| PlayerController | Facade, coordination | ~200 | - | ICombatant (delegated) |
| MovementController | Locomotion, gravity, physics | ~250 | FixedUpdate | - |
| CombatController | Attacks, combos, weapons | ~200 | Update | - |
| HealthPoiseController | HP, poise, stamina, damage | ~150 | Update | IDamageableWithPoise |
| AnimationController | Animator params, visual sync | ~150 | LateUpdate | - |
| LockOnController | Target tracking, rotation | ~100 | Update | ILockOnTarget |
| SmoothingState | Velocity/smoothing data | struct | - | - |

### State Machine Components

| Component | Responsibility | LOC |
|-----------|---------------|-----|
| BaseStateMachine<T,E,S> | Generic state machine base | ~150 |
| PlayerStateMachine | Player-specific config | ~50 |
| EnemyStateMachine | Enemy-specific config | ~50 |
| IState<TContext> | State lifecycle interface | ~20 |
| IStateWithInput | Input handling capability | ~5 |
| IInterruptibleState | Interruption capability | ~5 |

---

## Communication Patterns

### 1. Direct References (Same GameObject)
Components on the same GameObject use direct references via GetComponent:
```csharp
// MovementController needs AnimationController
animController = GetComponent<AnimationController>();
```

### 2. Events (Loose Coupling)
Notifications that don't need responses use events:
```csharp
public event Action<DamageInfo> OnDamageReceived;
public event Action OnDeath;
public event Action OnPoiseBreak;
```

### 3. Facade Pattern (External Systems)
External systems access player through PlayerController facade:
```csharp
// CombatSystem calls facade, not internal components
player.TakeDamage(damageInfo);  // Delegates to HealthPoiseController
```

---

## Update Strategy (Eliminating Update Soup)

### Before (Update Soup)
```csharp
// PlayerController.Update() - 4 mixed operations
private void Update()
{
    UpdateStaminaRegen();           // Resource management
    UpdatePoiseRegen();             // Resource management
    ApplyGravity();                 // Physics
    UpdateLockedLocomotionLayer();  // Animation
}
```

### After (Distributed Responsibility)
```csharp
// MovementController.FixedUpdate() - Physics
private void FixedUpdate()
{
    ApplyGravity();
    ApplyMovement();
}

// HealthPoiseController.Update() - Resources
private void Update()
{
    UpdateStaminaRegen();
    UpdatePoiseRegen();
}

// AnimationController.LateUpdate() - Animation
private void LateUpdate()
{
    UpdateLockedLocomotionLayer();
    SyncAnimationParameters();
}
```

---

## State Machine Architecture

### Generic Base Class
```csharp
public abstract class BaseStateMachine<TContext, TStateEnum, TState>
    where TContext : class
    where TStateEnum : struct, Enum
    where TState : class, IState<TContext>
{
    // Static reflection cache (one-time cost per type)
    private static Type[] cachedStateTypes;

    // State storage
    protected Dictionary<TStateEnum, TState> states;
    protected TState currentState;

    // Abstract methods for derived classes
    protected abstract TStateEnum GetInitialState();
    protected abstract void OnStateChanged(TState from, TState to);
}
```

### Performance Optimization
- LINQ eliminated → manual iteration
- Reflection cached → static per generic type
- First instantiation: 100-300ms (unchanged)
- Subsequent: <1ms (99.5% improvement)

---

## Changes from Current State

### Additions
| New Component | Purpose |
|--------------|---------|
| BaseStateMachine<T,E,S> | Generic state machine base |
| MovementController | Extracted from PlayerController |
| CombatController | Extracted from PlayerController |
| HealthPoiseController | Extracted from PlayerController |
| AnimationController | Extracted from PlayerController |
| LockOnController | Extracted from PlayerController |
| SmoothingState | Consolidated smoothing struct |
| PlayerState base class | Context-aware state base |
| EnemyState base class | Context-aware state base |

### Modifications
| Component | Change |
|-----------|--------|
| PlayerController | 920 LOC → ~200 LOC (Facade pattern) |
| PlayerStateMachine | 239 LOC → ~50 LOC (inherits base) |
| EnemyStateMachine | 230 LOC → ~50 LOC (inherits base) |
| All 11 player states | Inherit PlayerState base |
| All 6 enemy states | Inherit EnemyState base |

### Removals
| Item | Reason |
|------|--------|
| 15+ smoothing variables | Consolidated into SmoothingState struct |
| Duplicate FSM code | Moved to generic base |
| Update soup operations | Distributed to appropriate components |

---

## Dependency Graph

```
PlayerController
├── MovementController
│   ├── SmoothingState (struct)
│   └── AnimationController (rotation sync)
├── CombatController
│   └── AnimationController (attack anims)
├── HealthPoiseController
│   └── AnimationController (damage feedback)
├── AnimationController
│   └── Animator (Unity)
├── LockOnController
│   └── MovementController (rotation override)
└── PlayerStateMachine
    └── BaseStateMachine<PlayerController, PlayerStates, PlayerState>
```

---

## Migration Order (Risk-Based)

| Phase | Component | Risk | Effort | Dependencies |
|-------|-----------|------|--------|--------------|
| 1 | SmoothingState | LOW | 2-3h | None |
| 2 | HealthPoiseController | LOW | 3-4h | SmoothingState |
| 3 | AnimationController | LOW-MED | 4-5h | None |
| 4 | CombatController | MEDIUM | 5-6h | AnimationController |
| 5 | LockOnController | MEDIUM | 3-4h | MovementController |
| 6 | MovementController | HIGH | 6-8h | SmoothingState, Animation |
| 7 | PlayerController Facade | LOW | 2-3h | All above |
| 8 | BaseStateMachine | MEDIUM | 4-5h | None |
| 9 | State Migration | MEDIUM | 3-4h | BaseStateMachine |

**Total Estimated Effort**: ~35-45 hours

---

## Validation Requirements

### Acceptance Criteria Check
- [ ] AC-01: No file exceeds 300 LOC
- [ ] AC-02: State transitions identical behavior
- [ ] AC-03: 60 FPS maintained
- [ ] AC-04: Unity compiles without errors
- [ ] AC-05: No new GC allocations in Update loops
- [ ] AC-06: Documentation updated

### Testing Strategy
1. **Unit Tests**: State machine transitions, component isolation
2. **Integration Tests**: Component communication, event flow
3. **Manual Testing**: Gameplay feel, input responsiveness
4. **Performance Profiling**: GC allocations, frame time

---

## Context Loading for Next Phase

When loading Phase 4 (Planning):
1. Load `INDEX.md` for overall status
2. Load this file for architecture reference
3. Load `decisions.md` for rationale on key decisions
4. Use `--think-hard` depth for workflow planning
