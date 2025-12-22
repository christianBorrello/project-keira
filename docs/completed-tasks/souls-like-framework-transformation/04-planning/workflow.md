# Implementation Workflow: Souls-Like Framework MVP

**Data**: 2025-12-15
**Fase**: 04-Planning
**Effort Totale Stimato**: 4-6 settimane

---

## Executive Summary

Questo documento definisce il workflow di implementazione per trasformare Project Keira in un framework souls-like MVP. L'implementazione segue un approccio **bottom-up**: prima infrastructure e data, poi systems, poi entities, infine integration.

**Principio Guida**: Ogni task produce codice funzionante e testabile. No placeholder, no TODO comments.

---

## Week 1: Core Foundation

### Milestone: "Player si muove e può attaccare"

#### W1.1 - Infrastructure Setup (Day 1)
**Effort**: 4h | **Priority**: CRITICAL | **Dependencies**: None

| Task | File | Description |
|------|------|-------------|
| W1.1.1 | Physics Settings | Solver 10/4, layers setup |
| W1.1.2 | Input Actions | Verify/extend existing actions |
| W1.1.3 | Scene Setup | Test scene con ground plane |
| W1.1.4 | Player Prefab | Capsule + Rigidbody + components |

**Acceptance Criteria**:
- [ ] Physics layers configurati (Player, Enemy, Weapon, Hitbox)
- [ ] Input Actions verificate (Move, Attack, Parry, Dodge, Sprint, LockOn)
- [ ] Test scene con lighting base
- [ ] Player prefab spawna in scena

**Blockers potenziali**: Input System conflicts con legacy input

---

#### W1.2 - Data Structures (Day 1-2)
**Effort**: 6h | **Priority**: CRITICAL | **Dependencies**: None

| Task | File | Description |
|------|------|-------------|
| W1.2.1 | `CombatStats.cs` | Struct completa con factory methods |
| W1.2.2 | `DamageInfo.cs` | Struct per damage data |
| W1.2.3 | `AttackData.cs` | Struct per attack configuration |
| W1.2.4 | `InputData.cs` | Struct per input snapshot |

**Acceptance Criteria**:
- [ ] CombatStats.CreateDefaultPlayer() funziona
- [ ] CombatStats.CreateDefaultEnemy() funziona
- [ ] Structs compilano senza warning
- [ ] Unit test per factory methods

**File Path**: `Assets/_Scripts/Combat/Data/`

---

#### W1.3 - Interfaces (Day 2)
**Effort**: 4h | **Priority**: CRITICAL | **Dependencies**: W1.2

| Task | File | Description |
|------|------|-------------|
| W1.3.1 | `IState.cs` | State machine interface |
| W1.3.2 | `ICombatant.cs` | Combat participant interface |
| W1.3.3 | `IDamageable.cs` | Damage receiver interface |
| W1.3.4 | `ILockOnTarget.cs` | Lock-on target interface |

**Acceptance Criteria**:
- [ ] Interfaces compilano
- [ ] XML documentation completa
- [ ] No circular dependencies

**File Path**: `Assets/_Scripts/Combat/Interfaces/`

---

#### W1.4 - Input Handler (Day 2-3)
**Effort**: 8h | **Priority**: CRITICAL | **Dependencies**: W1.1, W1.2

| Task | File | Description |
|------|------|-------------|
| W1.4.1 | `InputHandler.cs` | Input System wrapper |
| W1.4.2 | Input buffering | Queue con 150ms window |
| W1.4.3 | Movement input | Vector2 processing |
| W1.4.4 | Action input | Button press detection |

**Acceptance Criteria**:
- [ ] Move input letto correttamente (WASD + gamepad)
- [ ] Attack input buffered
- [ ] Sprint hold detection
- [ ] Debug UI mostra input state

**File Path**: `Assets/_Scripts/Combat/Systems/InputHandler.cs`

---

#### W1.5 - Player State Machine Base (Day 3-4)
**Effort**: 8h | **Priority**: CRITICAL | **Dependencies**: W1.3

| Task | File | Description |
|------|------|-------------|
| W1.5.1 | `PlayerState.cs` | Enum definition |
| W1.5.2 | `PlayerStateMachine.cs` | FSM controller |
| W1.5.3 | `BasePlayerState.cs` | Abstract state class |
| W1.5.4 | State transition logic | ChangeState() method |

