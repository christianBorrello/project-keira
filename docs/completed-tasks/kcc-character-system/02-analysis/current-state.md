# Current State: KCC Character System Analysis

## Executive Summary

L'analisi conferma che l'integrazione KCC è **fattibile con rischio basso**. La maggior parte del codice esistente è riutilizzabile, il refactoring è principalmente spostamento di logica nei callback KCC.

---

## KCC Capabilities Assessment

### Cosa KCC Fornisce (Built-in)
| Feature | Stato | Note |
|---------|-------|------|
| Ground detection | ✅ Ready | `GroundingStatus.IsStableOnGround` |
| Slope handling | ✅ Ready | `MaxStableSlopeAngle` configurabile |
| Step climbing | ✅ Ready | `StepHandling` settings |
| Ledge handling | ✅ Ready | Multiple modes disponibili |
| Collision resolution | ✅ Ready | Rigidbody interaction |
| Interpolation | ✅ Ready | `Settings.Interpolate` |
| Moving platforms | ✅ Ready | `PhysicsMover` support |

### Cosa Dobbiamo Aggiungere
| Feature | Source | Complessità |
|---------|--------|-------------|
| Momentum curves | ADR-001 | BASSA - Pure math |
| Soft pivot | ADR-002 | BASSA - Speed modulation |
| Turn-in-place | ADR-003 | MEDIA - Root motion coordination |
| Lock-on orbital | ADR-004 | MEDIA - Velocity-based conversion |
| External forces | Combat req | BASSA - Additive system |

---

## Code Migration Map

### Completamente Riutilizzabili (13 metodi)
Questi metodi sono pure math/config e non richiedono modifiche:

```
MovementController:
├── GetCameraRelativeDirection()     // Camera-relative input
├── GetSpeedForMode()                // Config lookup
├── CalculateTurnAngle()             // Pure math
├── CalculatePivotFactor()           // Pure math
├── CalculateTurnType()              // Pure math
├── CreateDefaultAccelerationCurve() // Config factory
├── CreateDefaultDecelerationCurve() // Config factory
├── EvaluateAccelerationCurve()      // Curve evaluation
├── EvaluateDecelerationCurve()      // Curve evaluation
├── UpdateMomentumTimers()           // State logic
├── CancelTurnInPlace()              // State logic
├── EnterTurnInPlace()               // State logic
└── ExitTurnInPlace()                // State logic
```

### Da Riorganizzare (4 metodi)
Stessa logica, diverso placement nel lifecycle:

| Metodo Attuale | Nuovo Placement |
|----------------|-----------------|
| `ApplyGravity()` | → `BeforeCharacterUpdate()` |
| `ApplyMomentumRotation()` | → `UpdateRotation()` |
| `HandleTurnInPlace()` | → Split: check in `Before`, rotation in `UpdateRotation` |
| `ShouldTurnInPlace()` | → `BeforeCharacterUpdate()` |

### Da Riscrivere (2 metodi)
Output cambia da position a velocity:

| Metodo Attuale | Nuovo Metodo | Cambio Chiave |
|----------------|--------------|---------------|
| `ApplyMomentumMovement()` | `UpdateVelocity()` | `Move()` → set velocity ref |
| `ApplyLockedOnMovement()` | `UpdateVelocity()` | Position → velocity based |

---

## Critical Translation Pattern

### PRIMA (CharacterController - Push Model)
```csharp
void ApplyMomentumMovement() {
    Vector3 motion = velocity * Time.deltaTime * direction;
    motion.y = _verticalVelocity * Time.deltaTime;
    _characterController.Move(motion);  // ← Push: noi chiamiamo
}
```

### DOPO (KCC - Pull Model)
```csharp
void UpdateVelocity(ref Vector3 currentVelocity, float deltaTime) {
    Vector3 horizontal = speedFactor * direction * speed;
    currentVelocity.x = horizontal.x;
    currentVelocity.z = horizontal.z;
    currentVelocity.y = _verticalVelocity;  // ← Pull: KCC chiama noi
}
```

**Differenza fondamentale**: Non chiamiamo più `.Move()`, settiamo velocity e KCC integra.

---

## Integration Architecture

### Component Change
```csharp
// OLD
[RequireComponent(typeof(CharacterController))]
public class MovementController : MonoBehaviour

// NEW
[RequireComponent(typeof(KinematicCharacterMotor))]
public class MovementController : MonoBehaviour, ICharacterController
```

### Lifecycle Mapping
| Unity Lifecycle | KCC Callback | Nostro Codice |
|-----------------|--------------|---------------|
| - | `BeforeCharacterUpdate()` | Gravity, turn-in-place check |
| - | `UpdateVelocity()` | Momentum/lock-on movement |
| - | `UpdateRotation()` | Rotation logic |
| - | `PostGroundingUpdate()` | Ground state reactions |
| - | `AfterCharacterUpdate()` | Cleanup, reset intent |

### Data Flow
```
PlayerState.Execute()
    │
    ▼
MovementController.SetMovementIntent(input, mode)  // Cache
    │
    ▼ (KCC FixedUpdate)
    │
MovementController.UpdateVelocity(ref velocity)    // Apply cached intent
MovementController.UpdateRotation(ref rotation)
```

---

## Unchanged Components

### Player States - NESSUN CAMBIO API
```csharp
// L'entry point resta identico!
controller.ApplyMovement(moveInput, LocomotionMode.Run);
// Internamente diventa SetMovementIntent(), ma API pubblica uguale
```

### AnimationController - NESSUN CAMBIO
- Tutti i metodi rimangono identici
- Animator parameters invariati
- TurnAngle, Speed, VelocityMagnitude già pronti

### SmoothingState - NESSUN CAMBIO
- Struct-based state tracking
- IsTurningInPlace, timers, etc.

---

## Performance Budget

| Componente | Costo Stimato | Budget |
|------------|---------------|--------|
| Nostro codice (callbacks) | ~0.22ms | < 0.3ms ✅ |
| KCC overhead | 0.3-0.5ms | Fisso |
| **Totale** | ~0.5-0.7ms | < 1ms ✅ |

**Rischio**: BASSO - Sotto budget per singolo player character.

---

## Complexity Assessment Summary

| Aspetto | Complessità | Rischio |
|---------|-------------|---------|
| Movement Logic | BASSA | 🟢 |
| Velocity Integration | MEDIA | 🟡 |
| Rotation | MEDIA | 🟡 |
| Gravity | BASSA | 🟢 |
| Animation | NULLA | 🟢 |
| States | NULLA | 🟢 |
| Smoothing | NULLA | 🟢 |
| Lock-On Conversion | MEDIA | 🟡 |
| External Forces | BASSA | 🟢 |

**Rischio Complessivo: BASSO** - Refactoring è principalmente spostamento di codice esistente.

---

## Key Decisions Needed (for Design Phase)

1. **Intent Caching Strategy**: Struct vs class per MovementIntent?
2. **External Forces API**: List-based vs event-based?
3. **Ground State Exposure**: Wrapper o accesso diretto a `_motor.GroundingStatus`?
4. **Turn-In-Place Handoff**: Come coordinare root motion con UpdateRotation skip?

---

## Ready for Design Phase

L'analisi è completa. Abbiamo:
- ✅ Mappato tutti i metodi da migrare
- ✅ Identificato pattern di traduzione
- ✅ Verificato compatibilità con componenti esistenti
- ✅ Stimato budget performance
- ✅ Classificato rischi

**Prossimo Step**: Phase 3 - Design dell'architettura KCC-based.
