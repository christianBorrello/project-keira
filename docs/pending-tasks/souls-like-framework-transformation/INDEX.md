# Pipeline Task: Souls-Like Framework Transformation

**Task Slug**: `souls-like-framework-transformation`
**Path**: `docs/pending-tasks/souls-like-framework-transformation/`
**Data Inizio**: 2025-12-15
**Data Completamento**: -

---

## Descrizione Task

Trasformare il framework Unity attuale (predisposto per giochi turn-based) in un framework per giochi 3D action RPG in stile souls-like, ispirati a:
- Lies of P
- Elden Ring
- Bloodborne
- Dark Souls series

---

## Stato Fasi

| Fase | Nome | Stato | Data Completamento |
|------|------|-------|-------------------|
| 00 | Setup | ✅ Completato | 2025-12-15 |
| 01 | Discovery | ✅ Completato | 2025-12-15 |
| 02 | Analysis | ✅ Completato | 2025-12-15 |
| 03 | Design | ✅ Completato | 2025-12-15 |
| 04 | Planning | ✅ Completato | 2025-12-15 |
| 05 | Implementation | 🔄 In Progress (90%) | - |
| 06 | Validation | ⏳ Pending | - |
| 07 | Documentation | ⏳ Pending | - |
| 08 | Delivery | ⏳ Pending | - |

---

## Fase Corrente

**Fase**: 05-Implementation
**Stato**: 🔄 In Progress (~90% code complete)
**Depth**: `--think`
**Last Checkpoint**: 2025-12-15

### Current Focus
- ✅ Input Actions enabled
- ✅ ScriptableObjects created (CombatConfigSO, WeaponDataSO, PlayerConfigSO, EnemyDataSO)
- Next: Test scene setup and prefab configuration

---

## Summary Cumulativo

### Requisiti Chiave (Fase 1 ✅)
- **Scope**: MVP Combat (player + 1 enemy type)
- **Style**: Lies of P-inspired (parry-focused, responsive)
- **Core Systems**: Movement, Stamina, Attack, Parry, Dodge, Lock-on, Health
- **Approach**: Script-based movement, Enum FSM, New Input System, Placeholder assets
- **Key Risk**: Parry timing must feel right (configurabile via ScriptableObject)

### Stato Attuale (Fase 2 ✅)
- **Reusability**: 60% riutilizzabile (Singleton, SO, Systems), 40% da riscrivere
- **Critical Gaps**: No Update loops, No Input processing, Physics insufficiente
- **Pattern Grade**: A (Singleton), A (ScriptableObject), F (Turn-based FSM)
- **Effort Estimate**: 4-6 settimane MVP combat
- **Key Systems Needed**: InputSystem, CombatSystem, StaminaSystem, LockOnSystem

### Decisioni Principali (Fase 3 ✅)
- **Architecture**: HSM + Systems (Option C) - massima separazione concerns, score 4.6/5
- **State Machine**: Enum FSM ibrido con IState interface
- **Movement**: Script-based (no root motion) per max responsiveness
- **Poise System**: Lies of P-style (accumulative, not threshold)
- **Combo System**: Array-based in ScriptableWeapon
- **Input**: New Input System + 150ms buffer window
- **Parry**: 200ms window (configurable via SO), perfect parry primi 100ms
- **Defense**: Single value per MVP, estensibile per damage types
- **Physics**: Solver 10/4 iterations, Continuous Dynamic detection
- **10 ADR documentate** in decisions.md

### Implementation Workflow (Fase 4 ✅)
- **Week 1**: Core Foundation (movement, state machine, input)
- **Week 2**: Combat Core (stamina, attacks, hitbox, damage)
- **Week 3**: Defensive Mechanics (parry, dodge, poise, stagger)
- **Week 4**: Enemy & Integration (AI, lock-on, death, polish)
- **Week 5-6**: Buffer & Validation (tuning, bugs, testing)
- **4 Milestones**: M1 Movement → M2 Attack → M3 Defense → M4 Combat
- **Risk Checkpoints**: Day 5, 10, 13, 18, 20

### Progress Implementation (Fase 5 🔄)
- **Week 1-4 Code**: ~90% complete (57 C# files implemented)
- **Core Systems**: ✅ InputHandler, CombatSystem, LockOnSystem, StaminaSystem, PoiseSystem
- **Player States**: ✅ All 9 states (Idle, Walk, Sprint, LightAttack, HeavyAttack, Parry, Dodge, Stagger, Death)
- **Enemy States**: ✅ All 6 states (Idle, Alert, Chase, Attack, Stagger, Death)
- **Input Actions**: ✅ All combat bindings enabled (HeavyAttack, Parry, Dodge, LockOn)
- **ScriptableObjects**: ✅ All 4 created (CombatConfigSO, WeaponDataSO, PlayerConfigSO, EnemyDataSO)
- **Gaps Remaining**:
  - ⏳ Default SO instances (create in Unity Editor)
  - ❌ Test scene setup and prefabs
  - ❌ Integration testing

---

## Context Loading Guide

Per riprendere questo task:
1. Leggi questo INDEX.md
2. Identifica "Fase Corrente"
3. Carica SOLO i file della fase corrente
4. Usa "Summary Cumulativo" per context delle fasi precedenti

---

## Quick Links

- [00-Setup](./00-setup/checklist.md)
- [01-Discovery](./01-discovery/)
- [02-Analysis](./02-analysis/)
- [03-Design](./03-design/)
- [04-Planning](./04-planning/)
- [05-Implementation](./05-implementation/)
- [06-Validation](./06-validation/)
- [07-Documentation](./07-documentation/)
- [08-Delivery](./08-delivery/)