**Acceptance Criteria**:
- [ ] State enum visibile in Inspector
- [ ] ChangeState() logs transitions
- [ ] Enter/Execute/Exit chiamati correttamente
- [ ] No null reference exceptions

**File Path**: `Assets/_Scripts/Player/`

---

#### W1.6 - Locomotion States (Day 4-5)
**Effort**: 10h | **Priority**: HIGH | **Dependencies**: W1.4, W1.5

| Task | File | Description |
|------|------|-------------|
| W1.6.1 | `PlayerIdleState.cs` | Standing still state |
| W1.6.2 | `PlayerWalkState.cs` | Walking movement |
| W1.6.3 | `PlayerSprintState.cs` | Sprint with stamina |
| W1.6.4 | Movement physics | CharacterController movement |

**Acceptance Criteria**:
- [ ] Player si muove con WASD
- [ ] Transizione Idle ↔ Walk funziona
- [ ] Sprint aumenta velocità
- [ ] Rotation smooth verso direzione movimento

**Validation**: Video recording di movement test

---

### Week 1 Checkpoint
**Data**: End of Day 5
**Deliverable**: Player capsule che si muove in scena test
**Test**:
1. WASD movement fluido
2. Sprint con Shift
3. State transitions nel Inspector
4. No errori in console

---

## Week 2: Combat Core

### Milestone: "Player può attaccare e fare danno"

#### W2.1 - Stamina System (Day 6)
**Effort**: 6h | **Priority**: HIGH | **Dependencies**: W1.2

| Task | File | Description |
|------|------|-------------|
| W2.1.1 | `StaminaSystem.cs` | Singleton stamina manager |
| W2.1.2 | Consumption logic | TryConsume() method |
| W2.1.3 | Regeneration | Delayed regen with rate |
| W2.1.4 | Events | OnStaminaChanged, OnDepleted |

**Acceptance Criteria**:
- [ ] Sprint consuma stamina
- [ ] Regen inizia dopo delay
- [ ] Stamina non va sotto 0
- [ ] Events fired correttamente

**File Path**: `Assets/_Scripts/Combat/Systems/StaminaSystem.cs`

---

#### W2.2 - ScriptableObjects Config (Day 6-7)
**Effort**: 6h | **Priority**: HIGH | **Dependencies**: W1.2

| Task | File | Description |
|------|------|-------------|
| W2.2.1 | `CombatConfigSO.cs` | Global combat settings |
| W2.2.2 | `WeaponDataSO.cs` | Weapon configuration |
| W2.2.3 | `PlayerConfigSO.cs` | Player configuration |
| W2.2.4 | Default assets | Create default SO instances |

**Acceptance Criteria**:
- [ ] SO creabili da menu Assets
- [ ] Valori default ragionevoli
- [ ] Inspector user-friendly con ranges
- [ ] Tooltips per ogni campo

**File Path**: `Assets/_Scripts/Scriptables/`

---

#### W2.3 - Attack States (Day 7-8)
**Effort**: 10h | **Priority**: HIGH | **Dependencies**: W1.5, W2.1

| Task | File | Description |
|------|------|-------------|
| W2.3.1 | `PlayerLightAttackState.cs` | Light attack execution |
| W2.3.2 | `PlayerHeavyAttackState.cs` | Heavy attack execution |
| W2.3.3 | Attack timing | Entry → Active → Recovery |
| W2.3.4 | Stamina validation | Check before attack |

**Acceptance Criteria**:
- [ ] R1/LMB triggers light attack
- [ ] R2/RMB triggers heavy attack
- [ ] Attack ha startup, active, recovery
- [ ] No attack se stamina insufficiente

**File Path**: `Assets/_Scripts/Player/States/`

---

#### W2.4 - Hitbox System (Day 8-9)
**Effort**: 8h | **Priority**: HIGH | **Dependencies**: W1.1

| Task | File | Description |
|------|------|-------------|
| W2.4.1 | `Hitbox.cs` | Attack hitbox component |
| W2.4.2 | `Hurtbox.cs` | Damage receiver component |
| W2.4.3 | Collision detection | OnTriggerEnter logic |
| W2.4.4 | Hit registration | Prevent multi-hit same attack |

**Acceptance Criteria**:
- [ ] Hitbox attiva solo durante attack active frames
- [ ] Hurtbox registra hit una volta per attack
- [ ] Debug gizmos mostrano hitbox
- [ ] Layer matrix funziona correttamente

