# Milestones: Souls-Like Framework MVP

**Data**: 2025-12-15
**Fase**: 04-Planning

---

## Milestone Overview

```
M1 ──▶ M2 ──▶ M3 ──▶ M4 ──▶ MVP
│      │      │      │      │
Day 5  Day 10 Day 15 Day 20 Day 28
```

---

## M1: Player Movement (Day 5)

### Definition
**"Player capsule si muove in scena test con state machine funzionante"**

### Acceptance Criteria
- [ ] Player prefab (capsule) spawna in scena
- [ ] WASD/Left Stick muove player
- [ ] Shift/L3 attiva sprint
- [ ] State transitions visibili in Inspector (Idle ↔ Walk ↔ Sprint)
- [ ] Stamina UI placeholder mostra valore
- [ ] No errori in console
- [ ] Rotation smooth verso direzione movimento

### Deliverables
| Deliverable | Path |
|-------------|------|
| CombatStats.cs | `Assets/_Scripts/Combat/Data/` |
| Interfaces (4) | `Assets/_Scripts/Combat/Interfaces/` |
| InputHandler.cs | `Assets/_Scripts/Combat/Systems/` |
| PlayerStateMachine.cs | `Assets/_Scripts/Player/` |
| Locomotion States (3) | `Assets/_Scripts/Player/States/` |
| Test Scene | `Assets/Scenes/CombatTest.unity` |
| Player Prefab | `Assets/Prefabs/Player.prefab` |

### Validation
- [ ] 30 secondi di gameplay registrato
- [ ] Screenshot state machine in Inspector
- [ ] Console pulita (no errors/warnings)

### Exit Criteria
Movement feel responsive e paragonabile a souls-like reference.

---

## M2: Attack System (Day 10)

### Definition
**"Player può attaccare dummy target e vedere damage numbers"**

### Acceptance Criteria
- [ ] R1/LMB esegue light attack
- [ ] R2/RMB esegue heavy attack
- [ ] Attack ha fasi visibili (startup, active, recovery)
- [ ] Hitbox debug gizmos visibili
- [ ] Dummy target riceve danno
- [ ] Damage calculation correct (base * multiplier - defense)
- [ ] Stamina consumata per ogni attack
- [ ] No attack se stamina insufficiente

### Deliverables
| Deliverable | Path |
|-------------|------|
| StaminaSystem.cs | `Assets/_Scripts/Combat/Systems/` |
| CombatSystem.cs | `Assets/_Scripts/Combat/Systems/` |
| ScriptableObjects (3) | `Assets/_Scripts/Scriptables/` |
| Attack States (2) | `Assets/_Scripts/Player/States/` |
| Hitbox.cs | `Assets/_Scripts/Combat/Hitbox/` |
| Hurtbox.cs | `Assets/_Scripts/Combat/Hitbox/` |
| PlayerController.cs | `Assets/_Scripts/Player/` |
| Dummy Target Prefab | `Assets/Prefabs/DummyTarget.prefab` |

### Validation
- [ ] Video di light attack chain
- [ ] Video di heavy attack
- [ ] Screenshot hitbox gizmos
- [ ] Console log di damage dealt

### Exit Criteria
Attacks feel impactful, timing configurabile via ScriptableObject.

---

## M3: Defensive Mechanics (Day 15)

### Definition
**"Player può parry, dodge, e sistema poise funzionante"**

### Acceptance Criteria
- [ ] L1 attiva parry stance
- [ ] Parry window di 200ms funziona
- [ ] Perfect parry (primi 100ms) distinguibile
- [ ] Circle/Space esegue dodge roll
- [ ] i-frames di 300ms durante dodge
- [ ] Dodge nella direzione input
- [ ] Poise accumulation su hit ricevuti
- [ ] Poise break → stagger state
- [ ] Stagger recovery time configurabile

### Deliverables
| Deliverable | Path |
|-------------|------|
| PlayerParryState.cs | `Assets/_Scripts/Player/States/` |
| PlayerDodgeState.cs | `Assets/_Scripts/Player/States/` |
| PlayerStaggerState.cs | `Assets/_Scripts/Player/States/` |
| Poise logic in CombatSystem | Update existing |
| ParryTiming struct | `Assets/_Scripts/Combat/Data/` |

### Validation
- [ ] Frame-by-frame video parry window
- [ ] Video i-frames test (hit durante dodge = no damage)
- [ ] Video poise break → stagger
- [ ] Timing spreadsheet (actual vs expected)

### Exit Criteria
Parry timing feels fair but challenging. Dodge feels responsive.

---

