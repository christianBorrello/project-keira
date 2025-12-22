# Architecture Decision Records: KCC Character System

## ADR-KCC-001: MovementIntent Caching Strategy

### Status
**Accepted**

### Context
KCC usa un modello pull-based con callbacks in FixedUpdate (~50Hz), mentre l'input proviene da Update (~60-144Hz). Serve un meccanismo per cachare l'input tra i frame.

### Options Considered

**Option A: Simple Struct**
```csharp
struct MovementIntent { Vector2 Input; LocomotionMode Mode; bool IsValid; }
```
- Pro: Minimo overhead, zero allocations
- Con: Nessun tracking temporale

**Option B: Struct with Timestamp**
```csharp
struct MovementIntent {
    Vector2 RawInput;
    Vector3 WorldDirection;  // Pre-calculated
    LocomotionMode Mode;
    float Timestamp;
    bool IsValid;
}
```
- Pro: Stale detection, pre-calcolo direzione
- Con: Leggermente più complesso

**Option C: Class with History**
```csharp
class MovementIntentBuffer { /* circular buffer di intent */ }
```
- Pro: Input smoothing possibile
- Con: Allocations, over-engineering

### Decision
**Option B: Struct with Timestamp**

Motivi:
1. Pre-calcolo `WorldDirection` in Update evita calcoli ripetuti
2. Timestamp permette di rilevare intent stale (>100ms)
3. 48 bytes, stack-allocated, zero GC
4. Semplicità sufficiente senza over-engineering

### Consequences
- ✅ Zero allocations nel hot path
- ✅ Stale detection previene input outdated
- ✅ Pre-calcolo riduce lavoro in FixedUpdate
- ⚠️ Deve essere invalidato in AfterCharacterUpdate

---

## ADR-KCC-002: External Forces System Design

### Status
**Accepted**

### Context
Il combat system richiede forze esterne (knockback, stagger, esplosioni). Queste devono integrarsi con il velocity-based movement di KCC.

### Options Considered

**Option A: Simple Additive**
```csharp
private Vector3 _externalForce;
public void AddForce(Vector3 force) => _externalForce += force;
```
- Pro: Semplicissimo
- Con: Nessun decay, nessuna priorità, forze si accumulano

**Option B: Priority-Based with Modes**
```csharp
struct ForceInstance {
    Vector3 Force;
    ForceMode Mode;  // Instant, Impulse, Continuous
    float Duration;
    float DecayRate;
    int Priority;
}
```
- Pro: Flessibile, combat-ready
- Con: Più complesso

**Option C: Event-Based**
```csharp
event Action<ForceEvent> OnForceApplied;
```
- Pro: Decoupled
- Con: Over-engineering per questo use case

### Decision
**Option B: Priority-Based with Modes**

Motivi:
1. Combat richiede comportamenti diversi (knockback vs stagger vs wind)
2. Priority permette a stagger di "override" knockback minori
3. Decay rate configurabile per feel diversi
4. Pre-allocated list (8 elementi) evita allocations

### Consequences
- ✅ Supporta tutti i use case combat
- ✅ Priority system per interrupt (stagger > knockback)
- ✅ ForceMode.Continuous per effetti ambientali
- ⚠️ Richiede cleanup in AfterCharacterUpdate

---

## ADR-KCC-003: Ground State Exposure

### Status
**Accepted**

### Context
MovementController e altri sistemi (combat, animation) devono accedere allo stato grounding. KCC espone `GroundingStatus` con informazioni dettagliate.

### Options Considered

**Option A: Direct Motor Access**
```csharp
public KinematicCharacterMotor Motor => _motor;
// Usage: controller.Motor.GroundingStatus.IsStableOnGround
```
- Pro: Accesso completo
- Con: Accoppiamento forte, API esposta

**Option B: Wrapper Properties**
```csharp
public bool IsGrounded => _motor.GroundingStatus.IsStableOnGround;
public Vector3 GroundNormal => _motor.GroundingStatus.GroundNormal;
```
- Pro: API pulita, encapsulation
- Con: Richiede aggiunta proprietà per ogni info

**Option C: GroundingInfo Struct**
```csharp
public GroundingInfo GetGroundingInfo() => new GroundingInfo(_motor.GroundingStatus);
```
- Pro: Snapshot consistente
- Con: Allocation se class, copia se struct

### Decision
**Option B: Wrapper Properties**

Motivi:
1. Mantiene API esistente (`IsGrounded` già usato ovunque)
2. Encapsulation: KCC è implementation detail
3. Può esporre progressivamente più info se serve
4. Zero overhead (property inline)