**File Path**: `Assets/_Scripts/Combat/Hitbox/`

---

#### W2.5 - Combat System (Day 9-10)
**Effort**: 10h | **Priority**: HIGH | **Dependencies**: W2.4, W1.3

| Task | File | Description |
|------|------|-------------|
| W2.5.1 | `CombatSystem.cs` | Singleton combat manager |
| W2.5.2 | Damage calculation | Base * multipliers - defense |
| W2.5.3 | Hit processing | ProcessHit() method |
| W2.5.4 | Combat events | OnDamageDealt, OnHitLanded |

**Acceptance Criteria**:
- [ ] Damage calcolato correttamente
- [ ] Defense riduce danno
- [ ] Events propagati
- [ ] Debug log di ogni hit

**File Path**: `Assets/_Scripts/Combat/Systems/CombatSystem.cs`

---

#### W2.6 - Player Controller Integration (Day 10)
**Effort**: 6h | **Priority**: HIGH | **Dependencies**: All W2.*

| Task | File | Description |
|------|------|-------------|
| W2.6.1 | `PlayerController.cs` | Main player component |
| W2.6.2 | ICombatant impl | Implement interface |
| W2.6.3 | IDamageable impl | Implement interface |
| W2.6.4 | Component wiring | Connect all systems |

**Acceptance Criteria**:
- [ ] PlayerController compila senza errori
- [ ] Implements ICombatant, IDamageable
- [ ] Health visibile in Inspector
- [ ] TakeDamage() riduce health

**File Path**: `Assets/_Scripts/Player/PlayerController.cs`

---

### Week 2 Checkpoint
**Data**: End of Day 10
**Deliverable**: Player può attaccare dummy target
**Test**:
1. Light attack con R1
2. Heavy attack con R2
3. Stamina consumption visibile
4. Hitbox debug gizmos
5. Damage numbers in console

---

## Week 3: Defensive Mechanics

### Milestone: "Player può parry e dodge"

#### W3.1 - Parry State (Day 11-12)
**Effort**: 10h | **Priority**: CRITICAL | **Dependencies**: W2.5

| Task | File | Description |
|------|------|-------------|
| W3.1.1 | `PlayerParryState.cs` | Parry execution |
| W3.1.2 | Parry window | 200ms timing window |
| W3.1.3 | Perfect parry | First 100ms detection |
| W3.1.4 | Parry feedback | Visual/audio feedback |

**Acceptance Criteria**:
- [ ] L1 triggers parry stance
- [ ] Window di 200ms funziona
- [ ] Perfect vs Partial distinguibile
- [ ] State transition a idle dopo window

**File Path**: `Assets/_Scripts/Player/States/PlayerParryState.cs`

---

#### W3.2 - Parry Resolution (Day 12-13)
**Effort**: 8h | **Priority**: CRITICAL | **Dependencies**: W3.1

| Task | File | Description |
|------|------|-------------|
| W3.2.1 | Parry check | CombatSystem.TryParry() |
| W3.2.2 | Perfect parry | Full deflect + stagger enemy |
| W3.2.3 | Partial parry | Reduced damage |
| W3.2.4 | Miss | Full damage received |

**Acceptance Criteria**:
- [ ] Perfect parry: 0 damage, enemy stagger
- [ ] Partial parry: 50% damage
- [ ] Miss: 100% damage
- [ ] Timing feels responsive

**Validation**: Frame-by-frame video analysis parry timing

---

#### W3.3 - Dodge State (Day 13-14)
**Effort**: 8h | **Priority**: HIGH | **Dependencies**: W2.1

| Task | File | Description |
|------|------|-------------|
| W3.3.1 | `PlayerDodgeState.cs` | Dodge roll execution |
| W3.3.2 | i-frames | 300ms invulnerability |
| W3.3.3 | Dodge movement | Directional movement |
| W3.3.4 | Stamina cost | Consume on dodge |

**Acceptance Criteria**:
- [ ] Circle/Space triggers dodge
- [ ] i-frames prevengono danno
- [ ] Dodge nella direzione input
- [ ] Stamina consumata

**File Path**: `Assets/_Scripts/Player/States/PlayerDodgeState.cs`

---

#### W3.4 - Stagger State (Day 14)
**Effort**: 4h | **Priority**: HIGH | **Dependencies**: W2.5

