# Implementation Workflow: KCC Character System

## Overview

Piano di implementazione incrementale per l'integrazione KCC. Ogni fase è indipendente e testabile.

---

## Phase Summary

| Phase | Descrizione | Files Coinvolti | Rischio | Quality Gate |
|-------|-------------|-----------------|---------|--------------|
| 1 | KCC Core Setup | PlayerController, MovementController | Low | Character moves with WASD |
| 2 | Movement Intent | MovementController | Low | Intent cached correctly |
| 3 | Velocity Calculation | MovementController | Medium | Momentum curves work |
| 4 | Rotation Logic | MovementController | Low | Smooth rotation |
| 5 | External Forces | ExternalForcesManager (NEW) | Low | Knockback works |
| 6 | Lock-On Integration | MovementController | Medium | Orbital movement works |
| 7 | Cleanup & Polish | All | Low | All tests pass |

**Total Phases**: 7
**Estimated Effort**: ~15-20 ore di sviluppo

---

## Phase 1: KCC Core Setup

### Objective
Sostituire CharacterController con KinematicCharacterMotor e implementare stub ICharacterController.

### Tasks

#### 1.1 Add KCC Component to Player
**File**: `Assets/_Scripts/Player/PlayerController.cs`

```csharp
// BEFORE
[RequireComponent(typeof(CharacterController))]

// AFTER
[RequireComponent(typeof(KinematicCharacterMotor))]
```

- [ ] Cambiare RequireComponent
- [ ] Aggiungere reference a KinematicCharacterMotor
- [ ] Rimuovere reference a CharacterController

#### 1.2 Implement ICharacterController Stubs
**File**: `Assets/_Scripts/Player/Components/MovementController.cs`

```csharp
public class MovementController : MonoBehaviour, ICharacterController
{
    private KinematicCharacterMotor _motor;

    private void Awake()
    {
        _motor = GetComponent<KinematicCharacterMotor>();
        _motor.CharacterController = this;
    }

    // All ICharacterController methods as stubs initially
    public void BeforeCharacterUpdate(float deltaTime) { }
    public void UpdateVelocity(ref Vector3 currentVelocity, float deltaTime) { }
    public void UpdateRotation(ref Quaternion currentRotation, float deltaTime) { }
    public void PostGroundingUpdate(float deltaTime) { }
    public void AfterCharacterUpdate(float deltaTime) { }
    public bool IsColliderValidForCollisions(Collider coll) => true;
    public void OnGroundHit(...) { }
    public void OnMovementHit(...) { }
    public void ProcessHitStabilityReport(...) { }
    public void OnDiscreteCollisionDetected(Collider hitCollider) { }
}
```

- [ ] Aggiungere `ICharacterController` interface
- [ ] Cachare `_motor` reference
- [ ] Wiring `_motor.CharacterController = this`
- [ ] Implementare tutti i metodi come stub

#### 1.3 Update IsGrounded Property
**File**: `Assets/_Scripts/Player/Components/MovementController.cs`

```csharp
// BEFORE
public bool IsGrounded => _characterController.isGrounded;

// AFTER
public bool IsGrounded => _motor?.GroundingStatus.IsStableOnGround ?? false;
```

- [ ] Sostituire property IsGrounded
- [ ] Verificare che combat system continui a funzionare

### Quality Gate
- [ ] Player prefab ha KinematicCharacterMotor component
- [ ] No compile errors
- [ ] Character esiste in scena (anche se non si muove ancora)

---

## Phase 2: Movement Intent Caching

### Objective
Implementare il caching dell'input tra Update e FixedUpdate.

### Tasks

#### 2.1 Add MovementIntent Struct
**File**: `Assets/_Scripts/Player/Components/MovementController.cs`

```csharp
private struct MovementIntent
{
    public Vector2 RawInput;
    public Vector3 WorldDirection;
    public LocomotionMode Mode;
    public float Timestamp;
    public bool IsValid;

    public bool IsStale => IsValid && (Time.time - Timestamp > 0.1f);

    public void Invalidate()
    {
        IsValid = false;
        RawInput = Vector2.zero;
        WorldDirection = Vector3.zero;
    }
}

private MovementIntent _intent;
```

- [ ] Aggiungere struct MovementIntent
- [ ] Aggiungere field `_intent`

#### 2.2 Modify ApplyMovement to Cache Intent
**File**: `Assets/_Scripts/Player/Components/MovementController.cs`

