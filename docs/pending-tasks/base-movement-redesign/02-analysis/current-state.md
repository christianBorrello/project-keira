# Current State Analysis: Base Movement System

## Architecture Overview

### Component Flow
```
InputHandler → PlayerStateMachine → State(Idle/Walk/Run/Sprint) → MovementController → CharacterController
                                                                        ↓
                                                               AnimationController → Animator
```

### Key Components

| Component | Responsibility | File |
|-----------|----------------|------|
| `MovementController` | Movement physics, rotation, smoothing | `Components/MovementController.cs` |
| `AnimationController` | Animator parameter management | `Components/AnimationController.cs` |
| `PlayerStateMachine` | State transitions, input routing | `PlayerStateMachine.cs` |
| `BasePlayerState` | Base class for all player states | `BasePlayerState.cs` |
| `SmoothingState` | Velocity/smoothing data struct | `Data/SmoothingState.cs` |

---

## Current Movement Logic Analysis

### MovementController.ApplyMovement() - Core Logic

```csharp
// Line 134-286 (MovementController.cs)
// Current flow:
1. Get camera-relative direction from input
2. SmoothDamp the direction (movementSmoothTime = 0.08f)
3. Calculate alignment factor (dot product)
4. Reduce speed when misaligned (pivot-in-place)
5. SmoothDamp rotation toward target direction
6. Apply movement via CharacterController.Move()
```

### Identified Pain Points

#### PP1: Linear Smoothing (No Acceleration Curve)
```csharp
// Current: Linear SmoothDamp
_smoothing.SmoothedMoveDirection = Vector3.SmoothDamp(
    _smoothing.SmoothedMoveDirection,
    targetDirection,
    ref _smoothing.MoveDirectionVelocity,
    movementSmoothTime  // 0.08f - too fast, no weight
);
```
**Problem**: SmoothDamp è lineare, non c'è "ease-in" all'inizio del movimento.
**Impact**: Il personaggio parte a velocità quasi istantanea.

#### PP2: Instant Stop (No Deceleration)
```csharp
// When no input (UpdateSmoothingDecay):
if (hasNoInput) {
    _smoothing.SmoothedMoveDirection = Vector3.SmoothDamp(
        ..., Vector3.zero, ..., moveDirectionDecayTime  // 0.02f - very fast
    );
}
```
**Problem**: moveDirectionDecayTime = 0.02f è troppo veloce, stop quasi istantaneo.
**Impact**: Nessuna inerzia, nessun peso percepito.

#### PP3: Alignment Factor (Correct Concept, Wrong Feel)
```csharp
float alignment = (dotProduct + 1f) * 0.5f;
float alignmentFactor = Mathf.Pow(alignment, alignmentFalloffExponent); // exp = 2
speed *= alignmentFactor;
```
**Concept**: Corretto! Riduce velocità quando disallineato.
**Problem**: Applicato alla velocità ma non crea pivot animation.
**Impact**: Il personaggio rallenta ma non fa un turn convincente.

#### PP4: Root Motion Disabled
```csharp
// AnimationController.cs:71
if (_animator != null)
{
    _animator.applyRootMotion = false;
}
```
**Problem**: Root motion sempre disabilitato, impossibile usare turn-in-place animations.
**Impact**: Nessun pivot realistico possibile.

#### PP5: Animation Speed Only (No Blend Tree Angle)
```csharp
// Parameters:
// - Speed (0=idle, 0.5=walk, 1=run, 2=sprint)
// - MoveX, MoveY (solo per lock-on)
// - IsLockedOn
```
**Missing**: Nessun parametro per l'angolo di rotazione o velocità angolare.
**Impact**: L'animator non può sapere quando fare pivot vs smooth turn.

---

## Speed Configuration

| Mode | Speed (units/s) | Animation Speed Match |
|------|-----------------|----------------------|
| Walk | 1.1 | 1.4 |
| Run | 5.0 | 5.0 |
| Sprint | 7.0 | 6.5 |

**Note**: Mismatch tra movement speed e animation natural speed causa foot sliding compensation.

---

## State Machine Analysis

### Locomotion States
- **PlayerIdleState**: Transitions to Walk/Run/Sprint su input
- **PlayerWalkState**: ApplyMovement con LocomotionMode.Walk
- **PlayerRunState**: ApplyMovement con LocomotionMode.Run (default)
- **PlayerSprintState**: ApplyMovement con LocomotionMode.Sprint + stamina drain

### State Transition Pattern
```
Input > 0 → Walk/Run/Sprint (basato su modificatori)
Input = 0 → Idle

No intermediate states per:
- Start locomotion (da idle a movimento)
- Stop locomotion (da movimento a idle)
- Turn-in-place (rotazione da fermo)
- Pivot (cambio direzione significativo)
```

---

## Animator Structure (Current)

### Parameters
| Parameter | Type | Usage |
|-----------|------|-------|
| Speed | Float | Blend tree locomotion (0-2) |
| MoveX | Float | Lock-on strafe (-1 to 1) |
| MoveY | Float | Lock-on forward/back (-1 to 1) |
| IsLockedOn | Bool | Layer switch |
| IsMoving | Bool | Locomotion flag |

### Layers
- Layer 0: Base Layer (unlocked locomotion)
- Layer 1: Locked Locomotion (strafe when locked on)

### Missing
- Turn-in-place animations
- Start/Stop animations
- Angle parameter for pivot detection
- Velocity parameter for acceleration visualization

---

## SmoothingState Analysis

Good design: Consolidates all smoothing data in one struct.

### Current Fields
- `SmoothedMoveDirection`: Current smoothed direction
- `MoveDirectionVelocity`: Velocity for SmoothDamp
- `CurrentAnimatorSpeed`: Smoothed Speed parameter
- `RotationVelocity`: Velocity for rotation smoothing
- `LocalMoveX/Y`: Lock-on direction parameters

### Missing for New System
- `CurrentMomentum`: Per inerzia
- `AccelerationPhase`: 0-1 per curve
- `TurnAngle`: Per pivot detection
- `PivotProgress`: Per pivot animation sync

---

## Integration Points

### Safe to Modify
- `MovementController.ApplyMovement()` - Core logic
- `SmoothingState` - Add new fields
- `AnimationController` - Add parameters, enable root motion conditionally
- Animator Controller - Add states, parameters, transitions

### Must Preserve
- Lock-on logic (linee 159-186, 235-273 in MovementController)
- State machine structure
- Combat state transitions
- CharacterController usage

---

## Opportunities for Improvement

### O1: Introduce Momentum System
Replace linear SmoothDamp with velocity-based momentum.
```
acceleration → current velocity → position
deceleration curve when input stops
```

### O2: Add Turn-in-Place System
Detect angle change when stationary → play turn animation with root motion.

### O3: Add Pivot System
Detect direction change >90° during movement → soft deceleration + smooth curve.

### O4: Animation Curve Parameters
Add to animator:
- `TurnAmount`: Current rotation delta
- `Velocity`: Per transition conditions

### O5: Selective Root Motion
Enable root motion for specific states:
- Turn-in-place
- Start animations
- Stop animations

---

## Summary

**Current System Strengths:**
- Clean separation of concerns
- Working lock-on system
- Proper smoothing infrastructure
- Animation speed matching

**Current System Weaknesses:**
- No acceleration/deceleration curves (instant movement)
- No momentum/inertia
- No turn-in-place animations
- No pivot detection/handling
- Root motion always disabled
- Animator lacks directional/velocity parameters

**Recommendation:**
Extend MovementController with momentum-based system, keeping lock-on logic intact. Add animator parameters for turn detection. Implement soft pivot with deceleration curve.