| Task | File | Description |
|------|------|-------------|
| W3.4.1 | `PlayerStaggerState.cs` | Stagger reaction |
| W3.4.2 | Stagger duration | Configurable recovery |
| W3.4.3 | Input lockout | No input during stagger |
| W3.4.4 | Transition back | To idle after recovery |

**Acceptance Criteria**:
- [ ] Poise break triggers stagger
- [ ] No input accepted during stagger
- [ ] Recovery time configurabile
- [ ] Smooth transition back to idle

---

#### W3.5 - Poise System (Day 14-15)
**Effort**: 6h | **Priority**: HIGH | **Dependencies**: W3.4

| Task | File | Description |
|------|------|-------------|
| W3.5.1 | Poise tracking | CurrentPoise accumulation |
| W3.5.2 | Poise damage | Per-attack poise damage |
| W3.5.3 | Poise break | Trigger stagger at max |
| W3.5.4 | Poise recovery | Delayed regeneration |

**Acceptance Criteria**:
- [ ] Hits accumulano poise damage
- [ ] Stagger a MaxPoise raggiunto
- [ ] Poise recovery after delay
- [ ] Debug UI mostra poise

---

### Week 3 Checkpoint
**Data**: End of Day 15
**Deliverable**: Combat loop completo player-side
**Test**:
1. Parry timing test (200ms window)
2. Perfect vs Partial parry distinction
3. Dodge i-frames test
4. Poise break → stagger flow
5. Full combat loop: attack, parry, dodge, stagger, recovery

---

## Week 4: Enemy & Integration

### Milestone: "Player vs Enemy combat funzionante"

#### W4.1 - Enemy Controller Base (Day 16-17)
**Effort**: 8h | **Priority**: HIGH | **Dependencies**: W2.6

| Task | File | Description |
|------|------|-------------|
| W4.1.1 | `EnemyController.cs` | Main enemy component |
| W4.1.2 | `EnemyStateMachine.cs` | Enemy FSM |
| W4.1.3 | `EnemyState.cs` | Enum definition |
| W4.1.4 | Interface impl | ICombatant, IDamageable, ILockOnTarget |

**Acceptance Criteria**:
- [ ] Enemy prefab spawna in scena
- [ ] State machine funziona
- [ ] Implements tutte le interface
- [ ] Health visibile

**File Path**: `Assets/_Scripts/Enemies/`

---

#### W4.2 - Enemy AI States (Day 17-18)
**Effort**: 10h | **Priority**: HIGH | **Dependencies**: W4.1

| Task | File | Description |
|------|------|-------------|
| W4.2.1 | `EnemyIdleState.cs` | Idle with detection |
| W4.2.2 | `EnemyChaseState.cs` | Move towards player |
| W4.2.3 | `EnemyAttackState.cs` | Execute attack |
| W4.2.4 | `EnemyStaggeredState.cs` | Stagger reaction |

**Acceptance Criteria**:
- [ ] Enemy detecta player in range
- [ ] Chase fino a attack range
- [ ] Attack con telegraph
- [ ] Stagger on poise break

---

#### W4.3 - Enemy ScriptableObject (Day 18)
**Effort**: 4h | **Priority**: MEDIUM | **Dependencies**: W4.1

| Task | File | Description |
|------|------|-------------|
| W4.3.1 | `EnemyDataSO.cs` | Enemy configuration |
| W4.3.2 | Default enemy | Basic enemy SO |
| W4.3.3 | Attack patterns | Array of AttackData |
| W4.3.4 | AI parameters | Detection, chase, cooldowns |

**Acceptance Criteria**:
- [ ] SO creabile da menu
- [ ] Valori configurabili
- [ ] Attack patterns definiti
- [ ] AI tuning facile

---

#### W4.4 - Lock-On System (Day 19)
**Effort**: 6h | **Priority**: HIGH | **Dependencies**: W4.1

| Task | File | Description |
|------|------|-------------|
| W4.4.1 | `LockOnSystem.cs` | Singleton lock-on manager |
| W4.4.2 | Target detection | Find targets in range |
| W4.4.3 | Target switching | R3/Tab to switch |
| W4.4.4 | Camera influence | Face target when locked |

**Acceptance Criteria**:
- [ ] R3/Tab toggles lock-on
- [ ] Target indicator visibile
- [ ] Switch target funziona
- [ ] Movement diventa strafe

**File Path**: `Assets/_Scripts/Combat/Systems/LockOnSystem.cs`

---

#### W4.5 - Death State (Day 19-20)
**Effort**: 4h | **Priority**: MEDIUM | **Dependencies**: W4.2

