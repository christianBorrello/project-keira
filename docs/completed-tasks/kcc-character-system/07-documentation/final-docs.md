# KCC Character System - API Documentation

**Version**: 1.0.0
**Last Updated**: 2025-12-22
**Status**: Production Ready

---

## Overview

The KCC Character System provides a robust, physics-based movement solution built on top of the Kinematic Character Controller (KCC) library. It features:

- **Momentum-based movement** with configurable acceleration/deceleration curves
- **Lock-on orbital movement** for combat scenarios (strafe, approach, retreat)
- **Turn-in-place** system for responsive directional changes
- **External forces** support for knockback, explosions, and environmental effects
- **Soft pivot** speed modulation for natural turning behavior

---

## Architecture

```
PlayerController
    └── MovementController : ICharacterController
            ├── KinematicCharacterMotor (KCC)
            ├── ExternalForcesManager
            ├── AnimationController
            └── LockOnController
```

**Key Principle**: KCC handles physics (collisions, grounding, slopes). MovementController handles game logic (momentum, rotation, lock-on).

---

## MovementController

### Namespace
```csharp
using _Scripts.Player.Components;
```

### Class Signature
```csharp
[RequireComponent(typeof(PlayerController))]
[RequireComponent(typeof(KinematicCharacterMotor))]
public class MovementController : MonoBehaviour, ICharacterController
```

### Public Properties

| Property | Type | Description |
|----------|------|-------------|
| `IsGrounded` | `bool` | Whether character is on stable ground (from KCC) |
| `VerticalVelocity` | `float` | Current vertical velocity (gravity) |
| `ExternalForces` | `ExternalForcesManager` | Access to external forces system |

### Public Methods

#### ApplyMovement
```csharp
public void ApplyMovement(Vector2 moveInput, LocomotionMode mode)
```
Main entry point for movement input. Call this from PlayerState.Execute().

**Parameters**:
- `moveInput`: Raw input from InputHandler (-1 to 1 on each axis)
- `mode`: `LocomotionMode.Walk`, `Run`, or `Sprint`

**Example**:
```csharp
// In PlayerMoveState.Execute()
var input = _player.InputHandler.MoveInput;
var mode = _player.InputHandler.IsSprintHeld ? LocomotionMode.Sprint : LocomotionMode.Run;
_player.MovementController.ApplyMovement(input, mode);
```

#### CancelTurnInPlace
```csharp
public void CancelTurnInPlace()
```
Immediately cancels any in-progress turn-in-place animation. Use when combat actions interrupt movement.

**Example**:
```csharp
// In PlayerAttackState.Enter()
_player.MovementController.CancelTurnInPlace();
```

#### ResetAnimationSpeed
```csharp
public void ResetAnimationSpeed()
```
Resets animator speed parameters to defaults. Use after state transitions that may have modified animation speed.

---

## ExternalForcesManager

### Namespace
```csharp
using _Scripts.Player.Components;
```

### Class Signature
```csharp
public class ExternalForcesManager : MonoBehaviour
```

### Force Types

```csharp
public enum ForceType
{
    Instant,    // Applied once, consumed immediately (explosions)
    Impulse,    // Applied once, decays over duration (knockback)
    Continuous  // Applied every frame while active (wind, currents)
}
```

### Public Methods

#### AddInstantForce
```csharp
public void AddInstantForce(Vector3 force)
```
Applies a one-frame force. Use for explosions, landing impacts.

**Parameters**:
- `force`: World-space force vector (direction * magnitude)

**Example**:
```csharp
// Explosion impact
var explosionDir = (player.position - explosionCenter).normalized;
player.MovementController.ExternalForces.AddInstantForce(explosionDir * 15f);
```

#### AddImpulse
```csharp
public void AddImpulse(Vector3 direction, float magnitude, float duration, AnimationCurve decayCurve = null)
```
Applies a force that decays over time. Use for knockback, stagger.

**Parameters**:
- `direction`: Normalized direction vector
- `magnitude`: Initial force strength (m/s)
- `duration`: Time in seconds for force to decay
- `decayCurve`: Optional custom decay curve (null = default impulse curve)

**Bounds**: Magnitude clamped to 100 m/s, duration clamped to 10s.

**Example**:
```csharp
// Combat knockback
var knockbackDir = -player.transform.forward;
player.MovementController.ExternalForces.AddImpulse(knockbackDir, 8f, 0.4f);
```

#### AddKnockback
```csharp
public void AddKnockback(Vector3 direction, float distance, float duration = 0.3f)
```
Convenience method for standard combat knockback with optimized decay curve.

**Parameters**:
- `direction`: Knockback direction (will be normalized)
- `distance`: Approximate distance to push (meters)
- `duration`: Knockback duration (default 0.3s)

**Example**:
```csharp
// Enemy attack hits player
var knockbackDir = (player.position - enemy.position).normalized;
player.MovementController.ExternalForces.AddKnockback(knockbackDir, 2f);
```

#### AddContinuousForce
```csharp
public void AddContinuousForce(Vector3 direction, float magnitude, float duration)
```
Applies a constant force over duration. Use for wind, water currents.

**Parameters**:
- `direction`: Normalized direction vector
- `magnitude`: Force strength (m/s)
- `duration`: Time in seconds the force is active

**Example**:
```csharp
// Wind zone effect
var windDir = Vector3.right;
player.MovementController.ExternalForces.AddContinuousForce(windDir, 3f, 5f);
```

#### Clear
```csharp
public void Clear()
```
Removes all active forces immediately. Use for respawning, teleporting.

### Public Properties

| Property | Type | Description |
|----------|------|-------------|
| `HasActiveForces` | `bool` | Whether any forces are currently active |
| `ActiveForceCount` | `int` | Number of active forces |

---

## Enums