```csharp
public void ApplyMovement(Vector2 moveInput, LocomotionMode mode)
{
    // Cache intent instead of immediate application
    _intent = new MovementIntent
    {
        RawInput = moveInput,
        WorldDirection = GetCameraRelativeDirection(moveInput),
        Mode = mode,
        Timestamp = Time.time,
        IsValid = moveInput.sqrMagnitude > 0.001f
    };
}
```

- [ ] Modificare ApplyMovement per cachare intent
- [ ] Pre-calcolare WorldDirection

#### 2.3 Implement AfterCharacterUpdate
**File**: `Assets/_Scripts/Player/Components/MovementController.cs`

```csharp
public void AfterCharacterUpdate(float deltaTime)
{
    _intent.Invalidate();
}
```

- [ ] Invalidare intent dopo ogni frame KCC

### Quality Gate
- [ ] Intent viene cachato quando premo WASD
- [ ] Intent viene invalidato dopo ogni FixedUpdate
- [ ] Debug.Log mostra intent corretto

---

## Phase 3: Velocity Calculation (Momentum)

### Objective
Portare la logica momentum in UpdateVelocity().

### Tasks

#### 3.1 Implement UpdateVelocity Core
**File**: `Assets/_Scripts/Player/Components/MovementController.cs`

```csharp
public void UpdateVelocity(ref Vector3 currentVelocity, float deltaTime)
{
    if (!_intent.IsValid || _intent.IsStale)
    {
        // Deceleration
        ApplyDeceleration(ref currentVelocity, deltaTime);
        return;
    }

    // Normal momentum-based movement
    ApplyMomentumVelocity(ref currentVelocity, deltaTime);

    // Gravity (if airborne)
    if (!_motor.GroundingStatus.IsStableOnGround)
    {
        currentVelocity.y += _gravity * deltaTime;
    }
}
```

- [ ] Implementare UpdateVelocity base
- [ ] Integrare gravity handling

#### 3.2 Port Momentum Logic
**File**: `Assets/_Scripts/Player/Components/MovementController.cs`

Riutilizzare metodi esistenti:
- `EvaluateAccelerationCurve()` ✅ (già pronto)
- `EvaluateDecelerationCurve()` ✅ (già pronto)
- `CalculatePivotFactor()` ✅ (già pronto)
- `GetSpeedForMode()` ✅ (già pronto)

```csharp
private void ApplyMomentumVelocity(ref Vector3 velocity, float deltaTime)
{
    float targetSpeed = GetSpeedForMode(_intent.Mode);
    float pivotFactor = CalculatePivotFactor(_intent.WorldDirection);

    // Apply momentum timer logic
    UpdateMomentumTimers(deltaTime, _intent.RawInput.magnitude);

    float speedFactor = EvaluateAccelerationCurve(_smoothing.AccelerationTimer);
    speedFactor *= pivotFactor;

    Vector3 targetVelocity = _intent.WorldDirection * targetSpeed * speedFactor;

    velocity.x = targetVelocity.x;
    velocity.z = targetVelocity.z;
    // Note: y handled separately (gravity or grounded)
}
```

- [ ] Creare ApplyMomentumVelocity()
- [ ] Portare logica da ApplyMomentumMovement()
- [ ] Rimuovere `.Move()` call

#### 3.3 Handle Deceleration
```csharp
private void ApplyDeceleration(ref Vector3 velocity, float deltaTime)
{
    UpdateMomentumTimers(deltaTime, 0f);
    float decelerationFactor = EvaluateDecelerationCurve(_smoothing.DecelerationTimer);

    velocity.x *= decelerationFactor;
    velocity.z *= decelerationFactor;
}
```

- [ ] Implementare deceleration logic

### Quality Gate
- [ ] Character si muove con WASD
- [ ] Accelerazione visibile (curva)
- [ ] Decelerazione smooth quando rilascio input
- [ ] Pivot speed reduction funziona

---

## Phase 4: Rotation Logic

### Objective
Portare la logica rotazione in UpdateRotation().

### Tasks

#### 4.1 Implement UpdateRotation
**File**: `Assets/_Scripts/Player/Components/MovementController.cs`

