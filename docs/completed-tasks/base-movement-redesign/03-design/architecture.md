# Architecture Design: Base Movement System

## Overview

Sistema di movimento momentum-based con acceleration curves, soft pivot, e turn-in-place con root motion parziale. Ottimizzato per feel 80% Responsive / 20% Realistico.

---

## Architecture Diagram

```
┌─────────────────────────────────────────────────────────────────────┐
│                         INPUT LAYER                                  │
│  InputHandler → GetMoveInput() → Vector2 (normalized)               │
└─────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────────┐
│                    STATE MACHINE (unchanged)                         │
│  PlayerStateMachine → Idle/Walk/Run/Sprint States                   │
│  States call: controller.ApplyMovement(input, mode)                 │
└─────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────────┐
│                    MOVEMENT CONTROLLER (modified)                    │
│  ┌──────────────────────────────────────────────────────────────┐  │
│  │                  ApplyMovement(input, mode)                   │  │
│  │                                                               │  │
│  │  1. IsLockedOn? ──YES──► Use existing lock-on logic          │  │
│  │         │                                                     │  │
│  │        NO                                                     │  │
│  │         ▼                                                     │  │
│  │  2. CalculateTargetDirection(input, camera)                  │  │
│  │         │                                                     │  │
│  │         ▼                                                     │  │
│  │  3. CalculateTurnAngle(currentForward, targetDir)            │  │
│  │         │                                                     │  │
│  │         ▼                                                     │  │
│  │  4. IsStationary AND |turnAngle| > 45°?                      │  │
│  │         │                                                     │  │
│  │        YES ──► HandleTurnInPlace() ──► Root Motion Turn      │  │
│  │         │                                                     │  │
│  │        NO                                                     │  │
│  │         ▼                                                     │  │
│  │  5. ApplyMomentumMovement()                                  │  │
│  │      ├── EvaluateAccelerationCurve()                         │  │
│  │      ├── CalculatePivotFactor(turnAngle)                     │  │
│  │      ├── UpdateVelocity(target * accel * pivot)              │  │
│  │      └── CharacterController.Move(velocity * dt)             │  │
│  │         │                                                     │  │
│  │         ▼                                                     │  │
│  │  6. ApplyRotation() ── Smooth rotate toward target           │  │
│  │         │                                                     │  │
│  │         ▼                                                     │  │
│  │  7. UpdateAnimatorParameters()                               │  │
│  │      ├── Speed (velocity magnitude normalized)               │  │
│  │      ├── TurnAngle (for turn-in-place detection)             │  │
│  │      └── VelocityMagnitude (for transitions)                 │  │
│  └──────────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────────┐
│                    ANIMATION CONTROLLER (modified)                   │
│  ┌──────────────────────────────────────────────────────────────┐  │
│  │  Parameters:                                                  │  │
│  │    Speed (0-2)         ── Locomotion blend                   │  │
│  │    TurnAngle (-180-180) ── Turn-in-place trigger             │  │
│  │    VelocityMagnitude    ── Transition conditions             │  │
│  │    IsAccelerating       ── Start animation trigger           │  │
│  │                                                               │  │
│  │  Root Motion Handling:                                        │  │
│  │    OnAnimatorMove() {                                         │  │
│  │      if (isTurningInPlace)                                    │  │
│  │        ApplyRootMotionRotation()                              │  │
│  │      // Position always from script                          │  │
│  │    }                                                          │  │
│  └──────────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────────┐
│                    ANIMATOR (modified)                               │
│  ┌──────────────────────────────────────────────────────────────┐  │
│  │  Layer 0: Base Layer                                          │  │
│  │    ├── Locomotion Blend Tree (Speed driven)                  │  │
│  │    │     ├── Idle        @ Speed = 0                         │  │
│  │    │     ├── Walk        @ Speed = 0.5                       │  │
│  │    │     ├── Run         @ Speed = 1.0                       │  │
│  │    │     └── Sprint      @ Speed = 2.0                       │  │
│  │    │                                                          │  │
│  │    └── Turn In Place (Sub-State Machine)                     │  │
│  │          ├── Turn90_Left   (TurnAngle < -45)                 │  │
│  │          ├── Turn90_Right  (TurnAngle > 45)                  │  │
│  │          └── Turn180       (|TurnAngle| > 135)               │  │
│  │                                                               │  │
│  │  Layer 1: Locked Locomotion (unchanged)                      │  │
│  └──────────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────────┘
```

---

## Component Responsibilities

### MovementController (Extended)
**Primary**: Apply momentum-based movement with acceleration curves
**Secondary**: Detect and handle turn-in-place scenarios

