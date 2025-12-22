# Pipeline Task: Base Movement Redesign

**Task Slug**: `base-movement-redesign`
**Created**: 2025-12-19
**Path**: `docs/completed-tasks/base-movement-redesign/`
**Status**: ✅ Completed
**Implementation**: Features implemented in `kcc-character-system` task

---

## Summary

Riprogettazione del sistema di movimento base (senza target lock) per renderlo più naturale e realistico, ispirato a Ghost of Tsushima. Il sistema di lock-on è soddisfacente e non va modificato.

---

## Cumulative Summary

### Obiettivo
Creare un sistema di movimento fluido, naturale e responsive per il personaggio del giocatore quando non è in lock-on mode, seguendo i pattern di giochi moderni come Ghost of Tsushima.

### Stato Attuale (Analysis)
Pain points identificati in `MovementController.cs`:
- PP1: Linear smoothing (no acceleration curve) → partenza istantanea
- PP2: Decay troppo veloce (0.02s) → stop istantaneo, no inerzia
- PP3: Alignment factor riduce velocità ma no pivot animation
- PP4: Root motion sempre disabilitato
- PP5: Animator manca parametri angolo/velocità

### Requisiti Chiave (Discovery)
- **Feel**: 80% Responsive / 20% Realistico (Dark Souls 3 style)
- **Problema**: Manca peso/inerzia nel movimento attuale
- **Movimento**: Forward-based (non strafe) quando unlocked
- **Root Motion**: Parziale (turn-in-place, pivot = root motion; locomotion = codice)
- **Pivot**: Soft pivot (rallenta + curva dolcemente)
- **Animazioni**: Mixamo (idle, walk, run, sprint, turns, start/stop)

### Decisioni Chiave (Design)
- **ADR-001**: AnimationCurve per acceleration/deceleration (designer-friendly)
- **ADR-002**: Soft pivot via speed modulation (no stato separato)
- **ADR-003**: Turn-in-place con root motion parziale (solo rotazione)
- **ADR-004**: Lock-on path preservation (branch separato)
- **ADR-005**: Additive animator parameters (TurnAngle, VelocityMagnitude)

---

## Current Phase

| Phase | Status | Notes |
|-------|--------|-------|
| 00-Setup | ✅ Completed | Branch: feature/base-movement-redesign |
| 01-Discovery | ✅ Completed | Requirements documented |
| 02-Analysis | ✅ Completed | Current system documented |
| 03-Design | ✅ Completed | Architecture approved |
| 04-Planning | ✅ Completed | 16 tasks in 6 phases |
| 05-Implementation | ✅ Completed | Via `kcc-character-system` integration |
| 06-Validation | ✅ Completed | All 5 ADRs verified in KCC |
| 07-Documentation | ✅ Completed | See `kcc-character-system` docs |
| 08-Delivery | ✅ Completed | Features live in MovementController.cs |

**Note**: This task served as the **design specification** for movement features. The actual implementation was done as part of the `kcc-character-system` task, which used KCC (Kinematic Character Controller) as the physics foundation and integrated all ADR features on top.

---

## Context Loading Guide

### Phase 0 (Setup)
- Read: This file only

### Phase 1 (Discovery)
- Read: This file + `01-discovery/*`

### Phase 2 (Analysis)
- Read: This file + `02-analysis/current-state.md`
- Reference: `01-discovery/requirements.md` (summary only)

### Phase 3 (Design)
- Read: This file + `03-design/*`
- Reference: `02-analysis/current-state.md` (summary only)

### Phase 4 (Planning)
- Read: This file + `04-planning/workflow.md`
- Reference: `03-design/architecture.md` (summary only)

### Phase 5 (Implementation)
- Read: This file + `05-implementation/progress.md` + current phase file
- Reference: `04-planning/workflow.md` for task list

### Phase 6 (Validation)
- Read: This file + `06-validation/*`
- Reference: `01-discovery/requirements.md` for acceptance criteria

### Phase 7 (Documentation)
- Read: This file + `07-documentation/*`

### Phase 8 (Delivery)
- Read: This file + `08-delivery/*`

---

## Key Files

### Project Files (to modify)
- `Assets/_Scripts/Player/Components/MovementController.cs` - Main movement logic
- `Assets/_Scripts/Player/States/PlayerIdleState.cs`
- `Assets/_Scripts/Player/States/PlayerWalkState.cs`
- `Assets/_Scripts/Player/States/PlayerRunState.cs`
- `Assets/_Scripts/Player/States/PlayerSprintState.cs`
- `Assets/_Scripts/Player/PlayerAnimatorController.cs`
- Animator Controller (Unity asset)

### Reference Files
- `Assets/claudedocs/GAMEDEV_CODING_PATTERNS.md` - Coding patterns to follow

---

## Blockers

*(none currently)*

---

## Checkpoints

| Checkpoint | Date | Description |
|------------|------|-------------|
| Setup | 2025-12-19 | Initial structure created |

