# Phase 4: Planning - Implementation Workflow

## Task: Deep Refactoring GameDev Patterns
**Status**: Completed
**Depth**: `--think-hard`
**Date**: 2025-12-19

---

## Executive Summary

10-phase implementation plan ordered by risk (lowest first). Each phase includes quality gates, rollback plans, and git checkpoint strategy.

**Total Estimated Effort**: 35-45 hours across 10 phases
**Parallelization**: 2 parallel tracks identified (FSM + Component extraction)

---

## Phase Overview

| Phase | Description | Risk | Effort | Dependencies | Git Branch |
|-------|-------------|------|--------|--------------|------------|
| P1 | SmoothingState struct | LOW | 2-3h | None | `refactor/smoothing-state` |
| P2 | State Machine interfaces | LOW | 1-2h | None | `refactor/fsm-interfaces` |
| P3 | BaseStateMachine implementation | MEDIUM | 3-4h | P2 | `refactor/base-fsm` |
| P4 | AnimationController extraction | LOW-MED | 4-5h | P1 | `refactor/animation-ctrl` |
| P5 | HealthPoiseController extraction | LOW | 3-4h | P1 | `refactor/health-poise` |
| P6 | CombatController extraction | MEDIUM | 5-6h | P4 | `refactor/combat-ctrl` |
| P7 | LockOnController extraction | MEDIUM | 3-4h | P4 | `refactor/lockon-ctrl` |
| P8 | MovementController extraction | HIGH | 6-8h | P1, P4 | `refactor/movement-ctrl` |
| P9 | PlayerController Facade finalization | LOW | 2-3h | P4-P8 | `refactor/facade` |
| P10 | State Migration + Performance | MEDIUM | 4-5h | P3, P9 | `refactor/state-migration` |

---

## Parallel Execution Strategy

### Track A: State Machine Refactor
```
P2 → P3 → P10 (State Migration)
```

### Track B: Component Extraction
```
P1 → P4 → P5 → P6 → P7 → P8 → P9
```

**Parallelization Opportunity**:
- Track A (P2, P3) can run in parallel with Track B (P1, P4, P5)
- P10 requires both tracks complete

```
Timeline (sequential):     P1 → P2 → P3 → P4 → P5 → P6 → P7 → P8 → P9 → P10 = ~37h
Timeline (parallel):       [P1+P2] → [P3+P4] → [P5+P6] → [P7+P8] → P9 → P10 = ~28h
```

**Estimated Time Saved**: ~25% with parallel execution

---

## Detailed Phases

### Phase P1: SmoothingState Struct

**Goal**: Consolidate 15+ smoothing variables into single struct

**Risk Level**: LOW
**Effort**: 2-3 hours
**Dependencies**: None

**Tasks**:
1. [ ] Create `Assets/_Scripts/Player/Data/SmoothingState.cs`
2. [ ] Define struct with all smoothing fields
3. [ ] Add `[Serializable]` attribute for Inspector
4. [ ] Add `CreateDefault()` factory method
5. [ ] Replace variables in PlayerController with struct field
6. [ ] Update all references to use struct members
7. [ ] Verify Inspector shows SmoothingState fields

**Files Modified**:
- `Assets/_Scripts/Player/PlayerController.cs` (modify)
- `Assets/_Scripts/Player/Data/SmoothingState.cs` (create)

**Quality Gate**:
- [ ] Unity compiles without errors
- [ ] All smoothing behavior unchanged (manual test)
- [ ] Inspector shows struct fields correctly
- [ ] No new warnings in console

**Rollback Plan**: `git reset --hard HEAD~1`

**Git Checkpoint**:
```bash
git add -A && git commit -m "refactor(player): extract SmoothingState struct"
```

---

### Phase P2: State Machine Interfaces

**Goal**: Create interface hierarchy for state machine system

**Risk Level**: LOW
**Effort**: 1-2 hours
**Dependencies**: None

**Tasks**:
1. [ ] Create `Assets/_Scripts/StateMachine/Interfaces/IState.cs`
2. [ ] Create `Assets/_Scripts/StateMachine/Interfaces/IStateWithInput.cs`
3. [ ] Create `Assets/_Scripts/StateMachine/Interfaces/IInterruptibleState.cs`
4. [ ] Update `StateAttribute.cs` if needed

**Files Created**:
- `Assets/_Scripts/StateMachine/Interfaces/IState.cs`
- `Assets/_Scripts/StateMachine/Interfaces/IStateWithInput.cs`
- `Assets/_Scripts/StateMachine/Interfaces/IInterruptibleState.cs`