**New Methods:**
```csharp
// Core momentum system
private void ApplyMomentumMovement(Vector2 input, LocomotionMode mode)
private float EvaluateAccelerationCurve(float timeSinceStart)
private float EvaluateDecelerationCurve(float timeSinceStop)
private float CalculatePivotFactor(float turnAngle)

// Turn-in-place handling
private bool ShouldTurnInPlace(float turnAngle, float currentSpeed)
private void HandleTurnInPlace(float turnAngle)

// Rotation with momentum
private void ApplyRotationWithMomentum(Vector3 targetDirection)
```

**New Serialized Fields:**
```csharp
[Header("Momentum Settings")]
[SerializeField] private AnimationCurve accelerationCurve;
[SerializeField] private AnimationCurve decelerationCurve;
[SerializeField] private float accelerationDuration = 0.2f;
[SerializeField] private float decelerationDuration = 0.15f;

[Header("Pivot Settings")]
[SerializeField] private float pivotAngleThreshold = 60f;
[SerializeField] private float maxPivotSpeedReduction = 0.4f;

[Header("Turn In Place")]
[SerializeField] private float turnInPlaceThreshold = 45f;
[SerializeField] private float turnInPlaceSpeedThreshold = 0.5f;
```

### SmoothingState (Extended)
**New Fields:**
```csharp
// Momentum tracking
public float CurrentVelocityMagnitude;   // Actual speed (0 to max)
public float TargetVelocityMagnitude;    // Desired speed from input
public float AccelerationTimer;          // Time since movement started
public float DecelerationTimer;          // Time since input stopped

// Turn tracking
public float TurnAngle;                  // Angle to target direction
public bool IsTurningInPlace;            // Turn-in-place active flag
public float TurnProgress;               // 0-1 progress through turn
```

### AnimationController (Extended)
**New Responsibilities:**
- Set TurnAngle parameter
- Set VelocityMagnitude parameter
- Handle root motion conditionally for turns

**New Methods:**
```csharp
public void SetTurnAngle(float angle)
public void SetVelocityMagnitude(float velocity)
public void EnableTurnRootMotion()
public void DisableTurnRootMotion()
```

---

## Animation Requirements

### Required Animations (Mixamo)

| Animation | Duration | Root Motion | Priority |
|-----------|----------|-------------|----------|
| Standing Idle | Loop | No | P0 |
| Walking Forward | Loop | No | P0 |
| Running Forward | Loop | No | P0 |
| Sprinting Forward | Loop | No | P0 |
| Standing Turn 90 Left | 0.3-0.5s | Yes (rotation) | P0 |
| Standing Turn 90 Right | 0.3-0.5s | Yes (rotation) | P0 |
| Standing Turn 180 | 0.5-0.7s | Yes (rotation) | P1 |

**P0** = Must have for MVP
**P1** = Nice to have, can use 90° x2 as fallback

### Animator Parameter Configuration

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| Speed | Float | 0 | Locomotion blend (0-2) |
| MoveX | Float | 0 | Lock-on strafe |
| MoveY | Float | 0 | Lock-on forward/back |
| IsLockedOn | Bool | false | Layer switch |
| IsMoving | Bool | false | Locomotion flag |
| **TurnAngle** | Float | 0 | **NEW**: Turn detection (-180 to 180) |
| **VelocityMagnitude** | Float | 0 | **NEW**: Actual speed |

---

## Configurable Parameters (Design-Friendly)

### Acceleration/Deceleration Curves

```
Acceleration Curve (0.2s total):
   1.0 ┤         ●●●●●●●
       │      ●●●
       │    ●●
       │  ●●
   0.0 ┼●●────────────────
       0    0.1   0.2   time

Fast ease-in: reaches 80% speed in first 0.1s
Maintains 80% responsive feel while adding weight

Deceleration Curve (0.15s total):
   1.0 ┼●●●
       │   ●●
       │     ●●
       │       ●●
   0.0 ┤         ●●●●●●●
       0    0.1   0.15  time

Smooth ease-out: visible but not sluggish
```

### Pivot Factor Calculation

```
Angle:     0°    45°    60°    90°   120°   180°
Factor:   1.0   1.0   0.85   0.6   0.45   0.4

Below 45°: No reduction (slight turns)
45-90°: Moderate reduction (pivoting)
90-180°: Strong reduction (almost U-turn)
```

---

## Integration Points

### What Changes
- `MovementController.ApplyMovement()` - Core logic rewrite for unlocked mode
- `SmoothingState` - Add momentum tracking fields
- `AnimationController` - Add parameter setters and root motion handling
- `PlayerAnimatorController.controller` - Add parameters and turn states

### What Stays the Same
- Lock-on movement logic (branch preserved)
- State machine structure
- Combat integration
- Stamina system
- Input system

---

## Risk Mitigation

| Risk | Mitigation |
|------|------------|
| Turn-in-place feels sluggish | Make threshold/duration configurable |
| Pivot too aggressive | Adjustable curves + pivot threshold |
| Root motion glitches | Fallback to script-only rotation |
| Animation mismatch | Speed multiplier system already exists |
| Lock-on regression | Preserve existing code path with isLockedOn branch |
