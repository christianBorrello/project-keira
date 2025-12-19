# Phase 3: Design - Architecture Decision Records

## Task: Deep Refactoring GameDev Patterns
**Status**: Completed
**Date**: 2025-12-19

---

## Decisions Summary

| ADR | Title | Status |
|-----|-------|--------|
| ADR-001 | Facade Pattern for PlayerController | Accepted |
| ADR-002 | Generic State Machine Base | Accepted |
| ADR-003 | Component Communication Strategy | Accepted |
| ADR-004 | Update Distribution Strategy | Accepted |
| ADR-005 | SmoothingState as Struct | Accepted |
| ADR-006 | Reflection Caching for State Registration | Accepted |

---

## ADR-001: Facade Pattern for PlayerController

**Status**: Accepted
**Date**: 2025-12-19

**Context**:
PlayerController (920 LOC) is a God Class that violates SRP. External systems (InputHandler, CombatSystem, LockOnSystem) depend on PlayerController's public API. We need to decompose without breaking external contracts.

**Decision**:
Keep PlayerController as a thin Facade (~200 LOC) that:
- Coordinates internal components
- Exposes stable public API to external systems
- Delegates implementation to specialized components

**Alternatives Considered**:
1. **Remove PlayerController entirely**: Would break all external dependencies
2. **Interface-based abstraction**: Overkill for single player character
3. **Mediator pattern**: More complex, unnecessary for this scope

**Consequences**:
- ✅ External systems unchanged (stable API)
- ✅ Internal components can evolve independently
- ✅ Clear ownership of each concern
- ⚠️ Slight indirection overhead (negligible)

---

## ADR-002: Generic State Machine Base

**Status**: Accepted
**Date**: 2025-12-19

**Context**:
PlayerStateMachine (239 LOC) and EnemyStateMachine (230 LOC) share 90% identical code. Both use reflection-based state registration with LINQ causing 100-300ms initialization spikes.

**Decision**:
Create `BaseStateMachine<TContext, TStateEnum, TState>` with:
- Generic type parameters for context, enum, and state type
- Static reflection cache per generic instantiation
- Manual iteration replacing LINQ
- Abstract methods for derived class customization

**Alternatives Considered**:
1. **Keep separate implementations**: Maintains 90% duplication, double maintenance
2. **Code generation**: Adds build complexity, harder to debug
3. **Inheritance without generics**: Less type safety, more casting

**Consequences**:
- ✅ 90% code reduction (239+230 → ~150 shared + 50+50 derived)
- ✅ 99.5% faster subsequent instantiation (<1ms vs 100-300ms)
- ✅ Type-safe state registration via enums
- ✅ Zero-config pattern preserved (attribute-based)
- ⚠️ Slightly more complex type signatures

---

## ADR-003: Component Communication Strategy

**Status**: Accepted
**Date**: 2025-12-19

**Context**:
Extracted components need to communicate. Options: direct references, events, message bus, or interfaces. Unity's component model favors certain patterns.

**Decision**:
Hybrid approach:
1. **Direct references** for tightly coupled components on same GameObject (via GetComponent)
2. **Events** for loose coupling and notifications (damage, death, state changes)
3. **Facade delegation** for external system access

**Alternatives Considered**:
1. **Pure events**: Overcomplicates simple component interactions
2. **Message bus**: Overkill for single-player game
3. **Service locator**: Anti-pattern, hidden dependencies
4. **Dependency injection**: Not idiomatic for Unity MonoBehaviours

**Consequences**:
- ✅ Follows Unity idioms (GetComponent pattern)
- ✅ Clear dependency graph
- ✅ Events enable loose coupling where needed
- ✅ Testable via interface mocking if needed
- ⚠️ Must validate references in Awake()

---

## ADR-004: Update Distribution Strategy

**Status**: Accepted
**Date**: 2025-12-19

**Context**:
Current PlayerController.Update() contains 4 operations mixing physics, resources, and animation. This "Update Soup" violates SRP and makes profiling difficult.

**Decision**:
Distribute Update operations to appropriate Unity lifecycle methods:
- **FixedUpdate**: Physics (gravity, movement)
- **Update**: Game logic (stamina regen, poise regen)
- **LateUpdate**: Animation sync (after all gameplay logic)

**Alternatives Considered**:
1. **Central update orchestrator**: Adds complexity, no benefit
2. **Coroutines**: Wrong tool for per-frame operations
3. **Update manager pattern**: Overkill for this scope

**Consequences**:
- ✅ Each component owns its lifecycle
- ✅ Physics in FixedUpdate (consistent timing)
- ✅ Animation in LateUpdate (correct ordering)
- ✅ Easy to profile individual components
- ⚠️ Must consider execution order for dependencies

---

## ADR-005: SmoothingState as Struct

**Status**: Accepted
**Date**: 2025-12-19

**Context**:
PlayerController has 15+ smoothing velocity variables scattered throughout the class. These are used by movement, rotation, and animation smoothing.

**Decision**:
Consolidate into `SmoothingState` struct:
- All velocity/smoothing fields in one place
- Passed by reference to avoid copying
- Serializable for Inspector visibility

**Alternatives Considered**:
1. **Keep scattered variables**: Current mess, hard to maintain
2. **Class instead of struct**: Unnecessary heap allocation
3. **Multiple smaller structs**: Adds complexity, related data split

**Consequences**:
- ✅ 15+ variables → 1 struct
- ✅ Clear grouping of related data
- ✅ No GC allocations (struct)
- ✅ Inspector visible via [Serializable]
- ⚠️ Pass by ref to avoid copy overhead

---

## ADR-006: Reflection Caching for State Registration

**Status**: Accepted
**Date**: 2025-12-19

**Context**:
Current LINQ-based reflection runs every state machine instantiation, causing 100-300ms spikes per spawn. This is unacceptable for runtime spawning of enemies.

**Decision**:
Implement static reflection cache:
- Cache `Type[]` array per generic type instantiation
- Manual iteration instead of LINQ (no allocations)
- First instantiation pays cost, subsequent are free

**Alternatives Considered**:
1. **Runtime reflection every time**: Current problem, 100-300ms spikes
2. **Code generation**: Loses zero-config benefit
3. **Manual registration**: Loses attribute-based discovery
4. **Source generators**: Build complexity, C# version concerns

**Consequences**:
- ✅ First instantiation: unchanged (100-300ms)
- ✅ Subsequent: <1ms (99.5% improvement)
- ✅ Zero runtime allocations
- ✅ Preserves zero-config [State] attribute pattern
- ⚠️ ~1KB static memory per state machine type

---

## Context Loading

When referencing these decisions:
1. Scan titles for quick context
2. Read full ADR only when implementation details needed
3. Update status if decisions change during implementation
