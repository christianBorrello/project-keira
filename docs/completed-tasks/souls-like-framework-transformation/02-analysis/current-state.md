# Current State Analysis: Souls-Like Framework Transformation

**Data**: 2025-12-15
**Depth**: `--think-hard` 🔴

---

## Executive Summary

Il progetto "Project Keira" è un framework Unity turn-based minimale (~500 LOC) con architettura pulita e pattern solidi. **60% riutilizzabile** con modifiche, **40% richiede riscrittura completa**. La trasformazione a souls-like è tecnicamente fattibile con effort stimato di **4-6 settimane** per MVP combat.

---

## Agent Reports Consolidati

- ✅ `agent-explore.md` - Struttura progetto (completato)
- ✅ `agent-performance.md` - Analisi performance (completato)
- ✅ `agent-architecture.md` - Pattern architetturali (completato)

---

## Consolidated Findings

### 🟢 Componenti Riutilizzabili (60%)

| Componente | Stato | Uso Souls-Like |
|------------|-------|----------------|
| **StaticInstance Pattern** | ✅ Eccellente | Foundation per tutti i sistemi |
| **AudioSystem** | ✅ Buono | Aggiungere SFX combat, pooling audio sources |
| **ResourceSystem** | ✅ Buono | Estendere per weapons, enemies, patterns |
| **ScriptableObject Pattern** | ✅ Eccellente | Stats, weapons, enemy config |
| **Input System (config)** | ✅ Presente | Actions già definite (Move, Attack, Sprint) |
| **URP Graphics** | ✅ Settato | Rendering 3D moderno |
| **Systems Hierarchy** | ✅ Solida | Parent-child per combat systems |

### 🟡 Componenti da Modificare (20%)

| Componente | Problema | Modifica |
|------------|----------|----------|
| **UnitBase** | TakeDamage() vuoto | Implementare health, damage, poise |
| **Stats struct** | Solo 3 campi | Espandere a CombatStats (stamina, poise, defense) |
| **Helpers** | Minimal | Aggiungere utilities combat |

### 🔴 Componenti da Sostituire (20%)

| Componente | Motivo | Sostituzione |
|------------|--------|--------------|
| **ExampleGameManager** | Turn-based rigido | GameStateMachine real-time |
| **GameState enum** | Stati turn-based | HSM con combat substates |
| **HeroUnitBase** | OnMouseDown click-based | PlayerController con Update loop |
| **EnemyUnitBase** | Stub vuoto | EnemyController con AI |

---

## Gap Critici per Souls-Like

### ❌ BLOCCANTI (Week 1)

1. **No Update Loops**
   - Zero real-time processing
   - Nessuna logica frame-by-frame
   - **Azione**: Creare PlayerController con Update/FixedUpdate

2. **No Input Processing**
   - Input System configurato ma NON usato
   - Nessun callback handler
   - **Azione**: Implementare InputHandler con buffering

3. **Physics Insufficiente**
   - Solver iterations: 6/1 (troppo basso)
   - No collision layers configurati
   - **Azione**: Aumentare a 10-12, setup layer matrix

### ⚠️ IMPORTANTI (Week 2-3)

4. **No Animation System**
   - Nessun Animator Controller
   - No root motion, no events
   - **Azione**: Creare state machine animator

5. **No Combat System**
   - Hitbox/hurtbox assenti
   - Damage calculation non implementato
   - **Azione**: CombatSystem con hit detection

6. **No Camera System**
   - Camera follow assente
   - Lock-on non implementato
   - **Azione**: Cinemachine third-person + lock-on

### 🔵 DESIDERABILI (Week 4+)

7. **No Object Pooling**
   - Instantiate diretto
   - **Azione**: GenericObjectPool per VFX, enemies

8. **Audio Source Unico**
   - Solo 1 AudioSource per SFX
   - **Azione**: Audio source pool (10+ sources)

---

## Performance Analysis Summary

### Target Performance
- **FPS**: 60 stabile (16.67ms budget)
- **Input Latency**: <30ms
- **Parry Precision**: 200ms ±10ms

### Current State
- **GC Pressure**: ✅ Minima (codice pulito)
- **Update Loops**: ❌ Assenti
- **Physics Config**: ⚠️ Insufficiente per combat

### Raccomandazioni Chiave
1. Input in `Update()` per responsività
2. Movement in `FixedUpdate()` per physics
3. Camera in `LateUpdate()` per smooth follow
4. Continuous Collision Detection su player/weapons

---

## Architecture Analysis Summary

### Pattern Evaluation

| Pattern | Voto | Note |
|---------|------|------|
| Singleton Hierarchy | A | Production-ready |
| ScriptableObject Data | A | Ottimo per balancing |
| Turn-based FSM | F | Incompatibile, sostituire |
| Unit Inheritance | B- | Struttura ok, internals da rifare |
| Systems Hierarchy | A | Extend naturalmente |

### Recommended New Architecture

```
Systems (PersistentSingleton)
├── InputSystem
├── CombatSystem
├── StaminaSystem
├── LockOnSystem
├── AnimationSystem
├── VFXSystem
├── AudioSystem (existing)
└── ResourceSystem (existing)

GameStateMachine (Singleton)
├── Initialization
├── Gameplay
│   ├── Exploration
│   ├── Combat
│   │   ├── PlayerCombatState
│   │   └── EnemyCombatState
│   └── LockedOn
├── Pause
├── Victory
└── Death
```

---

## Risk Assessment (from Analysis)

### 🔴 High Risk
1. **Animation-Driven Combat** - Timing difficile da calibrare
2. **Parry Window** - Troppo facile o troppo difficile
3. **State Machine Complexity** - Rischio spaghetti code

### 🟡 Medium Risk
1. **Camera Collision** - Clip through walls
2. **Lock-On Switching** - Target selection frustrating
3. **Stamina Balance** - Troppo punitivo

### 🟢 Low Risk
1. Input handling (Input System maturo)
2. Audio system (già funzionale)
3. ScriptableObjects (pattern testato)

---

## Next Phase: Design

Con questa analisi completa, la **Fase 03: Design** dovrà definire:

1. **Hierarchical State Machine** architecture
2. **CombatStats** struct completa
3. **PlayerController** class design
4. **EnemyController** + AI design
5. **Hit Detection** system
6. **Parry/Dodge** timing implementation

---

## Context Loading

Per riprendere questa fase:
```
1. Leggi: current-state.md (questo file)
2. Se serve dettaglio: agent-*.md
3. Procedi a: ../03-design/
```