### Consequences
- ✅ API compatibile con codice esistente
- ✅ Encapsulation di KCC internals
- ✅ Più accurato di CharacterController (slope-aware)
- ⚠️ Aggiungere proprietà se servono più info

---

## ADR-KCC-004: Turn-In-Place Coordination

### Status
**Accepted**

### Context
Turn-in-place usa root motion per la rotazione. UpdateRotation() di KCC deve "cedere" il controllo all'animazione durante questo stato.

### Options Considered

**Option A: Flag Check in MovementController**
```csharp
if (_animationController.IsTurnInPlaceActive)
    return; // Skip rotation
```
- Pro: Semplice, diretto
- Con: Coupling con AnimationController

**Option B: Event-Based Handoff**
```csharp
_animationController.OnTurnInPlaceStart += () => _skipRotation = true;
_animationController.OnTurnInPlaceEnd += () => _skipRotation = false;
```
- Pro: Decoupled
- Con: Event management, possibili memory leaks

**Option C: Rotation Source Enum**
```csharp
enum RotationSource { Movement, Animation, External }
```
- Pro: Estensibile
- Con: Over-engineering

### Decision
**Option A: Flag Check in MovementController**

Motivi:
1. AnimationController già ha `IsTurnInPlaceActive`
2. Check diretto è chiaro e debuggable
3. Fallback safety: se flag manca, rotation continua
4. Coupling accettabile (sono parte dello stesso character system)

### Consequences
- ✅ Implementazione semplice
- ✅ Debuggable (flag visibile in Inspector)
- ✅ Fallback safe se AnimationController nullo
- ⚠️ Richiede che AnimationController aggiorni flag correttamente

---

## ADR-KCC-005: ICharacterController Implementation Strategy

### Status
**Accepted**

### Context
`ICharacterController` richiede 9 metodi. Non tutti sono necessari per la nostra implementazione iniziale.

### Decision

**Callbacks con Logica Attiva:**

| Callback | Responsabilità |
|----------|----------------|
| `BeforeCharacterUpdate` | Validate intent, update forces, turn-in-place check |
| `UpdateVelocity` | Core movement (momentum/locked-on) + forces + gravity |
| `UpdateRotation` | Rotation con skip per turn-in-place |
| `PostGroundingUpdate` | Animator parameters update |
| `AfterCharacterUpdate` | Invalidate intent, cleanup forces |

**Stubs per Espansione Futura:**

| Callback | Future Use |
|----------|------------|
| `IsColliderValidForCollisions` | Layer filtering, trigger exclusion |
| `OnGroundHit` | Footstep sounds, ground material |
| `OnMovementHit` | Wall slide, obstacle detection |
| `ProcessHitStabilityReport` | Custom slope stability |
| `OnDiscreteCollisionDetected` | Trigger interactions, damage zones |

### Consequences
- ✅ Implementazione focused su essenziale
- ✅ Stubs pronti per espansione
- ✅ Chiara separazione responsabilità
- ⚠️ Stubs devono restare empty (no `throw NotImplementedException`)

---

## ADR-KCC-006: Velocity vs Position-Based Lock-On

### Status
**Accepted**

### Context
Il lock-on attuale usa position-based movement per orbiting. KCC richiede velocity-based.

### Options Considered

**Option A: Pure Velocity Conversion**
```csharp
Vector3 orbitalVelocity = CalculateOrbitalDirection() * speed;
```
- Pro: Clean integration con KCC
- Con: Possibile drift dalla distanza target

**Option B: Velocity + Position Correction**
```csharp
Vector3 baseVelocity = CalculateOrbitalDirection() * speed;
Vector3 correction = (targetDistance - currentDistance) * correctionFactor;
```
- Pro: Mantiene distanza precisa
- Con: Più complesso

**Option C: Hybrid (strafe = correction, approach = velocity)**
- Pro: Best of both
- Con: Comportamento inconsistente

### Decision
**Option B: Velocity + Position Correction**

Motivi:
1. Distance maintenance è critico per combat feel
2. Correction è piccola e non visibile (soft)
3. Già implementato nel MovementController esistente
4. Tolerance configurabile per fine-tuning

### Consequences
- ✅ Distanza dal target mantenuta
- ✅ Movement fluido (velocity-based primario)
- ✅ Configurabile via tolerance
- ⚠️ Richiede testing per verificare feel

---

## Summary Table

| ADR | Decision | Risk |
|-----|----------|------|
| KCC-001 | Struct with timestamp | Low |
| KCC-002 | Priority-based forces | Low |
| KCC-003 | Wrapper properties | Low |
| KCC-004 | Flag check | Low |
| KCC-005 | Focused + stubs | Low |
| KCC-006 | Velocity + correction | Medium |
