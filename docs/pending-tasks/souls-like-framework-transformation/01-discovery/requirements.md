# Requirements: Souls-Like Framework Transformation

**Data**: 2025-12-15
**Depth**: `--think-hard` 🔴
**Stile Riferimento**: Lies of P (semplificato)

---

## Executive Summary

Trasformare il framework turn-based esistente in un framework action RPG souls-like con focus su:
- **Responsività**: Sistema di combattimento reattivo e preciso
- **Core Mechanics**: Movimento, attacco, parry, stamina, dodge
- **Scalabilità**: Base solida per future espansioni

---

## Scope: MVP Combat

Il primo deliverable include SOLO:
- Player con movimento 3D
- Sistema stamina
- Attacco base
- Parry/Deflect con timing
- Dodge/Roll con i-frames
- Lock-on targeting
- UN tipo di nemico base

**ESCLUSO** da MVP:
- Sistema progressione (stats, level up)
- Inventory
- Multiple weapon types
- UI complessa
- Boss fights
- Multiplayer

---

## Functional Requirements

### FR-001: Player Movement
**Priority**: 🔴 Critical
**Approach**: Script-based (massima responsività)

| Movimento | Descrizione |
|-----------|-------------|
| Walk | Movimento 8-direzionale con velocità base |
| Run/Sprint | Movimento veloce con consumo stamina |
| Strafe | Movimento laterale durante lock-on |
| Rotation | Rotazione fluida verso direzione movimento |

**Acceptance Criteria**:
- [ ] Player si muove in 8 direzioni
- [ ] Sprint consuma stamina
- [ ] Movimento smooth senza input lag percepibile
- [ ] Camera segue player (Cinemachine o custom)

---

### FR-002: Stamina System
**Priority**: 🔴 Critical

| Azione | Costo Stamina | Note |
|--------|---------------|------|
| Sprint | X/sec | Consumo continuo |
| Dodge | Y | Costo fisso |
| Attack | Z | Per attacco |
| Parry | 0 | Gratuito (come Lies of P) |

**Acceptance Criteria**:
- [ ] Barra stamina visibile (UI placeholder)
- [ ] Rigenerazione automatica quando non in uso
- [ ] Azioni bloccate se stamina insufficiente
- [ ] Delay prima della rigenerazione dopo azione

---

### FR-003: Combat - Basic Attack
**Priority**: 🔴 Critical

**Combo System (semplificato)**:
- Light Attack (R1/RB): Attacco veloce, basso danno
- Heavy Attack (R2/RT): Attacco lento, alto danno, più stamina

**Acceptance Criteria**:
- [ ] Light attack eseguibile
- [ ] Heavy attack eseguibile
- [ ] Attacchi consumano stamina
- [ ] Hitbox attiva solo durante finestra di attacco
- [ ] Può colpire nemici e applicare danno

---

### FR-004: Parry/Deflect System
**Priority**: 🔴 Critical
**Riferimento**: Lies of P (parry-focused combat)

| Timing | Risultato |
|--------|-----------|
| Perfect Parry | Stagger nemico, no damage, bonus window |
| Partial Parry | Reduced damage, no stagger |
| Miss | Full damage |

**Parry Window**: ~200ms (configurabile via ScriptableObject)

**Acceptance Criteria**:
- [ ] Input parry (L1/LB)
- [ ] Perfect parry detectabile
- [ ] Partial parry detectabile
- [ ] Feedback visivo/audio per parry success
- [ ] Nemico può essere staggerato

---

### FR-005: Dodge/Roll System
**Priority**: 🔴 Critical

| Proprietà | Valore |
|-----------|--------|
| i-frames | ~300ms (configurabile) |
| Stamina cost | Fisso |
| Recovery | ~500ms |
| Direction | Verso input o backward |

**Acceptance Criteria**:
- [ ] Dodge eseguibile con input (Circle/B)
- [ ] i-frames attivi durante dodge
- [ ] Dodge consuma stamina
- [ ] Direction basata su input
- [ ] Recovery time prima di next action

---

### FR-006: Lock-On Targeting
**Priority**: 🟡 High

**Behavior**:
- Toggle lock-on con input (R3/RS)
- Camera focus sul target
- Player strafe durante lock-on
- Auto-switch su target death
- Max range per lock-on

**Acceptance Criteria**:
- [ ] Lock-on toggle funzionante
- [ ] Camera ruota verso target
- [ ] Player affronta sempre il target
- [ ] Switch target (opzionale per MVP)
- [ ] Lock-on si disattiva se target troppo lontano

---

### FR-007: Enemy Base
**Priority**: 🔴 Critical

**Enemy MVP**:
- Health pool
- Basic attack pattern
- Attackable (riceve danno)
- Death state
- Attacks can be parried

**Acceptance Criteria**:
- [ ] Enemy spawnable
- [ ] Enemy ha health
- [ ] Enemy può attaccare (pattern semplice)
- [ ] Enemy può essere colpito
- [ ] Enemy muore quando health = 0
- [ ] Attack del nemico è parryabile

---

### FR-008: Health System
**Priority**: 🔴 Critical

| Entity | Health Source |
|--------|---------------|
| Player | Stats (configurabile) |
| Enemy | ScriptableObject |

**Acceptance Criteria**:
- [ ] Player ha health
- [ ] Enemy ha health
- [ ] Damage riduce health
- [ ] Death trigger a health 0
- [ ] Health bar visibile (placeholder UI)

---

## Non-Functional Requirements

### NFR-001: Responsiveness
- Input-to-action latency < 50ms
- No perceived lag on parry timing
- Smooth 60 FPS target

### NFR-002: Configurability
- Timing windows via ScriptableObject
- Damage values via ScriptableObject
- Stamina costs via ScriptableObject

### NFR-003: Extensibility
- State machine deve supportare nuovi stati
- Combat system deve supportare nuove armi
- Enemy base deve supportare inheritance

### NFR-004: Code Quality
- Clean Architecture principles
- SOLID compliance
- Testable design (dove possibile in Unity)

---

## Technical Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Movement | Script-based | Massima responsività per parry timing |
| Input | New Input System | Già nel progetto, rebinding support |
| State Machine | Enum-based FSM | Semplice, sufficiente per MVP |
| Assets | Placeholder | Capsule/Cube, focus su meccaniche |
| Camera | TBD | Cinemachine o custom |

---

## Future Scope (Post-MVP)

Questi elementi sono **ESCLUSI** dal MVP ma previsti per future iterazioni:

1. **Progression System** (Hybrid: stats + weapon upgrade + skills)
2. **Weapon Assembly** (Lies of P style - blade + handle)
3. **Multiple Enemy Types**
4. **Boss Fights**
5. **Inventory System**
6. **UI Polish**
7. **Save/Load System
8. **Audio/VFX Polish**

---

## Context Loading

Per riprendere questa fase:
```
Leggi: requirements.md (questo file)
Poi: constraints.md, risks.md
```
