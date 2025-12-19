# Architecture Decision Records (ADR)

**Data**: 2025-12-15
**Fase**: 03-Design

---

## ADR-001: HSM + Systems Architecture Pattern

**Status**: Accepted
**Context**:
Il framework turn-based esistente deve essere trasformato in un souls-like action RPG. Sono state valutate tre alternative architetturali:
- Opzione A: Monolithic Controller
- Opzione B: Component-Based (ECS-lite)
- Opzione C: Hierarchical State Machine + Systems

**Decision**:
Adottare **Opzione C: HSM + Systems** con:
- Systems singleton per logica globale (CombatSystem, StaminaSystem, LockOnSystem)
- Hierarchical State Machine per comportamento entità (PlayerStateMachine, EnemyStateMachine)
- ScriptableObjects per configurazione data-driven

**Rationale**:
1. **Compatibilità codebase esistente**: StaticInstance<T> e Systems parent già production-ready
2. **Scalabilità**: HSM supporta naturalmente substati per combo, charge attacks, boss phases
3. **Testabilità**: Systems testabili isolatamente, states mockabili
4. **Manutenibilità**: Score 4.6/5 vs 2.15 Monolithic vs 3.6 Component
5. **Souls-like fit**: Pattern standard per action RPG combat (Lies of P, Dark Souls usano pattern simili)

**Consequences**:
- (+) Zero refactoring futuro per nuove features
- (+) Parallelizzazione sviluppo possibile
- (-) Setup iniziale più lungo (+1 settimana vs Monolithic)
- (-) Learning curve HSM per contributors

---

## ADR-002: Enum-based FSM over Class-based HSM

**Status**: Accepted
**Context**:
Per la state machine del player, due approcci possibili:
- Enum FSM: Stati come enum values, switch/case per transizioni
- Class-based HSM: Ogni stato è una classe che implementa IState

**Decision**:
Adottare **Enum FSM ibrido** per MVP:
- `PlayerState` enum per stati top-level (Idle, Walk, Attack, Parry, Dodge, Stagger, Death)
- `IState` interface per logica complessa interna agli stati
- Migrazione incrementale a full HSM post-MVP se necessario

**Rationale**:
1. **Requisito utente esplicito**: "Enum-based FSM" scelto durante discovery
2. **Semplicità MVP**: Meno boilerplate, debug più semplice
3. **Unity Editor friendly**: Enum visibile in Inspector
4. **Estensibilità preservata**: IState interface permette refactoring futuro

**Consequences**:
- (+) Implementazione rapida (~3 giorni vs ~5 giorni full HSM)
- (+) Debug facile con enum in Inspector
- (-) Substati complessi richiedono gestione manuale
- (-) Potenziale refactoring per weapon assembly system futuro

---

## ADR-003: Script-based Movement over Root Motion

**Status**: Accepted
**Context**:
Movement systems in action games:
- Root Motion: Movimento derivato da animation clips
- Script-based: Movimento controllato da codice, animazioni blended

**Decision**:
Adottare **Script-based Movement** per tutte le azioni (walk, sprint, dodge, attack movement).

**Rationale**:
1. **Responsività**: Lies of P-level responsiveness richiede <30ms input-to-movement
2. **Tuning indipendente**: Velocità configurabile via ScriptableObject senza ri-export animazioni
3. **Placeholder-friendly**: Funziona con capsule/cube durante MVP
4. **Predicibilità**: Comportamento deterministico, più facile da bilanciare

**Consequences**:
- (+) Maximum responsiveness achievable
- (+) Disaccoppiamento animation/movement
- (+) Funziona senza animazioni finali
- (-) Animation blending più complesso
- (-) Potential sliding feet (mitigabile con IK futuro)

---

## ADR-004: Single Defense Value for MVP

**Status**: Accepted
**Context**:
Per il sistema di difesa:
- Single defense: Un valore percentuale riduce tutto il danno
- Per-damage-type: Resistenze separate (Physical, Fire, Lightning, etc.)

**Decision**:
Implementare **Single Defense Value** (`PhysicalDefense` float, 0-0.9 range) per MVP.

**Rationale**:
1. **Scope MVP**: Solo Physical damage implementato
2. **Semplicità bilanciamento**: Un parametro da tuning
3. **Estensibilità**: CombatStats struct già predisposto per defense array futuro
4. **Lies of P reference**: Anche LoP usa sistema difesa semplificato per tier base

**Consequences**:
- (+) Bilanciamento più semplice
- (+) Meno parametri da configurare per enemy
- (-) Meno profondità build variety
- (-) Refactoring necessario per elemental system

---

## ADR-005: Lies of P-style Poise System

**Status**: Accepted
**Context**:
Due modelli poise principali:
- **Dark Souls/Elden Ring**: Poise come threshold istantaneo, reset dopo stagger
- **Lies of P**: Poise accumulativo, si riempie con colpi, si rompe al 100%

**Decision**:
Implementare **Lies of P-style Poise**:
- `MaxPoise`: Threshold massimo
- `CurrentPoise`: Accumula con ogni hit ricevuto
- Stagger quando `CurrentPoise >= MaxPoise`
- Recovery graduale dopo delay senza hit

**Rationale**:
1. **Primary inspiration**: Lies of P è reference principale
2. **Visual feedback**: Poise bar visibile da intuizione player
3. **Strategic depth**: Player può "costruire" stagger con combo
4. **Parry synergy**: Perfect parry può aggiungere poise damage bonus

**Consequences**:
- (+) Gameplay più strategico
- (+) Clear feedback loop per player
- (+) Differenziazione da Dark Souls
- (-) Bilanciamento più complesso (accumulazione vs reset)
- (-) UI necessaria per poise bar nemico

