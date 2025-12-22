# Risks: Souls-Like Framework Transformation

**Data**: 2025-12-15

---

## Risk Register

| ID | Risk | Probability | Impact | Severity | Mitigation |
|----|------|-------------|--------|----------|------------|
| R1 | Parry timing feels wrong | Medium | High | 🔴 High | Configurabile via SO, playtesting iterativo |
| R2 | Animation without root motion feels floaty | Medium | Medium | 🟡 Medium | Script-based con tuning velocità/accelerazione |
| R3 | Lock-on camera fights player control | Medium | Medium | 🟡 Medium | Cinemachine con damping, test early |
| R4 | Stamina balance too punishing/generous | Low | Medium | 🟢 Low | Tutti i valori in ScriptableObject |
| R5 | State machine spaghetti code | Medium | High | 🔴 High | Clean state transitions, documentation |
| R6 | i-frames feel inconsistent | Medium | High | 🔴 High | FixedUpdate per physics, clear visual feedback |
| R7 | Hitbox/Hurtbox issues | Medium | Medium | 🟡 Medium | Debug visualization, careful collider setup |
| R8 | Input lag perceived | Low | High | 🟡 Medium | Process input in Update, actions in FixedUpdate |

---

## Risk Analysis

### R1: Parry Timing (🔴 High Severity)

**Problema**: Il sistema parry è il cuore del gameplay Lies of P. Se il timing non "sente" giusto, tutto il combat fallisce.

**Mitigations**:
1. Perfect parry window configurabile (200ms default)
2. Partial parry come fallback
3. Visual/audio feedback immediato
4. Input buffer per parry

### R5: State Machine Complexity (🔴 High Severity)

**Problema**: FSM enum-based può diventare spaghetti code con molti stati (Idle, Walk, Run, Attack, Dodge, Parry, Stagger, Death...)

**Mitigations**:
1. Documentare TUTTE le transizioni valide
2. Transition guards chiari
3. Single source of truth per stato corrente
4. Consider upgrade a class-based se necessario

### R6: i-frames Inconsistency (🔴 High Severity)

**Problema**: Se i-frames non sono consistenti, il dodge diventa inaffidabile e frustrante.

**Mitigations**:
1. i-frames gestiti in FixedUpdate (determinismo)
2. Layer collision per ignorare damage durante i-frames
3. Clear visual cue per i-frame window
4. Testing su frame rate variabili

---

## Risk Monitoring

Durante implementazione, verificare:
- [ ] Parry window feels responsive (R1)
- [ ] Movement non sembra floaty (R2)
- [ ] Camera non combatte input (R3)
- [ ] Code remains clean (R5)
- [ ] Dodge è reliable (R6)

---

## Context Loading

Per riprendere: Questo file è per reference, non critico per context loading.