```csharp
public void UpdateRotation(ref Quaternion currentRotation, float deltaTime)
{
    // Skip if turn-in-place is active
    if (_animationController?.IsTurnInPlaceActive ?? false)
        return;

    // Skip if no valid direction
    if (!_intent.IsValid || _intent.WorldDirection.sqrMagnitude < 0.01f)
        return;

    // Calculate and apply rotation
    Quaternion targetRotation = Quaternion.LookRotation(_intent.WorldDirection);
    currentRotation = Quaternion.Slerp(
        currentRotation,
        targetRotation,
        _rotationSpeed * deltaTime
    );
}
```

- [ ] Implementare UpdateRotation
- [ ] Aggiungere skip per turn-in-place
- [ ] Smooth rotation con Slerp

#### 4.2 Port Turn-In-Place Check
Riutilizzare:
- `ShouldTurnInPlace()` ✅ (già pronto)
- `CalculateTurnAngle()` ✅ (già pronto)

```csharp
public void BeforeCharacterUpdate(float deltaTime)
{
    // Check turn-in-place conditions
    if (ShouldTurnInPlace())
    {
        EnterTurnInPlace();
    }
}
```

- [ ] Aggiungere turn-in-place check a BeforeCharacterUpdate
- [ ] Verificare handoff con AnimationController

### Quality Gate
- [ ] Character ruota verso direzione movimento
- [ ] Rotazione è smooth
- [ ] Turn-in-place triggered quando fermo + grande angolo

---

## Phase 5: External Forces System

### Objective
Implementare ExternalForcesManager e integrare con combat.

### Tasks

#### 5.1 Create ExternalForcesManager
**File**: `Assets/_Scripts/Player/Components/ExternalForcesManager.cs` (NEW)

```csharp
public class ExternalForcesManager : MonoBehaviour
{
    private List<ForceInstance> _activeForces = new(8);

    public void AddForce(Vector3 force, ForceMode mode,
        float duration = 0.3f, float decayRate = 0f, int priority = 0)
    {
        // Implementation from architecture.md
    }

    public Vector3 GetCombinedForce(float deltaTime)
    {
        // Implementation from architecture.md
    }

    public void Clear() => _activeForces.Clear();
}
```

- [ ] Creare nuovo file ExternalForcesManager.cs
- [ ] Implementare ForceInstance struct
- [ ] Implementare AddForce(), GetCombinedForce(), Clear()

#### 5.2 Integrate with MovementController
**File**: `Assets/_Scripts/Player/Components/MovementController.cs`

```csharp
private ExternalForcesManager _forcesManager;

public void UpdateVelocity(ref Vector3 currentVelocity, float deltaTime)
{
    // ... existing momentum logic ...

    // Add external forces
    currentVelocity += _forcesManager.GetCombinedForce(deltaTime);
}
```

- [ ] Aggiungere reference a ExternalForcesManager
- [ ] Integrare in UpdateVelocity

#### 5.3 Expose API for Combat
**File**: `Assets/_Scripts/Player/Components/MovementController.cs`

```csharp
public void AddExternalForce(Vector3 force, ForceMode mode,
    float duration, float decayRate, int priority)
{
    _forcesManager.AddForce(force, mode, duration, decayRate, priority);
}

public void ClearExternalForces() => _forcesManager.Clear();
```

- [ ] Esporre API pubblica
- [ ] Connettere a HealthPoiseController per stagger

### Quality Gate
- [ ] Test knockback: applica forza, character si muove
- [ ] Test decay: forza diminuisce nel tempo
- [ ] Test priority: stagger override knockback normale

---

## Phase 6: Lock-On Integration

### Objective
Portare movimento lock-on a velocity-based con distance correction.

### Tasks

#### 6.1 Implement Lock-On Velocity
**File**: `Assets/_Scripts/Player/Components/MovementController.cs`