**Quality Gate**:
- [ ] Unity compiles without errors
- [ ] Interfaces follow design spec (ADR-002)

**Rollback Plan**: `git reset --hard HEAD~1`

**Git Checkpoint**:
```bash
git add -A && git commit -m "feat(fsm): add state machine interfaces"
```

---

### Phase P3: BaseStateMachine Implementation

**Goal**: Create generic state machine base with reflection caching

**Risk Level**: MEDIUM
**Effort**: 3-4 hours
**Dependencies**: P2 (interfaces)

**Tasks**:
1. [ ] Create `Assets/_Scripts/StateMachine/BaseStateMachine.cs`
2. [ ] Implement static reflection caching (no LINQ)
3. [ ] Implement `CacheStateTypes()` with manual iteration
4. [ ] Implement `RegisterStatesFromCache()`
5. [ ] Implement `ChangeState()` with validation
6. [ ] Implement `Update()` and `FixedUpdate()` routing
7. [ ] Add abstract methods: `GetInitialState()`, `OnStateChanged()`
8. [ ] Add virtual method: `CanTransition()`

**Files Created**:
- `Assets/_Scripts/StateMachine/BaseStateMachine.cs`

**Quality Gate**:
- [ ] Unity compiles without errors
- [ ] Performance test: subsequent init <1ms (measure with Stopwatch)
- [ ] No LINQ in hot paths

**Rollback Plan**: `git reset --hard HEAD~1`

**Git Checkpoint**:
```bash
git add -A && git commit -m "feat(fsm): implement BaseStateMachine with reflection caching"
```

---

### Phase P4: AnimationController Extraction

**Goal**: Extract animation logic to dedicated component

**Risk Level**: LOW-MEDIUM
**Effort**: 4-5 hours
**Dependencies**: P1 (SmoothingState)

**Tasks**:
1. [ ] Create `Assets/_Scripts/Player/Components/AnimationController.cs`
2. [ ] Move animator parameter caching logic
3. [ ] Implement `Animator.StringToHash()` caching (static readonly)
4. [ ] Move `UpdateLockedLocomotionLayer()` to `LateUpdate()`
5. [ ] Move animation parameter setters
6. [ ] Add public methods for other components to trigger animations
7. [ ] Update PlayerController to delegate animation calls
8. [ ] Add component to Player prefab

**Files Modified/Created**:
- `Assets/_Scripts/Player/Components/AnimationController.cs` (create)
- `Assets/_Scripts/Player/PlayerController.cs` (modify)
- Player prefab (modify)

**Quality Gate**:
- [ ] Unity compiles without errors
- [ ] AnimationController < 150 LOC
- [ ] All animations play correctly (manual test)
- [ ] Animator parameter hashes cached (no string lookup per frame)

**Rollback Plan**: `git reset --hard HEAD~1`, remove component from prefab

**Git Checkpoint**:
```bash
git add -A && git commit -m "refactor(player): extract AnimationController component"
```

---

### Phase P5: HealthPoiseController Extraction

**Goal**: Extract health, poise, and stamina management

**Risk Level**: LOW
**Effort**: 3-4 hours
**Dependencies**: P1 (SmoothingState)

**Tasks**:
1. [ ] Create `Assets/_Scripts/Player/Components/HealthPoiseController.cs`
2. [ ] Implement `IDamageableWithPoise` interface
3. [ ] Move health/poise/stamina fields and properties
4. [ ] Move `UpdateStaminaRegen()` and `UpdatePoiseRegen()` to `Update()`
5. [ ] Move `TakeDamage()`, `Heal()`, `Die()` methods
6. [ ] Add events: `OnDamageReceived`, `OnDeath`, `OnPoiseBreak`
7. [ ] Update PlayerController to delegate health calls
8. [ ] Add component to Player prefab

**Files Modified/Created**:
- `Assets/_Scripts/Player/Components/HealthPoiseController.cs` (create)
- `Assets/_Scripts/Player/PlayerController.cs` (modify)
- Player prefab (modify)

**Quality Gate**:
- [ ] Unity compiles without errors
- [ ] HealthPoiseController < 150 LOC
- [ ] Damage reception works (manual test)
- [ ] Stamina/poise regen works
- [ ] Events fire correctly

**Rollback Plan**: `git reset --hard HEAD~1`, remove component from prefab

**Git Checkpoint**:
```bash
git add -A && git commit -m "refactor(player): extract HealthPoiseController component"
```

---