| Task | File | Description |
|------|------|-------------|
| W4.5.1 | `PlayerDeathState.cs` | Player death |
| W4.5.2 | `EnemyDeathState.cs` | Enemy death |
| W4.5.3 | Death trigger | Health <= 0 |
| W4.5.4 | Game over flow | Basic restart option |

**Acceptance Criteria**:
- [ ] Death state triggered at 0 HP
- [ ] Input disabled after death
- [ ] Enemy disappears/ragdolls
- [ ] Restart option disponibile

---

#### W4.6 - Integration & Polish (Day 20)
**Effort**: 6h | **Priority**: HIGH | **Dependencies**: All W4.*

| Task | File | Description |
|------|------|-------------|
| W4.6.1 | Combat flow test | Full player vs enemy |
| W4.6.2 | Bug fixing | Address found issues |
| W4.6.3 | Timing tuning | Adjust via ScriptableObjects |
| W4.6.4 | Debug tools | Combat debug overlay |

**Acceptance Criteria**:
- [ ] Player può uccidere enemy
- [ ] Enemy può uccidere player
- [ ] Parry funziona su enemy attacks
- [ ] Combat feels responsive

---

### Week 4 Checkpoint (MVP COMPLETE)
**Data**: End of Day 20
**Deliverable**: MVP Combat Loop Completo
**Test**:
1. Player vs Enemy combat
2. Both can attack, damage, die
3. Parry su enemy attacks
4. Dodge evade enemy attacks
5. Lock-on targeting
6. Stamina management
7. Poise/stagger system
8. Video recording full combat

---

## Week 5-6: Buffer & Validation

### Milestone: "MVP Polished & Validated"

#### W5.1 - Combat Tuning (Day 21-23)
| Task | Description |
|------|-------------|
| Parry window tuning | Adjust 200ms if needed |
| Damage balance | Player/enemy health ratios |
| Stamina economy | Costs vs regen rates |
| Poise values | Break thresholds |

#### W5.2 - Bug Fixing (Day 23-25)
| Task | Description |
|------|-------------|
| Edge case fixes | State machine bugs |
| Collision issues | Hitbox refinement |
| Input bugs | Buffer edge cases |
| Performance | Profiling and optimization |

#### W5.3 - Validation (Day 25-28)
| Task | Description |
|------|-------------|
| Acceptance test | All requirements met |
| Playtest | Feel and responsiveness |
| Documentation | Code comments, README |
| Handoff prep | Clean repo state |

---

## Dependency Graph

```
W1.1 Infrastructure ──┬──▶ W1.4 Input Handler
                      │
W1.2 Data Structures ─┼──▶ W1.3 Interfaces ──▶ W1.5 State Machine ──▶ W1.6 Locomotion
                      │                              │
                      │                              ▼
                      └──▶ W2.1 Stamina ──────▶ W2.3 Attack States
                                                     │
W2.2 ScriptableObjects ──────────────────────────────┤
                                                     │
                      ┌──────────────────────────────┘
                      │
                      ▼
W2.4 Hitbox ──▶ W2.5 Combat System ──▶ W2.6 Player Controller
                      │
                      ├──▶ W3.1 Parry State ──▶ W3.2 Parry Resolution
                      │
                      ├──▶ W3.3 Dodge State
                      │
                      └──▶ W3.4 Stagger ──▶ W3.5 Poise System
                                │
                                ▼
                      W4.1 Enemy Controller ──▶ W4.2 Enemy AI
                                │
                                ├──▶ W4.3 Enemy SO
                                │
                                └──▶ W4.4 Lock-On ──▶ W4.5 Death ──▶ W4.6 Integration
```

---

## Risk Checkpoints

| Checkpoint | Day | Risk Check | Mitigation |
|------------|-----|------------|------------|
| Movement | 5 | Input responsiveness | Adjust buffer window |
| Combat | 10 | Hitbox reliability | Increase solver iterations |
| Parry | 13 | Timing feel | Expose all values in SO |
| Enemy | 18 | AI behavior | Simplify patterns if needed |
| MVP | 20 | Overall feel | Extra tuning week buffer |

---

## Context Loading

Per riprendere questo workflow:
```
1. Leggi: workflow.md (questo file)
2. Identifica settimana corrente
3. Leggi task specifici della settimana
4. Reference: ../03-design/architecture.md per implementazione
```