```csharp
public void UpdateVelocity(ref Vector3 currentVelocity, float deltaTime)
{
    if (_lockOnController?.IsLockedOn ?? false)
    {
        ApplyLockedOnVelocity(ref currentVelocity, deltaTime);
    }
    else
    {
        ApplyMomentumVelocity(ref currentVelocity, deltaTime);
    }

    // External forces and gravity...
}

private void ApplyLockedOnVelocity(ref Vector3 velocity, float deltaTime)
{
    Transform target = _lockOnController.CurrentTarget;
    if (target == null) return;

    // Calculate orbital direction
    Vector3 toTarget = target.position - transform.position;
    toTarget.y = 0;
    float currentDistance = toTarget.magnitude;

    // Strafe (perpendicular to target)
    Vector3 strafeDir = Vector3.Cross(Vector3.up, toTarget.normalized);
    Vector3 approachDir = toTarget.normalized;

    // Apply input
    Vector3 moveDir = strafeDir * _intent.RawInput.x + approachDir * _intent.RawInput.y;

    // Distance correction
    float targetDistance = _lockOnDesiredDistance;
    float distanceError = currentDistance - targetDistance;
    Vector3 correction = approachDir * distanceError * _distanceCorrectionFactor;

    // Final velocity
    float speed = GetSpeedForMode(_intent.Mode);
    velocity = (moveDir.normalized * speed) + correction;
}
```

- [ ] Creare ApplyLockedOnVelocity()
- [ ] Implementare orbital direction calculation
- [ ] Aggiungere distance correction

#### 6.2 Lock-On Rotation (Face Target)
**File**: `Assets/_Scripts/Player/Components/MovementController.cs`

```csharp
public void UpdateRotation(ref Quaternion currentRotation, float deltaTime)
{
    if (_animationController?.IsTurnInPlaceActive ?? false)
        return;

    // Lock-on: always face target
    if (_lockOnController?.IsLockedOn ?? false)
    {
        Vector3 toTarget = _lockOnController.CurrentTarget.position - transform.position;
        toTarget.y = 0;
        if (toTarget.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(toTarget);
            currentRotation = Quaternion.Slerp(currentRotation, targetRotation,
                _rotationSpeed * 2f * deltaTime); // Faster for lock-on
        }
        return;
    }

    // Normal rotation...
}
```

- [ ] Aggiungere lock-on rotation handling
- [ ] Face target sempre quando locked on

### Quality Gate
- [ ] Strafe attorno al target funziona
- [ ] Approach/retreat funzionano
- [ ] Distanza dal target mantenuta durante strafe
- [ ] Character always facing target

---

## Phase 7: Cleanup & Polish

### Objective
Rimuovere codice obsoleto, verificare tutti i comportamenti.

### Tasks

#### 7.1 Remove Old CharacterController Code
**Files**: MovementController.cs, PlayerController.cs

- [ ] Rimuovere `_characterController` field e references
- [ ] Rimuovere tutti i `.Move()` calls
- [ ] Rimuovere gravity handling manuale (se duplicato)
- [ ] Cleanup imports non usati

#### 7.2 Update Player States
**Files**: PlayerIdleState.cs, PlayerWalkState.cs, PlayerRunState.cs, PlayerSprintState.cs

- [ ] Verificare che tutte le chiamate a `ApplyMovement()` funzionino
- [ ] Verificare IsGrounded checks
- [ ] Test ogni stato

#### 7.3 Update AnimationController Integration
**File**: AnimationController.cs

- [ ] Verificare che PostGroundingUpdate() aggiorni parametri correttamente
- [ ] Test blend tree
- [ ] Test transizioni

#### 7.4 Final Testing
- [ ] Test full walk/run/sprint cycle
- [ ] Test pivot durante movimento
- [ ] Test turn-in-place da idle
- [ ] Test lock-on strafe/approach
- [ ] Test knockback durante combat
- [ ] Test gravity (jump, fall)
- [ ] Test slope handling (KCC default)

### Quality Gate
- [ ] No compile warnings
- [ ] Tutti i test manuali passano
- [ ] Performance profiler: < 1ms per character
- [ ] No GC allocations in hot path

---

## Rollback Strategy

Ogni phase ha commit atomico. Rollback:

```bash
# Rollback a fase precedente
git log --oneline  # trova commit
git revert <commit-hash>

# Rollback completo
git checkout feature/base-movement-redesign~N  # N = numero fasi
```

---

## Validation Checklist (Final)

### Functional
- [ ] Walk/Run/Sprint con momentum curves
- [ ] Pivot speed reduction
- [ ] Turn-in-place da idle
- [ ] Lock-on orbital movement
- [ ] External forces (knockback)
- [ ] Gravity e ground detection

### Performance
- [ ] < 1ms per character update
- [ ] Zero GC allocations in Update/FixedUpdate
- [ ] Animator parameters cached

### Compatibility
- [ ] Combat system funziona (stagger, dodge)
- [ ] Animation blend tree corretto
- [ ] Camera system invariato
