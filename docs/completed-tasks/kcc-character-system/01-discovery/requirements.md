# Requirements: KCC Character System

## Functional Requirements

### FR-001: KCC Core Integration
**Priority**: P0 (Must Have)
**Source**: User requirement

Il sistema deve utilizzare `KinematicCharacterMotor` come fondazione per il movimento del personaggio.

**Acceptance Criteria**:
- [ ] PlayerController usa KinematicCharacterMotor invece di CharacterController
- [ ] MovementController implementa ICharacterController
- [ ] Ground detection usa KCC (GroundingStatus.IsStableOnGround)
- [ ] Collision resolution gestita da KCC

### FR-002: Momentum System (Curves)
**Priority**: P0 (Must Have)
**Source**: ADR-001 da base-movement-redesign

Il movimento deve avere acceleration/deceleration curve-based per un feel pesante ma responsive.

**Acceptance Criteria**:
- [ ] AnimationCurve per accelerazione (default: 80% in 0.1s)
- [ ] AnimationCurve per decelerazione (default: smooth stop in 0.15s)
- [ ] Curve editabili in Inspector
- [ ] Feel: 80% responsive / 20% realistico

### FR-003: Soft Pivot System
**Priority**: P1 (Should Have)
**Source**: ADR-002 da base-movement-redesign

Riduzione velocità durante cambi di direzione significativi (>60°).

**Acceptance Criteria**:
- [ ] Speed reduction quando turn angle > pivotAngleThreshold
- [ ] Pivot factor configurabile (default: 0.4 a 180°)
- [ ] No stato separato (modulation continua)

### FR-004: Turn-In-Place
**Priority**: P2 (Could Have)
**Source**: ADR-003 da base-movement-redesign

Rotazione sul posto quando fermo e input > soglia angolare.

**Acceptance Criteria**:
- [ ] Trigger quando velocity < threshold E turn angle > 45°
- [ ] Root motion rotation (position da script)
- [ ] Exit quando angolo residuo < 10°
- [ ] Cancellabile per combat interrupt

### FR-005: Lock-On Orbital Movement
**Priority**: P0 (Must Have)
**Source**: ADR-004 da base-movement-redesign

Movimento orbitale attorno al target quando in lock-on mode.

**Acceptance Criteria**:
- [ ] Strafe sinistro/destro mantiene distanza dal target
- [ ] Approach/retreat funzionano correttamente
- [ ] Character sempre facing target
- [ ] Distance maintenance durante strafe puro

### FR-006: External Forces System
**Priority**: P0 (Must Have)
**Source**: Combat system requirements

Supporto per forze esterne (knockback, esplosioni, spinte).

**Acceptance Criteria**:
- [ ] API per aggiungere forze con durata e decay
- [ ] API per impulsi istantanei
- [ ] Forze additive alla velocity in UpdateVelocity()
- [ ] Integrazione con HealthPoiseController (stagger)

### FR-007: Animator Integration
**Priority**: P0 (Must Have)
**Source**: ADR-005 da base-movement-redesign

Parametri animator per blend tree e transizioni.

**Acceptance Criteria**:
- [ ] Speed (0-2) per locomotion blend
- [ ] TurnAngle (-180 to 180) per turn detection
- [ ] VelocityMagnitude per transition conditions
- [ ] MoveX/MoveY per lock-on strafe animations

---

## Non-Functional Requirements

### NFR-001: Performance
- Frame budget: < 0.5ms per character update
- No allocations in hot path (UpdateVelocity, UpdateRotation)
- Use cached hashes per animator parameters

### NFR-002: Maintainability
- Separation of concerns: KCC motor vs custom logic
- Configuration via SerializeField (designer-friendly)
- Clear API boundaries

### NFR-003: Extensibility
- Preparare architettura per moving platforms (non implementare)
- Supportare future capsule resize (non implementare)

---

## Implementation Order (Incremental)

| Phase | Feature | Dependencies | Validation |
|-------|---------|--------------|------------|
| 1 | KCC Core + Gravity | None | Character moves with WASD |
| 2 | Momentum Curves | Phase 1 | Visible acceleration/deceleration |
| 3 | Animator Integration | Phase 2 | Blend tree responds correctly |
| 4 | Soft Pivot | Phase 2 | Speed reduction on turns |
| 5 | Lock-On Orbital | Phase 1-3 | Strafe around target works |
| 6 | External Forces | Phase 1 | Knockback works |
| 7 | Turn-In-Place | Phase 3-4 | Root motion turn from idle |

---

## Out of Scope (for now)

- Moving platforms (PhysicsMover) - architettura pronta, non implementato
- Crouch/capsule resize
- Swimming/climbing
- Networked movement prediction
