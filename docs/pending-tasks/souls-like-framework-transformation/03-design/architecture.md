# Architecture Design: Souls-Like Framework

**Data**: 2025-12-15
**Depth**: `--ultrathink`
**Fase**: 03-Design

---

## Executive Summary

Questo documento presenta l'architettura completa per trasformare Project Keira da framework turn-based a souls-like action RPG. Vengono presentate 3 alternative architetturali con analisi comparativa, raccomandazione finale, e design dettagliato di tutti i componenti core.

**Raccomandazione**: Opzione C (Hierarchical State Machine + Systems) - massima separazione concerns, scalabilita eccellente, supporta pattern esistenti.

---

## 1. Alternative Architetturali

### 1.1 Opzione A: Monolithic Controller Pattern

**Descrizione**: Un unico `PlayerController` che gestisce tutti gli aspetti del player (movement, combat, stamina, states) con metodi separati per ciascuna responsabilita.

```
PlayerController
+-- Movement()
+-- Combat()
+-- Stamina()
+-- HandleStates()
+-- All logic in one class
```

**Pro**:
- Semplicita iniziale, veloce da implementare
- Facile debug (tutto in un posto)
- Minima coordinazione tra componenti
- Basso overhead di comunicazione

**Contro**:
- Violazione Single Responsibility (SOLID)
- Classe monolitica difficile da mantenere (>1000 LOC previste)
- Testing difficile (tutto accoppiato)
- Scarsa estensibilita per future features
- Difficile parallelizzare lo sviluppo

**Rischi**:
- **Alto**: Spaghetti code entro 2 mesi
- **Alto**: Refactoring obbligatorio per aggiungere nuove armi/nemici
- **Medio**: Bug difficili da isolare

**Effort Stimato**: 3 settimane MVP, +4 settimane refactoring futuro

---

### 1.2 Opzione B: Component-Based Architecture (ECS-lite)

**Descrizione**: Separazione in componenti Unity indipendenti, comunicazione via eventi, ogni componente una responsabilita.

```
Player (GameObject)
+-- PlayerMovement (Component)
+-- PlayerCombat (Component)
+-- StaminaController (Component)
+-- PlayerStateMachine (Component)
+-- HealthController (Component)
+-- Communication via Events/UnityEvents
```

**Pro**:
- Rispetta Single Responsibility
- Componenti testabili isolatamente
- Riutilizzabilita (HealthController per enemy)
- Parallelizzazione sviluppo possibile
- Pattern familiare Unity

**Contro**:
- Coordinazione eventi puo diventare complessa
- Event-hell se non gestito bene
- Overhead performance minimo per eventi
- Ordine di esecuzione da gestire

**Rischi**:
- **Medio**: Event spaghetti senza disciplina
- **Basso**: Performance (mitigabile con pooling eventi)
- **Basso**: Difficolta iniziale maggiore

**Effort Stimato**: 4 settimane MVP, +1 settimana setup iniziale

---

### 1.3 Opzione C: Hierarchical State Machine + Systems

**Descrizione**: Architettura a due livelli: Systems singleton per logica globale, HSM per comportamento entita. Massima separazione concerns.

```
Systems (Global Singletons)
+-- CombatSystem (damage calc, hit detection)
+-- StaminaSystem (regen logic, validation)
+-- LockOnSystem (target management)
+-- InputSystem (input buffering, processing)

Entities (Per-Instance)
+-- PlayerController
|   +-- PlayerStateMachine (HSM)
|       +-- LocomotionState (parent)
|       |   +-- IdleState
|       |   +-- WalkState
|       |   +-- SprintState
|       +-- CombatState (parent)
|       |   +-- LightAttackState
|       |   +-- HeavyAttackState
|       |   +-- ParryState
|       |   +-- DodgeState
|       +-- StaggerState
+-- EnemyController
    +-- EnemyStateMachine (HSM)
```

**Pro**:
- Massima separazione concerns
- Systems riutilizzabili tra entita
- HSM previene stati invalidi
- Scalabilita eccellente
- Testabilita ottima
- Supporta pattern esistenti (StaticInstance)

**Contro**:
- Complessita iniziale maggiore
- Learning curve per HSM
- Piu boilerplate iniziale
- Richiede design upfront accurato

**Rischi**:
- **Basso**: Over-engineering (mitigato da MVP scope)
- **Basso**: Complessita (HSM e pattern standard)
- **Medio**: Tempo setup iniziale

**Effort Stimato**: 5 settimane MVP, setup robusto per espansioni

---

## 2. Raccomandazione: Opzione C (HSM + Systems)

### 2.1 Rationale

| Criterio | Peso | A | B | C |
|----------|------|---|---|---|
| **Maintainability** | 25% | 2 | 4 | 5 |
| **Extensibility** | 25% | 1 | 3 | 5 |
| **MVP Speed** | 20% | 5 | 3 | 3 |
| **Code Quality** | 15% | 2 | 4 | 5 |
| **Testing** | 15% | 1 | 4 | 5 |
| **TOTALE** | 100% | 2.15 | 3.6 | **4.6** |

### 2.2 Motivazioni Chiave

1. **Compatibilita con Codebase Esistente**
   - `StaticInstance<T>` pattern gia presente e production-ready
   - `Systems` parent singleton pronto per estensione
   - ScriptableObject pattern riutilizzabile