### LocomotionMode
```csharp
public enum LocomotionMode
{
    Walk,   // Slow movement (Control held) - 1.1 m/s default
    Run,    // Default movement - 5 m/s default
    Sprint  // Fast movement (Shift held) - 7 m/s default
}
```

### TurnType
```csharp
public enum TurnType
{
    None = 0,
    Turn90Left = 1,
    Turn90Right = 2,
    Turn180 = 3
}
```
Used by AnimationController for turn-in-place animation selection.

---

## Configuration (Inspector)

### Movement Speeds
| Field | Default | Description |
|-------|---------|-------------|
| `walkSpeed` | 1.1 | Walking speed (m/s) |
| `runSpeed` | 5.0 | Running speed (m/s) |
| `sprintSpeed` | 7.0 | Sprinting speed (m/s) |

### Momentum System
| Field | Default | Description |
|-------|---------|-------------|
| `accelerationDuration` | 0.2s | Time to reach full speed |
| `decelerationDuration` | 0.15s | Time to stop from full speed |
| `accelerationCurve` | 80/20 responsive | Custom acceleration curve |
| `decelerationCurve` | smooth ease-out | Custom deceleration curve |

### Lock-On Movement
| Field | Default | Description |
|-------|---------|-------------|
| `lockedOnAccelerationDuration` | 0.4s | Slower acceleration when locked |
| `lockedOnDecelerationDuration` | 0.35s | Slower deceleration when locked |
| `lockedOnSpeed` | 4.5 | Movement speed when locked on |

### Turn-In-Place
| Field | Default | Description |
|-------|---------|-------------|
| `turnInPlaceMinAngle` | 45° | Minimum angle to trigger turn-in-place |
| `turnInPlaceExitAngle` | 15° | Angle threshold to complete turn |

### Soft Pivot
| Field | Default | Description |
|-------|---------|-------------|
| `pivotAngleThreshold` | 60° | Angle at which pivot speed reduction starts |
| `maxPivotSpeedReduction` | 0.4 | Maximum speed reduction factor (0.4 = 60% of normal speed) |

---

## Integration Guide

### Setup Checklist

1. **Add Components** (in order):
   - `KinematicCharacterMotor` on Player
   - `MovementController` on Player (auto-adds ExternalForcesManager)
   - Remove legacy `CharacterController` if present

2. **Configure KCC Motor**:
   ```
   Capsule Height: 2.0
   Capsule Radius: 0.5
   Max Stable Slope Angle: 60
   Max Step Height: 0.5
   ```

3. **Wire References**:
   - MovementController auto-finds PlayerController, AnimationController, LockOnController
   - Camera transform auto-found via Camera.main

### State Machine Integration

```csharp
// PlayerMoveState.cs
public override void Execute()
{
    base.Execute();

    var input = _player.InputHandler.MoveInput;
    var mode = GetLocomotionMode();

    _player.MovementController.ApplyMovement(input, mode);
}

private LocomotionMode GetLocomotionMode()
{
    if (_player.InputHandler.IsWalkHeld) return LocomotionMode.Walk;
    if (_player.InputHandler.IsSprintHeld) return LocomotionMode.Sprint;
    return LocomotionMode.Run;
}
```

### Combat Integration

```csharp
// CombatSystem.cs - OnPlayerHit
public void ApplyKnockback(PlayerController player, Vector3 attackerPos, float force)
{
    var dir = (player.transform.position - attackerPos).normalized;
    player.MovementController.ExternalForces.AddKnockback(dir, force);

    // Cancel any turn-in-place
    player.MovementController.CancelTurnInPlace();
}
```

### Lock-On Integration

Lock-on mode is auto-detected via `LockOnController.IsLockedOn`. When locked:
- Character always faces target
- Strafe movement (A/D) orbits around target at locked distance
- Forward/backward (W/S) adjusts distance to target
- Distance is maintained via velocity-based correction (respects collisions)

---

## Robustness Features

### Input Validation
- All Vector2/Vector3 inputs validated for NaN/Infinity
- Invalid inputs clamped to zero with debug warning
- Camera calculation results validated

### Force Buffer Protection
- Maximum 8 concurrent forces (configurable)
- Priority-based culling when at capacity
- Oldest-force tiebreaker for equal priorities
- Paranoid overflow check (2x capacity = emergency clear)

### Magnitude Bounds
- Force magnitude clamped to 100 m/s
- Force duration clamped to 10 seconds
- Values below threshold (0.1) ignored

---

## Performance Notes

- **Frame Budget**: ~0.21ms estimated (well under 0.5ms target)
- **Zero Allocations**: MovementIntent is a struct, ForceInstance is a struct
- **Cached Calculations**: Camera direction, target vectors pre-calculated in Update
- **Predicate Optimization**: RemoveAll uses predicate (no allocation in Unity 2022+)

---

## Troubleshooting

### Character doesn't move
1. Check `IsGrounded` - may be in air
2. Verify `ApplyMovement()` is being called from state
3. Check KCC Motor is enabled
4. Look for NaN warnings in console

### Rotation doesn't work
1. Rotation is handled in KCC's `UpdateRotation()` callback
2. Don't set `transform.rotation` directly - KCC will overwrite
3. Ensure `_smoothing.SmoothedMoveDirection` has valid data

### Knockback doesn't push
1. Check `ExternalForces.HasActiveForces`
2. Verify magnitude > threshold (0.1)
3. Look for validation warnings in console
4. Ensure not at force buffer capacity

### Lock-on strafe feels off
1. Distance correction is velocity-based (not teleport)
2. Adjust `lockedOnSpeed` for orbit speed
3. Check `LockOnController.IsLockedOn` is true

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 1.0.0 | 2025-12-22 | Initial release with KCC integration, momentum, lock-on, external forces |

