# Phase 1: Discovery - Requirements

## Task: Deep Refactoring GameDev Patterns
**Status**: In Progress
**Depth**: `--think-hard`
**Date**: 2025-12-19

---

## Obiettivi del Refactoring

### Obiettivo Primario
Applicare le best practices definite in `Assets/claudedocs/GAMEDEV_CODING_PATTERNS.md` al codebase esistente, con focus su:
1. **Leggibilita'** - Codice chiaro, ben strutturato, facile da capire
2. **Performance** - 60 FPS target, zero allocazioni in hot paths
3. **Manutenibilita'** - Facilita' di estensione e modifica

### Vincolo Chiave
- Retrocompatibilita' **flessibile**: OK ricreare prefab/scene se il refactoring lo richiede

---

## Functional Requirements

### Must Have (P0)

- [x] **REQ-F01**: Decomposizione PlayerController
  - Da 920 LOC a ~300 LOC per componente
  - Separare: Movement, Combat, Health, Animation
  - Mantenere comportamento identico al giocatore

- [x] **REQ-F02**: Consolidamento State Machine
  - Estrarre `BaseStateMachine<TEnum, TState>` generico
  - Riutilizzare per Player e Enemy
  - Ridurre duplicazione del 90%

- [x] **REQ-F03**: Refactoring Update Soup
  - Rimuovere le 4 operazioni da Update() di PlayerController
  - Implementare pattern appropriato (event-driven o system-based)

- [x] **REQ-F04**: Estrazione Smoothing State
  - Creare struct dedicata per le 15+ velocity variables
  - Migliorare leggibilita' e manutenibilita'

### Should Have (P1)

- [x] **REQ-F05**: Decoupling AnimationEventBridge
  - Rimuovere hardcoding su PlayerController
  - Implementare pattern event bus o interface

- [x] **REQ-F06**: Rimozione Magic Numbers
  - Esternalizzare `LockedLocomotionLayerIndex = 1`
  - Usare configurazione o costanti nominate

- [x] **REQ-F07**: Cleanup UnitBase
  - Valutare rimozione classe obsoleta
  - O integrazione con sistema interfacce esistente

### Nice to Have (P2)

- [ ] **REQ-F08**: Cache Reflection Results
  - Ottimizzare `AppDomain.GetAssemblies()` in Initialize()
  - Performance improvement per initialization

- [ ] **REQ-F09**: Object Pooling Review
  - Verificare implementazione esistente
  - Applicare pattern dove mancante (particles, effects)

---

## Non-Functional Requirements

### Performance (NFR-P)

| ID | Requirement | Target | Measurement |
|----|-------------|--------|-------------|
| NFR-P01 | Frame rate | 60 FPS sustained | Unity Profiler |
| NFR-P02 | Frame time | < 16.67ms | Unity Profiler |
| NFR-P03 | GC Allocations | 0 bytes/frame in Update loops | Memory Profiler |
| NFR-P04 | Load time | No regression | Stopwatch |

### Code Quality (NFR-Q)

| ID | Requirement | Target | Measurement |
|----|-------------|--------|-------------|
| NFR-Q01 | Max class LOC | 300 lines | Line count |
| NFR-Q02 | Single Responsibility | 1 reason to change per class | Code review |
| NFR-Q03 | Cyclomatic complexity | < 10 per method | Static analysis |
| NFR-Q04 | Method length | < 30 lines | Line count |

### Maintainability (NFR-M)

| ID | Requirement | Target | Measurement |
|----|-------------|--------|-------------|
| NFR-M01 | Interface segregation | No unused interface members | Code review |
| NFR-M02 | Dependency direction | High -> Low level | Architecture review |
| NFR-M03 | Naming consistency | Follow GAMEDEV_CODING_PATTERNS | Code review |

---

## Acceptance Criteria

### AC-01: PlayerController Decomposition
- [ ] PlayerController LOC reduced to ~300
- [ ] New components created: MovementSystem, CombatController, HealthManager
- [ ] All tests pass (manual gameplay verification)
- [ ] Input responsiveness unchanged
- [ ] Animation transitions identical

### AC-02: State Machine Consolidation
- [ ] Single `BaseStateMachine<TEnum, TState>` class exists
- [ ] PlayerStateMachine inherits from generic base
- [ ] EnemyStateMachine inherits from generic base
- [ ] State registration via reflection works
- [ ] State transitions identical to before

### AC-03: Update Pattern
- [ ] No direct business logic in Update()
- [ ] Systems update through proper channels
- [ ] Frame-independent physics in FixedUpdate
- [ ] Camera follow in LateUpdate

### AC-04: Code Quality
- [ ] No class > 300 LOC
- [ ] No method > 30 LOC
- [ ] No magic numbers in code
- [ ] All public APIs documented

### AC-05: Performance
- [ ] 60 FPS sustained in gameplay
- [ ] No GC spikes visible in profiler
- [ ] No frame drops during combat

### AC-06: Functionality Preservation
- [ ] Player movement feels identical
- [ ] Combat system unchanged (damage, hitboxes)
- [ ] Lock-on system functional
- [ ] State machine transitions correct
- [ ] Input buffering works

---

## Out of Scope

1. **New Features** - No gameplay additions
2. **UI Refactoring** - Unless blocking core refactoring
3. **Asset Changes** - Models, animations, sounds unchanged
4. **Network Code** - Not present, not added
5. **Save System** - If present, not modified

---

## Success Metrics

| Metric | Before | Target | Priority |
|--------|--------|--------|----------|
| PlayerController LOC | 920 | ~300 | P0 |
| State Machine Duplication | 90% | < 10% | P0 |
| Update() complexity | 4 ops | 0 ops | P0 |
| Smoothing variables scattered | 15+ | 1 struct | P1 |
| Max class size | 920 LOC | 300 LOC | P1 |

---

## Context Loading
When loading this phase:
1. Reference this file for acceptance criteria during validation
2. Use Success Metrics to track progress
3. Verify Out of Scope items are not touched