2. **Requisiti Souls-Like**
   - HSM naturale per combat states (attack, parry, dodge, stagger)
   - Systems centralizzati per timing critico (parry window 200ms)
   - Separazione permette tuning indipendente

3. **Future-Proofing**
   - Weapon system: nuovo stato, stessa macchina
   - Enemy types: ereditano da EnemyStateMachine base
   - Boss fights: HSM supporta substati complessi

4. **Risk Mitigation**
   - Over-engineering mitigato da scope MVP
   - Complessita HSM mitigata da pattern standard
   - Setup time compensato da zero refactoring futuro

---

## 3. Component Diagram

```
+------------------------------------------------------------------+
|                         SYSTEMS (Persistent)                      |
+------------------------------------------------------------------+
|                                                                   |
|  +-----------------+  +-----------------+  +-----------------+    |
|  |   InputSystem   |  |  CombatSystem   |  | StaminaSystem   |    |
|  |-----------------|  |-----------------|  |-----------------|    |
|  | - inputBuffer   |  | - hitDetection  |  | - regenRate     |    |
|  | - ProcessInput()|  | - damageCalc    |  | - ValidateCost()|    |
|  | - BufferAction()|  | - ApplyDamage() |  | - Consume()     |    |
|  +-----------------+  +-----------------+  +-----------------+    |
|           |                   |                    |              |
|           v                   v                    v              |
|  +-----------------+  +-----------------+  +-----------------+    |
|  |  LockOnSystem   |  |   AudioSystem   |  | ResourceSystem  |    |
|  |-----------------|  |-----------------|  |-----------------|    |
|  | - currentTarget |  | (existing)      |  | (existing)      |    |
|  | - ToggleLock()  |  | - PlaySFX()     |  | - GetAsset()    |    |
|  | - SwitchTarget()|  +-----------------+  +-----------------+    |
|  +-----------------+                                              |
|                                                                   |
+------------------------------------------------------------------+
                              |
                              | References
                              v
+------------------------------------------------------------------+
|                      ENTITIES (Per-Instance)                      |
+------------------------------------------------------------------+
|                                                                   |
|  +---------------------------+   +---------------------------+    |
|  |     PlayerController      |   |     EnemyController       |    |
|  |---------------------------|   |---------------------------|    |
|  | - StateMachine            |   | - StateMachine            |    |
|  | - CombatStats             |   | - CombatStats             |    |
|  | - Animator                |   | - Animator                |    |
|  | - CharacterController     |   | - NavMeshAgent (opt)      |    |
|  +---------------------------+   +---------------------------+    |
|               |                              |                    |
|               v                              v                    |
|  +---------------------------+   +---------------------------+    |
|  |   PlayerStateMachine      |   |   EnemyStateMachine       |    |
|  |---------------------------|   |---------------------------|    |
|  | IState currentState       |   | IState currentState       |    |
|  | ChangeState()             |   | ChangeState()             |    |
|  | Update()                  |   | Update()                  |    |
|  +---------------------------+   +---------------------------+    |
|               |                              |                    |
|       +-------+-------+              +-------+-------+            |
|       |       |       |              |       |       |            |
|       v       v       v              v       v       v            |
|   +------+ +------+ +------+    +------+ +------+ +------+        |
|   | Idle | |Attack| |Dodge |    | Idle | |Attack| |Chase |        |
|   +------+ +------+ +------+    +------+ +------+ +------+        |
|                                                                   |
+------------------------------------------------------------------+
                              |
                              | Implements
                              v
+------------------------------------------------------------------+
|                        INTERFACES                                 |
+------------------------------------------------------------------+
|                                                                   |
|  +------------------+  +------------------+  +------------------+  |
|  |    IState        |  |   ICombatant     |  |   IDamageable    |  |
|  |------------------|  |------------------|  |------------------|  |
|  | Enter()          |  | Attack()         |  | TakeDamage()     |  |
|  | Execute()        |  | Parry()          |  | Die()            |  |
|  | Exit()           |  | Dodge()          |  | CurrentHealth    |  |
|  | HandleInput()    |  | GetCombatStats() |  | MaxHealth        |  |
|  +------------------+  +------------------+  +------------------+  |
|                                                                   |
+------------------------------------------------------------------+
                              |
                              | Uses
                              v
+------------------------------------------------------------------+
|                   DATA (ScriptableObjects)                        |
+------------------------------------------------------------------+
|                                                                   |
|  +-------------------+  +-------------------+  +-----------------+ |
|  | CombatConfig SO   |  | WeaponData SO     |  | EnemyData SO    | |
|  |-------------------|  |-------------------|  |-----------------| |
|  | parryWindow       |  | baseDamage        |  | health          | |
|  | iFrameDuration    |  | staminaCost       |  | attackPatterns  | |
|  | staminaRegenDelay |  | attackSpeed       |  | detectionRange  | |
|  +-------------------+  +-------------------+  +-----------------+ |
|                                                                   |
+------------------------------------------------------------------+
```

---

## 4. Data Flow Diagram

### 4.1 Input Flow

