# Agent Report: KCC + MovementController Integration Analysis

## Summary

Analisi completa del sistema KCC e del MovementController esistente per mappare l'integrazione.

---

## KCC Interface Requirements

L'`ICharacterController` richiede 9 metodi:

```csharp
void UpdateVelocity(ref Vector3 currentVelocity, float deltaTime);
void UpdateRotation(ref Quaternion currentRotation, float deltaTime);
void BeforeCharacterUpdate(float deltaTime);
void PostGroundingUpdate(float deltaTime);
void AfterCharacterUpdate(float deltaTime);
void OnGroundHit(Collider, Vector3, Vector3, ref HitStabilityReport);
void OnMovementHit(Collider, Vector3, Vector3, ref HitStabilityReport);
void ProcessHitStabilityReport(...);
void OnDiscreteCollisionDetected(Collider);
bool IsColliderValidForCollisions(Collider);
```

---

## MovementController Methods Classification

### Completamente Riutilizzabili (Copy-Paste Safe)

| Metodo | Categoria |
|--------|-----------|
| `GetCameraRelativeDirection()` | Pure Math |
| `GetSpeedForMode()` | Config |
| `CalculateTurnAngle()` | Pure Math |
| `CalculatePivotFactor()` | Pure Math |
| `CalculateTurnType()` | Pure Math |
| `CreateDefaultAccelerationCurve()` | Config |
| `CreateDefaultDecelerationCurve()` | Config |
| `EvaluateAccelerationCurve()` | Pure Math |
| `EvaluateDecelerationCurve()` | Pure Math |
| `UpdateMomentumTimers()` | State Logic |
| `CancelTurnInPlace()` | State Logic |
| `EnterTurnInPlace()` | State Logic |
| `ExitTurnInPlace()` | State Logic |

### Da Riorganizzare (Logica uguale, diverso placement)

| Metodo | Nuovo Placement |
|--------|-----------------|
| `ApplyGravity()` | → `BeforeCharacterUpdate()` |
| `ApplyMomentumRotation()` | → `UpdateRotation()` |
| `HandleTurnInPlace()` | → `BeforeCharacterUpdate()` + `UpdateRotation()` |
| `ShouldTurnInPlace()` | → `BeforeCharacterUpdate()` |

### Da Riscrivere (Estrai logica, cambia output)

| Metodo | Cambiamento |
|--------|-------------|
| `ApplyMomentumMovement()` | Estrai momentum logic → `UpdateVelocity()` |
| `ApplyLockedOnMovement()` | Estrai lock-on logic → `UpdateVelocity()` |

---

## Critical Translation: Movement to KCC

### Attualmente (CharacterController):
```csharp
Vector3 motion = velocity * Time.deltaTime * direction;
motion.y = _verticalVelocity * Time.deltaTime;
_characterController.Move(motion);
```

### Con KCC (UpdateVelocity callback):
```csharp
void UpdateVelocity(ref Vector3 currentVelocity, float deltaTime) {
    Vector3 horizontalVelocity = speedFactor * direction * speed;
    currentVelocity.x = horizontalVelocity.x;
    currentVelocity.z = horizontalVelocity.z;
    currentVelocity.y = _verticalVelocity;
}
```

**Differenza chiave**:
- CharacterController: chiami `.Move(motion)` ogni frame
- KCC: setti velocity in callback, KCC applica integrazione

---

## Integration Points - Cosa Cambia

### Component Change
```csharp
// OLD:
[RequireComponent(typeof(CharacterController))]
private CharacterController _characterController;

// NEW:
[RequireComponent(typeof(KinematicCharacterMotor))]
public class MovementController : MonoBehaviour, ICharacterController
private KinematicCharacterMotor _motor;
```

### Player States - NESSUN CAMBIO
```csharp
// Rimane identico - l'entry point non cambia!
controller.ApplyMovement(moveInput, LocomotionMode.Run);
```

### AnimationController - NESSUN CAMBIO
Tutti i metodi rimangono identici.

---

## Turn-In-Place con KCC

```csharp
void UpdateRotation(ref Quaternion currentRotation, float deltaTime) {
    if (_smoothing.IsTurningInPlace) {
        // Let OnAnimatorMove handle root motion rotation
        currentRotation = transform.rotation;
        return;
    }
    // ... normal rotation code ...
}
```

---

## Lifecycle Mapping

| Attuale | Con KCC |
|---------|---------|
| `Update() { ApplyGravity(); }` | `BeforeCharacterUpdate() { ApplyGravity(); }` |
| `ApplyMomentumMovement()` | `UpdateVelocity()` |
| `ApplyMomentumRotation()` | `UpdateRotation()` |
| Direct `transform.rotation` | Via callback ref parameter |

---

## Complexity Assessment

| Aspetto | Complessità |
|---------|-------------|
| Movement Logic | BASSA |
| Velocity Integration | MEDIA |
| Rotation | MEDIA |
| Gravity | BASSA |
| Animation | NULLA |
| States | NULLA |
| Smoothing | NULLA |

**Rischio Globale: BASSO** - Refactoring è principalmente spostamento di codice.