### Phase P6: CombatController Extraction

**Goal**: Extract attack and combo logic

**Risk Level**: MEDIUM
**Effort**: 5-6 hours
**Dependencies**: P4 (AnimationController)

**Tasks**:
1. [ ] Create `Assets/_Scripts/Player/Components/CombatController.cs`
2. [ ] Move attack initiation logic
3. [ ] Move combo tracking and timing
4. [ ] Move weapon-related methods
5. [ ] Connect to AnimationController for attack anims
6. [ ] Connect to HealthPoiseController for stamina consumption
7. [ ] Update state machine states to use CombatController
8. [ ] Add component to Player prefab

**Files Modified/Created**:
- `Assets/_Scripts/Player/Components/CombatController.cs` (create)
- `Assets/_Scripts/Player/PlayerController.cs` (modify)
- Player prefab (modify)

**Quality Gate**:
- [ ] Unity compiles without errors
- [ ] CombatController < 200 LOC
- [ ] Light attack works (manual test)
- [ ] Heavy attack works
- [ ] Combo system works
- [ ] Stamina consumed correctly

**Rollback Plan**: `git reset --hard HEAD~1`, remove component from prefab

**Git Checkpoint**:
```bash
git add -A && git commit -m "refactor(player): extract CombatController component"
```

---

### Phase P7: LockOnController Extraction

**Goal**: Extract lock-on targeting logic

**Risk Level**: MEDIUM
**Effort**: 3-4 hours
**Dependencies**: P4 (AnimationController)

**Tasks**:
1. [ ] Create `Assets/_Scripts/Player/Components/LockOnController.cs`
2. [ ] Implement `ILockOnTarget` interface
3. [ ] Move target acquisition and switching
4. [ ] Implement `Physics.OverlapSphereNonAlloc()` (performance fix)
5. [ ] Add target caching with refresh interval (0.2-0.3s)
6. [ ] Connect to camera system
7. [ ] Update PlayerController to delegate lock-on calls
8. [ ] Add component to Player prefab

**Files Modified/Created**:
- `Assets/_Scripts/Player/Components/LockOnController.cs` (create)
- `Assets/_Scripts/Player/PlayerController.cs` (modify)
- Player prefab (modify)

**Quality Gate**:
- [ ] Unity compiles without errors
- [ ] LockOnController < 100 LOC
- [ ] Lock-on acquisition works (manual test)
- [ ] Target switching works
- [ ] No Physics.OverlapSphere GC allocations (profiler check)

**Rollback Plan**: `git reset --hard HEAD~1`, remove component from prefab

**Git Checkpoint**:
```bash
git add -A && git commit -m "refactor(player): extract LockOnController component with NonAlloc physics"
```

---

### Phase P8: MovementController Extraction

**Goal**: Extract core movement and physics logic (highest risk, do last)

**Risk Level**: HIGH
**Effort**: 6-8 hours
**Dependencies**: P1 (SmoothingState), P4 (AnimationController)

**Tasks**:
1. [ ] Create `Assets/_Scripts/Player/Components/MovementController.cs`
2. [ ] Move character controller/rigidbody references
3. [ ] Move `ApplyGravity()` to `FixedUpdate()`
4. [ ] Move movement calculation and application
5. [ ] Move rotation logic
6. [ ] Use SmoothingState for all smoothing
7. [ ] Connect to AnimationController for movement anims
8. [ ] Handle lock-on rotation override (from LockOnController)
9. [ ] Update state machine states to use MovementController
10. [ ] Add component to Player prefab

**Files Modified/Created**:
- `Assets/_Scripts/Player/Components/MovementController.cs` (create)
- `Assets/_Scripts/Player/PlayerController.cs` (modify)
- Multiple state classes (modify)
- Player prefab (modify)

**Quality Gate**:
- [ ] Unity compiles without errors
- [ ] MovementController < 250 LOC
- [ ] Walking works (manual test)
- [ ] Running works
- [ ] Sprinting works
- [ ] Gravity feels correct
- [ ] Rotation is smooth
- [ ] No jitter or physics issues
- [ ] Input responsiveness unchanged

**Rollback Plan**: `git reset --hard HEAD~1`, remove component from prefab

**Git Checkpoint**:
```bash
git add -A && git commit -m "refactor(player): extract MovementController component"
```

---

### Phase P9: PlayerController Facade Finalization

**Goal**: Clean up PlayerController to pure Facade pattern

**Risk Level**: LOW
**Effort**: 2-3 hours
**Dependencies**: P4, P5, P6, P7, P8 (all component extractions)

