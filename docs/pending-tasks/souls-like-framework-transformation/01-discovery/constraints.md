# Constraints: Souls-Like Framework Transformation

**Data**: 2025-12-15

---

## Technical Constraints

### TC-001: Unity Version
- Progetto esistente Unity (versione da verificare in Fase 2)
- Universal Render Pipeline (URP) presente

### TC-002: Input System
- **Must use**: New Input System (già nel progetto)
- Supporto controller e keyboard/mouse

### TC-003: Assets
- **Constraint**: Nessun asset 3D custom disponibile
- **Mitigation**: Placeholder (Capsule, Cube) per MVP
- Animazioni: Blend tree con animazioni base o nessuna inizialmente

### TC-004: Existing Codebase
- Framework turn-based esistente da trasformare
- Alcuni pattern riutilizzabili (Singleton hierarchy, ScriptableObjects)
- GameState machine esistente ma non adatta (enum-based turn logic)

---

## Resource Constraints

### RC-001: Development
- Solo developer (presunto)
- Focus su meccaniche, non su polish visivo

### RC-002: Assets
- No budget per asset store (da confermare)
- Placeholder-first approach

---

## Design Constraints

### DC-001: Scope Limitation
- MVP = Combat core only
- No progression, inventory, save/load in prima iterazione
- Un solo tipo di nemico

### DC-002: Complexity Budget
- Enum-based FSM (no complex state machines)
- ScriptableObject per configurazione (no database)
- Minimal UI (solo health/stamina bars)

---

## Performance Constraints

### PC-001: Target FPS
- 60 FPS stabile (critico per timing parry)
- No frame drops durante combat

### PC-002: Platform
- PC primary (presunto)
- Controller support required

---

## Context Loading

Per riprendere: Questo file è secondario, leggi requirements.md prima.