```
+----------+     +-------------+     +------------------+     +-------------+
|  Input   | --> | InputSystem | --> | PlayerController | --> | StateMachine|
| (Device) |     | (Singleton) |     |   (Instance)     |     |  (States)   |
+----------+     +-------------+     +------------------+     +-------------+
     |                  |                     |                      |
     |                  |                     |                      |
     v                  v                     v                      v
 Raw Input      Input Buffering        Input Validation       State Transition
 (R1 press)     (queue actions)        (can attack?)          (Idle -> Attack)
```

**Dettaglio Input Buffer**:
```
Frame N:   [Attack pressed] --> Buffer.Enqueue(Attack, timestamp)
Frame N+1: [In recovery]    --> Buffer held, not consumed
Frame N+5: [Recovery ends]  --> Buffer.Dequeue() --> Execute Attack
Frame N+15: [Buffer timeout] --> Buffer.Clear(stale actions)
```

### 4.2 Combat Flow

```
+--------+     +--------+     +--------------+     +--------+
| Attack | --> | Hitbox | --> | CombatSystem | --> | Damage |
| State  |     | Active |     | .ProcessHit()|     | Apply  |
+--------+     +--------+     +--------------+     +--------+
                    |                |                  |
                    v                v                  v
              +-----------+    +-----------+    +-----------+
              | Collision |    | Calculate |    | IDamageable|
              | Detection |    | Damage    |    | .TakeDamage|
              +-----------+    +-----------+    +-----------+
```

**Hit Detection Sequence**:
```
1. AttackState.Enter()
   +-- Enable hitbox collider (trigger)

2. Hitbox.OnTriggerEnter(Collider other)
   +-- if (other.TryGetComponent<IDamageable>(out var target))
       +-- CombatSystem.Instance.ProcessHit(attacker, target, hitData)

3. CombatSystem.ProcessHit()
   +-- Calculate damage (base * multipliers)
   +-- Check parry state of target
   |   +-- if (target.IsParrying && timing.IsPerfect) --> ParrySuccess
   |   +-- else --> ApplyDamage
   +-- Emit events (OnHit, OnDamage, OnParry)

4. IDamageable.TakeDamage(damage)
   +-- health -= damage
   +-- if (health <= 0) --> Die()
```

### 4.3 Parry Flow

```
+---------+     +------------+     +--------------+     +----------+
| Parry   | --> | Parry      | --> | Enemy Attack | --> | Parry    |
| Input   |     | Window     |     | Collision    |     | Result   |
+---------+     +------------+     +--------------+     +----------+
     |               |                    |                  |
     v               v                    v                  v
 L1 Press      200ms Active         Hit Detection       Perfect/Partial
               (configurable)       during window       /Miss
```

**Parry Timing Detail**:
```
Timeline (ms):
0ms     50ms    100ms   150ms   200ms   250ms
|-------|-------|-------|-------|-------|
[PERFECT WINDOW ]
        [    PARTIAL WINDOW     ]
                                [RECOVERY]

Perfect: 0-100ms   --> Stagger enemy, 0 damage, bonus window
Partial: 50-200ms  --> Reduced damage (50%), no stagger
Miss:    >200ms    --> Full damage received
```

### 4.4 Stamina Flow

```
+--------+     +---------------+     +--------+     +--------+
| Action | --> | StaminaSystem | --> | Validate| --> | Execute|
| Request|     | .TryConsume() |     | Cost   |     | or Deny|
+--------+     +---------------+     +--------+     +--------+
                      |
                      v
               +-------------+
               | Regeneration|
               | (delayed)   |
               +-------------+
```

**Stamina Regeneration Logic**:
```
State: Regenerating
+-- if (timeSinceLastAction > regenDelay)
|   +-- currentStamina += regenRate * deltaTime
+-- Clamp(0, maxStamina)
+-- OnStaminaChanged?.Invoke(currentStamina)

State: Depleted
+-- currentStamina = 0
+-- canSprint = false
+-- canDodge = false
+-- Wait for partial regen before actions
```

---

## 5. State Machine Design

### 5.1 Player State Machine (HSM)