## M4: Enemy Combat (Day 20)

### Definition
**"Player vs Enemy combat loop completo e funzionante"**

### Acceptance Criteria
- [ ] Enemy prefab (cube/capsule) spawna
- [ ] Enemy detecta player in range
- [ ] Enemy chase player
- [ ] Enemy attack con telegraph
- [ ] Player può parry enemy attacks
- [ ] Player può dodge enemy attacks
- [ ] Enemy poise break → stagger
- [ ] Lock-on targeting funziona
- [ ] Sia player che enemy possono morire
- [ ] Death state blocca input

### Deliverables
| Deliverable | Path |
|-------------|------|
| EnemyController.cs | `Assets/_Scripts/Enemies/` |
| EnemyStateMachine.cs | `Assets/_Scripts/Enemies/` |
| Enemy States (5) | `Assets/_Scripts/Enemies/States/` |
| EnemyDataSO.cs | `Assets/_Scripts/Scriptables/` |
| LockOnSystem.cs | `Assets/_Scripts/Combat/Systems/` |
| PlayerDeathState.cs | `Assets/_Scripts/Player/States/` |
| EnemyDeathState.cs | `Assets/_Scripts/Enemies/States/` |
| Enemy Prefab | `Assets/Prefabs/BasicEnemy.prefab` |

### Validation
- [ ] Video full combat loop (60 secondi)
- [ ] Player kills enemy
- [ ] Enemy kills player
- [ ] Parry su enemy attack
- [ ] Dodge su enemy attack
- [ ] Lock-on demonstration

### Exit Criteria
Combat loop feels complete. Winnable e losable.

---

## MVP Complete (Day 28)

### Definition
**"MVP Combat validato, polished, e documentato"**

### Final Acceptance Criteria

#### Functional Requirements
- [ ] Player movement (walk, sprint)
- [ ] Player combat (light, heavy attack)
- [ ] Player defense (parry, dodge)
- [ ] Stamina system con consumption e regen
- [ ] Poise system con break e stagger
- [ ] Enemy AI (detect, chase, attack)
- [ ] Lock-on targeting
- [ ] Health system (damage, death)

#### Non-Functional Requirements
- [ ] 60 FPS stabile
- [ ] Input latency < 30ms
- [ ] No memory leaks
- [ ] Console pulita

#### Documentation
- [ ] Code comments XML
- [ ] README updated
- [ ] ScriptableObject tooltips

### Final Deliverables
| Deliverable | Description |
|-------------|-------------|
| Complete codebase | All scripts in `Assets/_Scripts/` |
| Test scene | `CombatTest.unity` con setup completo |
| Prefabs | Player, Enemy, DummyTarget |
| ScriptableObjects | Default configs |
| Documentation | Code docs, README |

### Validation Protocol
1. **Playtest Session**: 15 minuti di combat senza crash
2. **Timing Validation**: Parry/dodge frame analysis
3. **Balance Check**: Player può vincere con skill
4. **Performance Profile**: Stable 60 FPS
5. **Code Review**: No TODOs, no placeholder code

---

## Milestone Tracking Template

```markdown
## Milestone [N] Status

**Target Date**: YYYY-MM-DD
**Actual Date**: YYYY-MM-DD
**Status**: [ ] On Track / [ ] At Risk / [ ] Blocked / [ ] Complete

### Completed Tasks
- [x] Task 1
- [x] Task 2

### In Progress
- [ ] Task 3 (80%)
- [ ] Task 4 (50%)

### Blockers
- Blocker description (owner, action needed)

### Notes
- Additional observations
```

---

## Risk Mitigation Per Milestone

### M1 Risks
| Risk | Mitigation |
|------|------------|
| Input System learning curve | Reference existing Input Actions |
| State machine bugs | Extensive logging, visual debugging |

### M2 Risks
| Risk | Mitigation |
|------|------------|
| Hitbox unreliable | Increase physics iterations, debug gizmos |
| Timing feels off | Expose all values in ScriptableObject |

### M3 Risks
| Risk | Mitigation |
|------|------------|
| Parry too hard/easy | 200ms baseline, tune from there |
| i-frames inconsistent | Continuous collision detection |

### M4 Risks
| Risk | Mitigation |
|------|------------|
| AI too dumb/smart | Simple patterns, configurable aggression |
| Lock-on camera issues | Use Cinemachine FreeLook base |

---

## Context Loading

Per riprendere i milestones:
```
1. Leggi: milestones.md (questo file)
2. Identifica milestone corrente
3. Reference: workflow.md per task dettagliati
4. Track progress con template sopra
```