---

## ADR-006: Array-based Combo System

**Status**: Accepted
**Context**:
Strutture dati per combo:
- **Graph-based**: Nodi con transizioni, supporta branching complesso
- **Array-based**: Array lineare di AttackData, indexato da combo count

**Decision**:
Implementare **Array-based Combo** in ScriptableWeapon:
```csharp
public AttackData[] lightCombo;  // L1 -> L2 -> L3
public AttackData[] heavyCombo;  // H1 -> H2
```

**Rationale**:
1. **MVP scope**: Combo lineari sufficienti per base combat
2. **ScriptableObject friendly**: Array facilmente editabile in Inspector
3. **Performance**: O(1) lookup vs graph traversal
4. **Estensibilità**: Può evolvere a graph per weapon assembly system

**Consequences**:
- (+) Setup rapido nuove armi
- (+) Designer-friendly editing
- (-) No branching combos (L -> L -> H pattern richiede workaround)
- (-) Mixed combo array separato necessario

---

## ADR-007: Input Buffering Strategy

**Status**: Accepted
**Context**:
Input handling per action games:
- **Immediate**: Input processato frame-by-frame, nessun buffer
- **Buffered**: Input salvato in queue, consumato quando possibile

**Decision**:
Implementare **Input Buffering** con:
- Buffer window: 150ms (configurabile)
- Queue FIFO per combat actions (Attack, Parry, Dodge)
- Movement input sempre immediato (no buffer)

**Rationale**:
1. **Industry standard**: Tutti i souls-like usano input buffering
2. **Responsiveness percepita**: Player può "queueare" input durante recovery
3. **Skill expression**: Buffer window bilanciamento skill floor/ceiling
4. **Lies of P reference**: Input buffering evidente nel gameplay

**Consequences**:
- (+) Gameplay più fluido
- (+) Riduzione "eaten inputs" frustration
- (-) Complexity: gestione timestamp e cleanup
- (-) Tuning: buffer troppo lungo = imprecisione, troppo corto = frustrazione

---

## ADR-008: Unity New Input System

**Status**: Accepted
**Context**:
Unity offre due input systems:
- **Legacy Input Manager**: GetKeyDown, GetAxis
- **New Input System**: Event-driven, action maps, rebinding

**Decision**:
Utilizzare **New Input System** (già presente nel progetto, v1.17.0).

**Rationale**:
1. **Già installato**: Package presente in manifest, actions già definite
2. **Event-driven**: Si integra con architettura event-based
3. **Rebinding**: Support nativo per options menu futuro
4. **Cross-platform**: Controller support migliore

**Consequences**:
- (+) Zero setup package
- (+) Actions già definite (Move, Attack, Sprint, etc.)
- (-) Learning curve vs legacy input
- (-) Callback-based richiede gestione lifecycle

---

## ADR-009: Parry Timing Configuration

**Status**: Accepted
**Context**:
Il parry è meccanica critica per "feel" souls-like. Timing deve essere:
- Challenging ma fair
- Configurabile per tuning
- Testabile

**Decision**:
Implementare parry con **timing configurabile via ScriptableObject**:
- `ParryWindowDuration`: 200ms default
- `PerfectParryWindowDuration`: 100ms default (primi 100ms della finestra)
- Esposizione in CombatConfigSO per tuning runtime

**Rationale**:
1. **Risk mitigation**: Parry timing identificato come High Risk
2. **Iteration speed**: Modifica valori senza ricompilare
3. **A/B testing**: Facile testare multiple configurazioni
4. **Lies of P reference**: ~200ms parry window nel gioco

**Consequences**:
- (+) Fast iteration su feel
- (+) Balance team può modificare senza engineering
- (-) Overhead ScriptableObject loading
- (-) Potenziale inconsistenza tra multiple config files

---

## ADR-010: Physics Configuration Standards

**Status**: Accepted
**Context**:
Unity default physics settings insufficienti per combat precision:
- Solver iterations: 6/1 (default)
- No collision layers configurati

**Decision**:
Applicare configurazione physics ottimizzata:
- Solver Iterations: 10
- Velocity Iterations: 4
- Collision Detection: Continuous Dynamic (Player), Continuous (Enemy)
- Custom Layer Matrix per Player/Enemy/Weapon/Hitbox

**Rationale**:
1. **60 FPS target**: Richiede physics stabili
2. **Hit detection precision**: Hitbox/hurtbox devono collidere affidabilmente
3. **No tunneling**: Continuous detection previene passthrough ad alta velocità

**Consequences**:
- (+) Combat affidabile
- (+) No missed collisions
- (-) Leggero overhead performance
- (-) Setup iniziale layer matrix

---

## Summary Decision Matrix

| ADR | Decision | Confidence | Reversibility |
|-----|----------|------------|---------------|
| 001 | HSM + Systems | Alta | Bassa |
| 002 | Enum FSM | Media | Media |
| 003 | Script-based Movement | Alta | Bassa |
| 004 | Single Defense | Alta | Alta |
| 005 | Lies of P Poise | Alta | Media |
| 006 | Array Combos | Media | Alta |
| 007 | Input Buffering | Alta | Media |
| 008 | New Input System | Alta | Bassa |
| 009 | Configurable Parry | Alta | Alta |
| 010 | Physics Config | Alta | Alta |

---

## Context Loading

Per riprendere queste decisioni:
```
1. Leggi: decisions.md (questo file)
2. Reference: architecture.md per implementazione dettagliata
3. Procedi a: ../04-planning/
```