```
PlayerStateMachine
|
+-- GroundedState (Parent)
|   +-- IdleState
|   |   +-- Entry: PlayAnimation("Idle"), EnableMovement()
|   |   +-- Execute: CheckMovementInput(), CheckCombatInput()
|   |   +-- Transitions:
|   |       +-- Move input --> WalkState
|   |       +-- Sprint input --> SprintState
|   |       +-- Attack input --> LightAttackState
|   |       +-- Parry input --> ParryState
|   |       +-- Dodge input --> DodgeState
|   |
|   +-- WalkState
|   |   +-- Entry: PlayAnimation("Walk")
|   |   +-- Execute: ApplyMovement(walkSpeed), RotateToMovement()
|   |   +-- Transitions:
|   |       +-- No input --> IdleState
|   |       +-- Sprint --> SprintState
|   |       +-- Combat inputs --> Combat states
|   |
|   +-- SprintState
|       +-- Entry: PlayAnimation("Sprint")
|       +-- Execute: ApplyMovement(sprintSpeed), ConsumeStamina()
|       +-- Transitions:
|           +-- Stamina depleted --> WalkState
|           +-- Sprint released --> WalkState
|           +-- Combat inputs --> Combat states
|
+-- CombatState (Parent)
|   +-- LightAttackState
|   |   +-- Entry: PlayAnimation("LightAttack"), EnableHitbox()
|   |   +-- Execute: AnimationDrivenTiming()
|   |   +-- Exit: DisableHitbox()
|   |   +-- Transitions:
|   |       +-- Animation complete --> IdleState
|   |       +-- Combo window + input --> LightAttack2State
|   |       +-- Hit received --> StaggerState
|   |
|   +-- HeavyAttackState
|   |   +-- Entry: PlayAnimation("HeavyAttack"), EnableHitbox()
|   |   +-- Execute: AnimationDrivenTiming()
|   |   +-- Exit: DisableHitbox()
|   |   +-- Transitions:
|   |       +-- Animation complete --> IdleState
|   |       +-- Hit received --> StaggerState
|   |
|   +-- ParryState
|   |   +-- Entry: StartParryWindow(), PlayAnimation("Parry")
|   |   +-- Execute: CheckParryTiming()
|   |   +-- Exit: EndParryWindow()
|   |   +-- Transitions:
|   |       +-- Parry success --> ParryRecoveryState
|   |       +-- Parry miss --> IdleState
|   |       +-- Window expires --> IdleState
|   |
|   +-- DodgeState
|       +-- Entry: EnableIFrames(), PlayAnimation("Dodge")
|       +-- Execute: ApplyDodgeMovement()
|       +-- Exit: DisableIFrames()
|       +-- Transitions:
|           +-- Animation complete --> IdleState
|
+-- HitReactionState (Parent)
|   +-- StaggerState
|   |   +-- Entry: ApplyKnockback(), PlayAnimation("Stagger")
|   |   +-- Transitions:
|   |       +-- Recovery complete --> IdleState
|   |
|   +-- DeathState
|       +-- Entry: DisableControl(), PlayAnimation("Death")
|       +-- Transitions: None (terminal state)
|
+-- LockedOnState (Modifier)
    +-- Modifies: Movement becomes strafe
    +-- Modifies: Camera focuses on target
    +-- Active: During any non-death state when locked
```

### 5.2 Enemy State Machine

```
EnemyStateMachine
|
+-- IdleState
|   +-- Entry: PlayAnimation("Idle")
|   +-- Execute: DetectPlayer()
|   +-- Transitions:
|       +-- Player detected --> ChaseState
|
+-- ChaseState
|   +-- Entry: PlayAnimation("Walk")
|   +-- Execute: MoveTowardsPlayer()
|   +-- Transitions:
|       +-- In attack range --> AttackDecisionState
|       +-- Player lost --> IdleState
|
+-- AttackDecisionState
|   +-- Execute: SelectAttackPattern()
|   +-- Transitions:
|       +-- Pattern selected --> AttackState
|
+-- AttackState
|   +-- Entry: PlayAnimation(selectedAttack), EnableHitbox()
|   +-- Execute: ExecuteAttackPattern()
|   +-- Exit: DisableHitbox()
|   +-- Transitions:
|       +-- Attack complete --> CooldownState
|       +-- Parried --> StaggeredState
|
+-- StaggeredState
|   +-- Entry: PlayAnimation("Staggered"), OpenToRiposte()
|   +-- Transitions:
|       +-- Stagger duration ends --> RecoveryState
|
+-- RecoveryState
|   +-- Execute: Wait(recoveryTime)
|   +-- Transitions:
|       +-- Recovery complete --> ChaseState
|
+-- DeathState
    +-- Entry: PlayAnimation("Death"), DisableAI()
    +-- Transitions: None (terminal)
```

### 5.3 State Transition Matrix

**Player States**:

| From / To | Idle | Walk | Sprint | LightAtk | HeavyAtk | Parry | Dodge | Stagger | Death |
|-----------|------|------|--------|----------|----------|-------|-------|---------|-------|
| Idle | - | move | sprint | R1 | R2 | L1 | Circle | hit | hp=0 |
| Walk | !move | - | sprint | R1 | R2 | L1 | Circle | hit | hp=0 |
| Sprint | !sprint | !sprint | - | R1 | R2 | L1 | Circle | hit | hp=0 |
| LightAtk | anim_end | - | - | combo | - | - | - | hit | hp=0 |
| HeavyAtk | anim_end | - | - | - | - | - | - | hit | hp=0 |
| Parry | timeout | - | - | - | - | - | - | miss | hp=0 |
| Dodge | anim_end | - | - | - | - | - | - | - | hp=0 |
| Stagger | recovery | - | - | - | - | - | - | - | hp=0 |
| Death | - | - | - | - | - | - | - | - | - |

**Legenda**: R1=light attack, R2=heavy attack, L1=parry, Circle=dodge

---

## 6. Interface Definitions

### 6.1 IState Interface

```csharp
/// <summary>
/// Base interface for all state machine states.
/// Implements the State pattern for finite state machines.
/// </summary>
public interface IState
{
    /// <summary>
    /// Called once when entering this state.
    /// Initialize state-specific variables, play animations, enable colliders.
    /// </summary>
    void Enter();

    /// <summary>
    /// Called every frame while in this state.
    /// Handle ongoing logic: movement, input checking, timers.
    /// </summary>
    void Execute();

    /// <summary>
    /// Called in FixedUpdate for physics-related operations.
    /// Movement, forces, physics queries.
    /// </summary>
    void PhysicsUpdate();

    /// <summary>
    /// Called once when exiting this state.
    /// Cleanup: disable colliders, reset flags, stop effects.
    /// </summary>
    void Exit();

    /// <summary>
    /// Process input specific to this state.
    /// Returns the next state to transition to, or null to stay.
    /// </summary>
    /// <param name="input">Current input snapshot</param>
    /// <returns>Next state type, or null if no transition</returns>
    IState HandleInput(InputData input);

    /// <summary>
    /// Check if this state can be interrupted by given action.
    /// Used for priority-based state transitions.
    /// </summary>
    /// <param name="action">The interrupting action</param>
    /// <returns>True if interruption is allowed</returns>
    bool CanBeInterruptedBy(CombatAction action);
}
```