**Tasks**:
1. [ ] Remove all extracted logic from PlayerController
2. [ ] Keep only coordination and delegation
3. [ ] Implement ICombatant via delegation
4. [ ] Add component reference getters for external systems
5. [ ] Verify all external systems still work
6. [ ] Clean up unused fields and methods

**Files Modified**:
- `Assets/_Scripts/Player/PlayerController.cs` (major cleanup)

**Quality Gate**:
- [ ] Unity compiles without errors
- [ ] PlayerController < 200 LOC
- [ ] All external systems work (CombatSystem, LockOnSystem, etc.)
- [ ] State machine still functions
- [ ] Full gameplay test passes

**Rollback Plan**: `git reset --hard HEAD~1`

**Git Checkpoint**:
```bash
git add -A && git commit -m "refactor(player): finalize PlayerController as Facade"
```

---

### Phase P10: State Migration + Performance

**Goal**: Migrate state machines to use new base class + final performance fixes

**Risk Level**: MEDIUM
**Effort**: 4-5 hours
**Dependencies**: P3 (BaseStateMachine), P9 (Facade complete)

**Tasks**:

**Part A: State Machine Migration**
1. [ ] Create `Assets/_Scripts/Player/States/PlayerState.cs` base class
2. [ ] Create `Assets/_Scripts/Enemies/States/EnemyState.cs` base class
3. [ ] Refactor `PlayerStateMachine` to inherit `BaseStateMachine`
4. [ ] Refactor `EnemyStateMachine` to inherit `BaseStateMachine`
5. [ ] Update all 11 player states to inherit `PlayerState`
6. [ ] Update all 6 enemy states to inherit `EnemyState`
7. [ ] Add `IStateWithInput` to input-handling player states
8. [ ] Add `IInterruptibleState` to interruptible states

**Part B: Performance Fixes**
9. [ ] Fix InputHandler List allocation (pre-allocate buffer)
10. [ ] Verify animator parameter caching (from P4)
11. [ ] Verify Physics.OverlapSphereNonAlloc (from P7)

**Files Modified/Created**:
- `Assets/_Scripts/Player/States/PlayerState.cs` (create)
- `Assets/_Scripts/Enemies/States/EnemyState.cs` (create)
- `Assets/_Scripts/Player/PlayerStateMachine.cs` (modify)
- `Assets/_Scripts/Enemies/EnemyStateMachine.cs` (modify)
- All 11 player state files (modify)
- All 6 enemy state files (modify)
- `Assets/Systems/InputHandler.cs` (modify)

**Quality Gate**:
- [ ] Unity compiles without errors
- [ ] All state transitions work identically (AC-02)
- [ ] Enemy AI works correctly
- [ ] Performance: FSM init <1ms (subsequent)
- [ ] Performance: No GC allocations in Update loops
- [ ] Profiler shows improvement

**Rollback Plan**: `git reset --hard HEAD~1`

**Git Checkpoint**:
```bash
git add -A && git commit -m "refactor(fsm): migrate to BaseStateMachine + performance fixes"
```

---

## Acceptance Criteria Verification

After P10, verify all acceptance criteria:

| Criteria | Check | Phase |
|----------|-------|-------|
| AC-01: No file > 300 LOC | [ ] | P4, P5, P6, P7, P8, P9 |
| AC-02: State transitions identical | [ ] | P10 |
| AC-03: 60 FPS maintained | [ ] | P10 |
| AC-04: Unity compiles | [ ] | All phases |
| AC-05: No new GC in Update | [ ] | P4, P7, P10 |
| AC-06: Documentation updated | [ ] | P10 (Phase 7) |

---

## Risk Mitigation Summary

| Phase | Risk | Mitigation |
|-------|------|------------|
| P3 | Reflection caching breaks | Manual iteration tested, rollback ready |
| P6 | Combat system regression | Incremental changes, test after each step |
| P8 | Movement feels different | Preserve exact smoothing values, extensive testing |
| P10 | State machine behavior change | Document transitions before, verify after |

---

## Daily Checkpoint Strategy

1. **Before each phase**: `git status`, `git branch`
2. **After each task**: Unity play mode test
3. **After each phase**: Commit with descriptive message
4. **End of day**: Push to remote (backup)

---

## Context Loading for Next Phase

When loading Phase 5 (Implementation):
1. Load `INDEX.md` for overall status
2. Load this workflow for current phase tasks
3. Start with P1 (SmoothingState) - lowest risk
4. Follow quality gates before proceeding to next phase
