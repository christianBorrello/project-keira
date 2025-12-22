# Risks: KCC Character System

## High Priority Risks

### RISK-001: Animation Timing Desync
**Probability**: Medium
**Impact**: High
**Category**: Technical

KCC simula in FixedUpdate con interpolation, mentre le animazioni girano in Update. Potenziale desync tra movimento e animazioni.

**Mitigation**:
1. Usare KCC's built-in interpolation (Settings.Interpolate = true)
2. Animator speed matching già implementato in AnimationController
3. Test intensivo durante integration

**Contingency**: Se desync visibile, investigare custom interpolation layer.

### RISK-002: Combat System Regression
**Probability**: Medium
**Impact**: High
**Category**: Integration

Cambiare il sistema di movimento potrebbe rompere il combat esistente (stagger, dodge, attack movement).

**Mitigation**:
1. Implementare external forces system prima di combat integration
2. Mantenere API simile (IsGrounded, velocity access)
3. Test ogni combat state dopo integration

**Contingency**: Combat states possono bypassare KCC temporaneamente se necessario.

### RISK-003: Lock-On Orbital Drift
**Probability**: Medium
**Impact**: Medium
**Category**: Technical

Convertire lock-on da position-based a velocity-based potrebbe causare drift dalla distanza target.

**Mitigation**:
1. Distance correction logic (già esistente in MovementController)
2. Test strafe puro per verificare distance maintenance
3. Tolerance configurabile

**Contingency**: Hybrid approach - velocity per movimento, position correction per strafe puro.

---

## Medium Priority Risks

### RISK-004: Feel Difference
**Probability**: Low
**Impact**: Medium
**Category**: Design

Il feel del movimento potrebbe risultare diverso nonostante le stesse curve, per via di come KCC gestisce ground snapping e collision.

**Mitigation**:
1. Side-by-side comparison con sistema precedente
2. Curve tuning dopo integration
3. User validation del feel

**Contingency**: Adjust KCC settings (GroundDetectionExtraDistance, etc.)

### RISK-005: Performance Overhead
**Probability**: Low
**Impact**: Medium
**Category**: Performance

KCC potrebbe avere overhead maggiore del CharacterController base.

**Mitigation**:
1. Profile prima e dopo
2. KCC è ottimizzato e usato in produzioni AAA
3. Evitare allocations nei callback

**Contingency**: KCC ha opzioni per disabilitare features non necessarie.

### RISK-006: Turn-In-Place Root Motion Conflict
**Probability**: Medium
**Impact**: Low
**Category**: Technical

Root motion rotation durante turn-in-place potrebbe confliggere con UpdateRotation callback.

**Mitigation**:
1. Skip UpdateRotation quando IsTurningInPlace
2. Lasciar fare ad AnimationController con OnAnimatorMove
3. Clear handoff protocol

**Contingency**: Fallback a script-only rotation (no root motion).

---

## Low Priority Risks

### RISK-007: Moving Platform Future Complexity
**Probability**: Low (non implementato ora)
**Impact**: Low
**Category**: Extensibility

Aggiungere moving platforms in futuro potrebbe richiedere refactoring.

**Mitigation**:
1. KCC ha built-in PhysicsMover support
2. Non fare scelte che precludono future integration
3. Document integration path

**Contingency**: KCC documentation copre PhysicsMover integration.

---

## Risk Matrix

| Risk | Probability | Impact | Priority |
|------|-------------|--------|----------|
| RISK-001 Animation Desync | Medium | High | 🔴 High |
| RISK-002 Combat Regression | Medium | High | 🔴 High |
| RISK-003 Orbital Drift | Medium | Medium | 🟡 Medium |
| RISK-004 Feel Difference | Low | Medium | 🟡 Medium |
| RISK-005 Performance | Low | Medium | 🟢 Low |
| RISK-006 Root Motion | Medium | Low | 🟢 Low |
| RISK-007 Moving Platform | Low | Low | 🟢 Low |