### 6.2 ICombatant Interface

```csharp
/// <summary>
/// Interface for any entity that can participate in combat.
/// Implemented by PlayerController and EnemyController.
/// </summary>
public interface ICombatant
{
    /// <summary>
    /// Unique identifier for this combatant.
    /// Used for targeting, event routing, and combat log.
    /// </summary>
    int CombatantId { get; }

    /// <summary>
    /// Current faction affiliation.
    /// Used for friendly fire prevention and AI targeting.
    /// </summary>
    Faction Faction { get; }

    /// <summary>
    /// Current combat statistics.
    /// Returns a readonly view of current stats.
    /// </summary>
    CombatStats GetCombatStats();

    /// <summary>
    /// Current position in world space.
    /// Used for range checks, targeting, AOE calculations.
    /// </summary>
    Vector3 Position { get; }

    /// <summary>
    /// Forward direction of this combatant.
    /// Used for directional attacks and parry angle checks.
    /// </summary>
    Vector3 Forward { get; }

    /// <summary>
    /// Initiate a light attack.
    /// </summary>
    /// <returns>True if attack was initiated</returns>
    bool TryLightAttack();

    /// <summary>
    /// Initiate a heavy attack.
    /// </summary>
    /// <returns>True if attack was initiated</returns>
    bool TryHeavyAttack();

    /// <summary>
    /// Initiate parry stance.
    /// </summary>
    /// <returns>True if parry was initiated</returns>
    bool TryParry();

    /// <summary>
    /// Initiate dodge roll.
    /// </summary>
    /// <param name="direction">Direction to dodge</param>
    /// <returns>True if dodge was initiated</returns>
    bool TryDodge(Vector3 direction);

    /// <summary>
    /// Check if combatant is currently in parry window.
    /// </summary>
    /// <param name="timing">Output timing information</param>
    /// <returns>True if actively parrying</returns>
    bool IsParrying(out ParryTiming timing);

    /// <summary>
    /// Check if combatant is currently invulnerable (i-frames).
    /// </summary>
    bool IsInvulnerable { get; }

    /// <summary>
    /// Apply stagger effect to this combatant.
    /// Called when parried or poise broken.
    /// </summary>
    /// <param name="duration">Stagger duration in seconds</param>
    void ApplyStagger(float duration);

    /// <summary>
    /// Event fired when this combatant attacks.
    /// </summary>
    event Action<AttackData> OnAttack;

    /// <summary>
    /// Event fired when this combatant's attack hits.
    /// </summary>
    event Action<HitData> OnHitLanded;
}
```

### 6.3 IDamageable Interface

```csharp
/// <summary>
/// Interface for any entity that can receive damage.
/// More general than ICombatant - includes destructible objects.
/// </summary>
public interface IDamageable
{
    /// <summary>
    /// Current health value.
    /// </summary>
    float CurrentHealth { get; }

    /// <summary>
    /// Maximum health value.
    /// </summary>
    float MaxHealth { get; }

    /// <summary>
    /// Normalized health (0-1 range).
    /// Useful for UI health bars.
    /// </summary>
    float HealthNormalized => MaxHealth > 0 ? CurrentHealth / MaxHealth : 0f;

    /// <summary>
    /// Whether this entity is currently alive.
    /// </summary>
    bool IsAlive => CurrentHealth > 0;

    /// <summary>
    /// Apply damage to this entity.
    /// </summary>
    /// <param name="damage">Damage info including amount, source, type</param>
    /// <returns>Actual damage dealt after resistances</returns>
    float TakeDamage(DamageInfo damage);

    /// <summary>
    /// Heal this entity.
    /// </summary>
    /// <param name="amount">Amount to heal</param>
    /// <returns>Actual amount healed</returns>
    float Heal(float amount);

    /// <summary>
    /// Kill this entity immediately.
    /// Bypasses damage calculation.
    /// </summary>
    void Die();

    /// <summary>
    /// Event fired when health changes.
    /// Parameters: (currentHealth, maxHealth, delta)
    /// </summary>
    event Action<float, float, float> OnHealthChanged;

    /// <summary>
    /// Event fired when entity dies.
    /// </summary>
    event Action OnDeath;
}
```

### 6.4 ILockOnTarget Interface

```csharp
/// <summary>
/// Interface for entities that can be locked onto.
/// </summary>
public interface ILockOnTarget
{
    /// <summary>
    /// World position of the lock-on point (usually chest height).
    /// </summary>
    Vector3 LockOnPoint { get; }

    /// <summary>
    /// Whether this target can currently be locked onto.
    /// False if dead, invisible, or too far.
    /// </summary>
    bool CanBeLocked { get; }

    /// <summary>
    /// Priority for target selection when multiple targets available.
    /// Higher = more likely to be selected.
    /// </summary>
    int LockOnPriority { get; }

    /// <summary>
    /// Called when this target becomes locked.
    /// </summary>
    void OnLockedOn();

    /// <summary>
    /// Called when lock is released from this target.
    /// </summary>
    void OnLockReleased();
}
```

