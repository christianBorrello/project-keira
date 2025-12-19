# Phase 1: Discovery - Constraints

## Task: Deep Refactoring GameDev Patterns
**Status**: In Progress
**Date**: 2025-12-19

---

## Technical Constraints

### Framework Constraints
- **Unity Version**: Current project version (do not upgrade)
- **MonoBehaviour Lifecycle**: Respect Awake -> OnEnable -> Start -> Update order
- **Serialization**: Maintain [SerializeField] compatibility where reasonable
- **Assembly References**: No new external dependencies

### Architecture Constraints
- **Interface Contracts**: Must maintain:
  - `ICombatant`
  - `IDamageable`
  - `IDamageableWithPoise`
  - `ILockOnTarget`
  - `IStateWithInput`
  - `IInterruptibleState`

- **State Machine Behavior**:
  - Reflection-based registration must continue working
  - State transitions must be identical
  - Enter/Execute/Exit lifecycle preserved

- **Event System**:
  - `Action<>` events must continue firing
  - Subscribers must not break

### Performance Constraints
- **Zero Allocations**: No `new` in Update/FixedUpdate/LateUpdate
- **Physics**: Must remain in FixedUpdate
- **No LINQ in hot paths**: Use explicit loops
- **No string operations in Update**: Use StringToHash for animator params

### Code Organization Constraints
- **Namespace Structure**: Maintain existing hierarchy
- **File Location**: Follow existing `_Scripts/` organization
- **Naming Conventions**: Follow GAMEDEV_CODING_PATTERNS.md

---

## Business Constraints

### Timeline
- **No hard deadline**: Quality over speed
- **Incremental delivery**: Commit after each working change

### Resources
- **Single developer**: Claude Code
- **No pair programming**: Self-review required
- **Documentation**: Update as code changes

### Risk Tolerance
- **Retrocompatibilita' prefab**: FLESSIBILE (user decision)
- **Gameplay feel**: MUST preserve exactly
- **Performance regression**: NOT acceptable

---

## Scope Constraints

### In Scope
1. Player systems refactoring (PlayerController decomposition)
2. State machine consolidation (generic base class)
3. Code organization improvements (Update soup cleanup)
4. Smoothing variables extraction
5. Magic number removal
6. AnimationEventBridge decoupling
7. UnitBase cleanup

### Out of Scope
1. New gameplay features
2. UI changes (unless blocking)
3. Asset modifications
4. Network/multiplayer code
5. Save system changes
6. Third-party package updates
7. Editor tools

### Gray Area (Evaluate case by case)
- Minor bug fixes discovered during refactoring
- Performance optimizations beyond requirements
- Documentation improvements

---

## Dependency Constraints

### External Dependencies (Do Not Modify)
- **Ilumisoft Health System**: Third-party package
- **Cinemachine**: Unity package
- **Input System**: Unity package
- **TextMesh Pro**: Unity package

### Internal Dependencies (Can Modify)
- **PlayerController** -> all new components
- **PlayerStateMachine** -> new generic base
- **EnemyStateMachine** -> new generic base
- **AnimationEventBridge** -> decoupled interface

### Integration Points (Verify After Changes)
- `CombatSystem` <-> `PlayerController`
- `InputHandler` <-> `PlayerController`
- `LockOnSystem` <-> `PlayerController`
- `ThirdPersonCameraSystem` <-> `PlayerController`

---

## Quality Constraints

### Code Review Checklist
Before each commit:
- [ ] No class > 300 LOC
- [ ] No method > 30 LOC
- [ ] No magic numbers
- [ ] No LINQ in Update loops
- [ ] No allocations in hot paths
- [ ] All public methods documented
- [ ] Naming follows conventions

### Testing Constraints
- **No automated tests available**: Manual gameplay testing required
- **Testing environment**: Unity Editor play mode
- **Regression testing**: Full gameplay loop after major changes

---

## Context Loading
When loading this phase:
1. Review constraints before making changes
2. Check Integration Points after modifications
3. Use Quality Constraints checklist before commits
