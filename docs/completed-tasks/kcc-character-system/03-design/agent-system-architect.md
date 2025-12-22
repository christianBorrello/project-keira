# Agent Report: System Architect - KCC Architecture Design

## Summary

Design completo dell'architettura KCC-based character system con 6 ADRs e implementation skeleton.

---

## Key Decisions Made

### ADR-KCC-001: MovementIntent
**Decision**: Struct con timestamp per caching input Update→FixedUpdate
- 48 bytes, zero GC
- Pre-calcolo WorldDirection
- Stale detection (>100ms)

### ADR-KCC-002: External Forces
**Decision**: Priority-based ForceInstance system
- 3 modes: Instant, Impulse, Continuous
- Priority override (stagger > knockback)
- Pre-allocated list (8 elementi)

### ADR-KCC-003: Ground State
**Decision**: Wrapper properties
- `IsGrounded`, `GroundNormal`, `GroundAngle`
- Encapsulation di KCC internals
- API compatibile con codice esistente

### ADR-KCC-004: Turn-In-Place
**Decision**: Flag check diretto
- `if (_animationController.IsTurnInPlaceActive) return;`
- Fallback safe
- Debuggable

### ADR-KCC-005: Interface Implementation
**Decision**: 5 callback con logica, 4 stubs
- Active: Before, UpdateVelocity, UpdateRotation, PostGrounding, After
- Stubs: Collision callbacks per espansione futura

### ADR-KCC-006: Lock-On Conversion
**Decision**: Velocity + position correction
- Velocity-based per smooth movement
- Soft correction per distance maintenance

---

## Architecture Highlights

### Component Diagram
```
InputHandler → PlayerState → MovementController.ApplyMovement()
                                      ↓ cache
                              MovementIntent
                                      ↓
                    KCC FixedUpdate callbacks
                                      ↓
              UpdateVelocity() + UpdateRotation()
```

### Data Flow
1. **Update**: Input → Intent (cached with timestamp)
2. **FixedUpdate**: KCC chiama callbacks
3. **BeforeCharacterUpdate**: Validate, update forces
4. **UpdateVelocity**: Momentum/Lock-on + Forces + Gravity
5. **UpdateRotation**: Skip if turn-in-place
6. **PostGroundingUpdate**: Animator params
7. **AfterCharacterUpdate**: Cleanup

---

## Risk Assessment

| Decision | Risk Level | Mitigation |
|----------|------------|------------|
| Intent caching | Low | Stale detection |
| Force system | Low | Priority override |
| Ground wrappers | Low | Fallback null-safe |
| Turn handoff | Low | Flag check simple |
| Lock-on conversion | Medium | Testing, tolerance tuning |

**Overall Risk**: LOW - Design è conservativo e testabile.

---

## Implementation Ready

Tutti i design sono pronti per implementazione:
- Struct definitions complete
- Interface skeleton definito
- Migration checklist creata
- 7 fasi incrementali identificate