### 6.5 Supporting Types

```csharp
/// <summary>
/// Snapshot of input state for a single frame.
/// Passed to state machines for decision making.
/// </summary>
public readonly struct InputData
{
    public readonly Vector2 MovementInput;
    public readonly bool AttackPressed;
    public readonly bool HeavyAttackPressed;
    public readonly bool ParryPressed;
    public readonly bool DodgePressed;
    public readonly bool SprintHeld;
    public readonly bool LockOnPressed;
    public readonly float Timestamp;

    public bool HasMovement => MovementInput.sqrMagnitude > 0.01f;
}

/// <summary>
/// Result of a parry timing check.
/// </summary>
public readonly struct ParryTiming
{
    public readonly float WindowStart;
    public readonly float WindowDuration;
    public readonly float CurrentTime;

    public float ElapsedInWindow => CurrentTime - WindowStart;
    public float NormalizedTiming => Mathf.Clamp01(ElapsedInWindow / WindowDuration);
    public bool IsPerfect => NormalizedTiming < 0.5f;
    public bool IsPartial => NormalizedTiming >= 0.5f && NormalizedTiming <= 1f;
}

/// <summary>
/// Information about damage being applied.
/// </summary>
public readonly struct DamageInfo
{
    public readonly float Amount;
    public readonly ICombatant Source;
    public readonly DamageType Type;
    public readonly Vector3 HitPoint;
    public readonly Vector3 HitDirection;
    public readonly bool CanBeParried;
    public readonly float PoiseImpact;
}

/// <summary>
/// Types of damage for resistance calculation.
/// </summary>
public enum DamageType
{
    Physical,
    Fire,
    Electric,
    Toxic
}

/// <summary>
/// Combat actions for state interruption checks.
/// </summary>
public enum CombatAction
{
    Move,
    Attack,
    Parry,
    Dodge,
    Stagger,
    Death
}
```

---

## 7. CombatStats Struct

```csharp
/// <summary>
/// Complete combat statistics for any combatant entity.
/// Designed for souls-like combat with stamina, poise, and parry mechanics.
/// Immutable struct for thread safety and value semantics.
/// </summary>
[Serializable]
public struct CombatStats
{
    //===========================================
    // HEALTH
    //===========================================

    /// <summary>
    /// Maximum health points.
    /// Player: base 100, scales with vigor.
    /// Enemy: defined in EnemyData ScriptableObject.
    /// </summary>
    [Header("Health")]
    [Range(1, 9999)]
    public int MaxHealth;

    /// <summary>
    /// Current health points.
    /// Clamped between 0 and MaxHealth.
    /// </summary>
    [HideInInspector]
    public int CurrentHealth;

    //===========================================
    // STAMINA
    //===========================================

    /// <summary>
    /// Maximum stamina points.
    /// Typical: 100-150 for player.
    /// </summary>
    [Header("Stamina")]
    [Range(1, 500)]
    public float MaxStamina;

    /// <summary>
    /// Current stamina points.
    /// </summary>
    [HideInInspector]
    public float CurrentStamina;

    /// <summary>
    /// Stamina regeneration rate per second.
    /// Typical: 30-50 stamina/sec.
    /// </summary>
    [Range(1f, 100f)]
    public float StaminaRegenRate;

    /// <summary>
    /// Delay before stamina starts regenerating after an action.
    /// Typical: 0.5-1.0 seconds.
    /// </summary>
    [Range(0f, 3f)]
    public float StaminaRegenDelay;

    //===========================================
    // POISE (Stagger Resistance)
    //===========================================

    /// <summary>
    /// Maximum poise before stagger.
    /// Higher = harder to stagger.
    /// Player typical: 50-100.
    /// Boss typical: 200-500.
    /// </summary>
    [Header("Poise")]
    [Range(0, 1000)]
    public int MaxPoise;

    /// <summary>
    /// Current poise value.
    /// Resets after stagger recovery.
    /// </summary>
    [HideInInspector]
    public int CurrentPoise;

    /// <summary>
    /// Poise regeneration rate per second (when not hit recently).
    /// </summary>
    [Range(0f, 100f)]
    public float PoiseRegenRate;

    /// <summary>
    /// Time without hits before poise starts regenerating.
    /// </summary>
    [Range(0f, 10f)]
    public float PoiseRegenDelay;

    //===========================================
    // ATTACK
    //===========================================

    /// <summary>
    /// Base physical attack power.
    /// Final damage = BaseDamage * WeaponMultiplier * ComboMultiplier.
    /// </summary>
    [Header("Attack")]
    [Range(1, 999)]
    public int BaseDamage;

    /// <summary>
    /// Damage multiplier for light attacks.
    /// Typical: 1.0x
    /// </summary>
    [Range(0.1f, 5f)]
    public float LightAttackMultiplier;

    /// <summary>
    /// Damage multiplier for heavy attacks.
    /// Typical: 1.5-2.0x
    /// </summary>
    [Range(0.1f, 5f)]
    public float HeavyAttackMultiplier;

    /// <summary>
    /// Attack speed multiplier.
    /// 1.0 = normal, 1.5 = 50% faster.
    /// </summary>
    [Range(0.5f, 2f)]
    public float AttackSpeed;

    //===========================================
    // DEFENSE
    //===========================================

    /// <summary>
    /// Physical damage reduction percentage.
    /// 0.2 = 20% damage reduction.
    /// </summary>
    [Header("Defense")]
    [Range(0f, 0.9f)]
    public float PhysicalDefense;

    /// <summary>
    /// Damage taken multiplier when partially parrying.
    /// Typical: 0.5 (50% damage on partial parry).
    /// </summary>
    [Range(0f, 1f)]
    public float PartialParryDamageReduction;

    //===========================================
    // STAMINA COSTS
    //===========================================

    /// <summary>
    /// Stamina consumed per second while sprinting.
    /// </summary>
    [Header("Stamina Costs")]
    [Range(1f, 50f)]
    public float SprintStaminaCostPerSecond;

    /// <summary>
    /// Stamina cost for a single dodge roll.
    /// </summary>
    [Range(1f, 50f)]
    public float DodgeStaminaCost;

    /// <summary>
    /// Stamina cost for a light attack.
    /// </summary>
    [Range(1f, 50f)]
    public float LightAttackStaminaCost;

    /// <summary>
    /// Stamina cost for a heavy attack.
    /// </summary>
    [Range(1f, 100f)]
    public float HeavyAttackStaminaCost;

    //===========================================
    // TIMING WINDOWS
    //===========================================

    /// <summary>
    /// Duration of the parry window in seconds.
    /// Typical: 0.2s (200ms) for souls-like.
    /// </summary>
    [Header("Timing")]
    [Range(0.05f, 1f)]
    public float ParryWindowDuration;

    /// <summary>
    /// Duration of perfect parry window (subset of parry window).
    /// Typical: First 100ms of parry window.
    /// </summary>
    [Range(0.01f, 0.5f)]
    public float PerfectParryWindowDuration;

    /// <summary>
    /// Duration of invincibility frames during dodge.
    /// Typical: 0.3s (300ms).
    /// </summary>
    [Range(0.1f, 1f)]
    public float IFrameDuration;

    /// <summary>
    /// Total dodge animation duration.
    /// </summary>
    [Range(0.3f, 2f)]
    public float DodgeDuration;

    /// <summary>
    /// Recovery time after stagger before regaining control.
    /// </summary>
    [Range(0.5f, 5f)]
    public float StaggerRecoveryTime;

    //===========================================
    // MOVEMENT
    //===========================================

    /// <summary>
    /// Base movement speed in units per second.
    /// </summary>
    [Header("Movement")]
    [Range(1f, 20f)]
    public float MoveSpeed;

    /// <summary>
    /// Sprint speed multiplier.
    /// Typical: 1.5-2.0x walk speed.
    /// </summary>
    [Range(1f, 3f)]
    public float SprintMultiplier;

    /// <summary>
    /// Rotation speed in degrees per second.
    /// </summary>
    [Range(90f, 720f)]
    public float RotationSpeed;

    //===========================================
    // FACTORY METHODS
    //===========================================

    /// <summary>
    /// Creates default player stats for MVP.
    /// </summary>
    public static CombatStats CreateDefaultPlayer() => new CombatStats
    {
        // Health
        MaxHealth = 100,
        CurrentHealth = 100,

        // Stamina
        MaxStamina = 100f,
        CurrentStamina = 100f,
        StaminaRegenRate = 30f,
        StaminaRegenDelay = 0.8f,

        // Poise
        MaxPoise = 50,
        CurrentPoise = 50,
        PoiseRegenRate = 20f,
        PoiseRegenDelay = 3f,

        // Attack
        BaseDamage = 20,
        LightAttackMultiplier = 1f,
        HeavyAttackMultiplier = 1.8f,
        AttackSpeed = 1f,

        // Defense
        PhysicalDefense = 0.1f,
        PartialParryDamageReduction = 0.5f,

        // Stamina Costs
        SprintStaminaCostPerSecond = 15f,
        DodgeStaminaCost = 20f,
        LightAttackStaminaCost = 15f,
        HeavyAttackStaminaCost = 30f,

        // Timing
        ParryWindowDuration = 0.2f,
        PerfectParryWindowDuration = 0.1f,
        IFrameDuration = 0.3f,
        DodgeDuration = 0.6f,
        StaggerRecoveryTime = 1.5f,

        // Movement
        MoveSpeed = 5f,
        SprintMultiplier = 1.6f,
        RotationSpeed = 360f
    };

    /// <summary>
    /// Creates default basic enemy stats for MVP.
    /// </summary>
    public static CombatStats CreateDefaultEnemy() => new CombatStats
    {
        // Health
        MaxHealth = 50,
        CurrentHealth = 50,

        // Stamina (enemies typically don't use stamina)
        MaxStamina = 100f,
        CurrentStamina = 100f,
        StaminaRegenRate = 100f,
        StaminaRegenDelay = 0f,

        // Poise (lower than player - easier to stagger)
        MaxPoise = 30,
        CurrentPoise = 30,
        PoiseRegenRate = 10f,
        PoiseRegenDelay = 5f,

        // Attack
        BaseDamage = 15,
        LightAttackMultiplier = 1f,
        HeavyAttackMultiplier = 1.5f,
        AttackSpeed = 0.8f, // Slightly slower, more telegraphed

        // Defense
        PhysicalDefense = 0.05f,
        PartialParryDamageReduction = 0.7f,

        // Stamina Costs (not used by basic AI)
        SprintStaminaCostPerSecond = 0f,
        DodgeStaminaCost = 0f,
        LightAttackStaminaCost = 0f,
        HeavyAttackStaminaCost = 0f,

        // Timing
        ParryWindowDuration = 0f, // Enemies don't parry in MVP
        PerfectParryWindowDuration = 0f,
        IFrameDuration = 0f,
        DodgeDuration = 0f,
        StaggerRecoveryTime = 2f, // Longer stagger for riposte window

        // Movement
        MoveSpeed = 3f,
        SprintMultiplier = 1f,
        RotationSpeed = 180f
    };

    //===========================================
    // UTILITY METHODS
    //===========================================

    public readonly bool HasStaminaFor(float cost) => CurrentStamina >= cost;

    public readonly bool IsAlive => CurrentHealth > 0;

    public readonly float HealthPercent => MaxHealth > 0 ? (float)CurrentHealth / MaxHealth : 0f;

    public readonly float StaminaPercent => MaxStamina > 0 ? CurrentStamina / MaxStamina : 0f;

    public readonly float PoisePercent => MaxPoise > 0 ? (float)CurrentPoise / MaxPoise : 0f;
}
```

---

## 8. File Structure Proposal

```
Assets/_Scripts/
+-- Combat/
|   +-- Systems/
|   |   +-- CombatSystem.cs
|   |   +-- StaminaSystem.cs
|   |   +-- LockOnSystem.cs
|   |   +-- InputHandler.cs
|   |
|   +-- Data/
|   |   +-- CombatStats.cs
|   |   +-- DamageInfo.cs
|   |   +-- AttackData.cs
|   |   +-- ParryTiming.cs
|   |
|   +-- Interfaces/
|   |   +-- IState.cs
|   |   +-- ICombatant.cs
|   |   +-- IDamageable.cs
|   |   +-- ILockOnTarget.cs
|   |
|   +-- Hitbox/
|       +-- Hitbox.cs
|       +-- Hurtbox.cs
|
+-- Player/
|   +-- PlayerController.cs
|   +-- PlayerStateMachine.cs
|   +-- States/
|       +-- PlayerIdleState.cs
|       +-- PlayerWalkState.cs
|       +-- PlayerSprintState.cs
|       +-- PlayerLightAttackState.cs
|       +-- PlayerHeavyAttackState.cs
|       +-- PlayerParryState.cs
|       +-- PlayerDodgeState.cs
|       +-- PlayerStaggerState.cs
|       +-- PlayerDeathState.cs
|
+-- Enemies/
|   +-- EnemyController.cs
|   +-- EnemyStateMachine.cs
|   +-- States/
|       +-- EnemyIdleState.cs
|       +-- EnemyChaseState.cs
|       +-- EnemyAttackState.cs
|       +-- EnemyStaggeredState.cs
|       +-- EnemyDeathState.cs
|
+-- Scriptables/
|   +-- CombatConfigSO.cs
|   +-- WeaponDataSO.cs
|   +-- EnemyDataSO.cs
|
+-- Managers/
|   +-- GameStateManager.cs (replaces ExampleGameManager)
|
+-- Systems/
|   +-- Systems.cs (existing)
|   +-- AudioSystem.cs (existing)
|   +-- ResourceSystem.cs (existing)
|
+-- Utilities/
    +-- StaticInstance.cs (existing)
    +-- Helpers.cs (existing)
```

---

## 9. Implementation Priority

### Week 1: Core Foundation
1. `CombatStats` struct
2. Interfaces (`IState`, `ICombatant`, `IDamageable`)
3. `PlayerStateMachine` base
4. `PlayerIdleState`, `PlayerWalkState`
5. Basic input processing

### Week 2: Combat Core
1. `StaminaSystem`
2. `PlayerSprintState`
3. `PlayerLightAttackState`, `PlayerHeavyAttackState`
4. `Hitbox`/`Hurtbox` system
5. `CombatSystem` base

### Week 3: Defensive Mechanics
1. `PlayerParryState`
2. Parry timing window
3. `PlayerDodgeState`
4. i-frames implementation
5. `PlayerStaggerState`

### Week 4: Enemy and Polish
1. `EnemyController` + `EnemyStateMachine`
2. Basic enemy AI states
3. `LockOnSystem`
4. Integration testing
5. Timing tuning (ScriptableObjects)

---

## 10. Risk Mitigation

| Risk | Probability | Impact | Mitigation |
|------|-------------|--------|------------|
| HSM Complexity | Medium | High | Start with flat FSM, refactor to HSM incrementally |
| Parry Timing | High | High | Early prototyping, expose all values via SO |
| Animation Integration | Medium | Medium | Design for placeholder anims, abstract timing |
| State Spaghetti | Medium | High | Strict interface contracts, no cross-state refs |
| Performance (60fps) | Low | High | Profile early, pool events and objects |

---

## Context Loading

Per riprendere questo design:
```
1. Leggi: architecture.md (questo file)
2. Procedi a: implementation/ (prossima fase)
3. Reference: 01-discovery/requirements.md per acceptance criteria
```
