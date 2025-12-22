# Implementation Workflow: Base Movement Redesign

## Overview

Implementation in 6 phases, ordered by dependency and risk.

**Total Tasks**: 16
**Estimated Code Changes**: ~300-400 lines new/modified

---

## Phase 1: Data Layer Extension (Low Risk)

### Task 1.1: Extend SmoothingState
**File**: `Assets/_Scripts/Player/Data/SmoothingState.cs`
**Changes**:
- Add momentum tracking fields
- Add turn tracking fields
- Update CreateDefault() and Reset()

**New Fields**:
```csharp
// Momentum
public float CurrentVelocityMagnitude;
public float TargetVelocityMagnitude;
public float AccelerationTimer;
public float DecelerationTimer;

// Turn tracking
public float TurnAngle;
public bool IsTurningInPlace;
public float TurnProgress;
```

**Quality Gate**: Compiles without errors

### Task 1.2: Add Configuration Fields to MovementController
**File**: `Assets/_Scripts/Player/Components/MovementController.cs`
**Changes**:
- Add SerializeField for curves and thresholds
- Default curve values

**New Fields**:
```csharp
[Header("Momentum Settings")]
[SerializeField] private AnimationCurve accelerationCurve = CreateDefaultAccelerationCurve();
[SerializeField] private AnimationCurve decelerationCurve = CreateDefaultDecelerationCurve();
[SerializeField] private float accelerationDuration = 0.2f;
[SerializeField] private float decelerationDuration = 0.15f;

[Header("Pivot Settings")]
[SerializeField] private float pivotAngleThreshold = 60f;
[SerializeField] private float maxPivotSpeedReduction = 0.4f;

[Header("Turn In Place")]
[SerializeField] private float turnInPlaceThreshold = 45f;
[SerializeField] private float turnInPlaceSpeedThreshold = 0.5f;
```

**Quality Gate**: Inspector shows new fields with defaults

---

## Phase 2: Core Momentum System (Medium Risk)

### Task 2.1: Create Helper Methods
**File**: `Assets/_Scripts/Player/Components/MovementController.cs`
**Changes**:
- Add EvaluateAccelerationCurve()
- Add EvaluateDecelerationCurve()
- Add CalculatePivotFactor()
- Add CalculateTurnAngle()

**Quality Gate**: Methods compile, unit testable

### Task 2.2: Implement ApplyMomentumMovement()
**File**: `Assets/_Scripts/Player/Components/MovementController.cs`
**Changes**:
- New private method for unlocked movement
- Replaces direct SmoothDamp with curve-based acceleration
- Integrates pivot factor

**Flow**:
```csharp
private void ApplyMomentumMovement(Vector2 input, Vector3 targetDirection, LocomotionMode mode)
{
    // 1. Calculate target speed from mode
    // 2. Update acceleration/deceleration timer
    // 3. Evaluate curve for current speed factor
    // 4. Calculate pivot factor from turn angle
    // 5. Apply final velocity
    // 6. Move character
}
```

**Quality Gate**: Character moves with visible acceleration

### Task 2.3: Modify ApplyMovement() to Branch
**File**: `Assets/_Scripts/Player/Components/MovementController.cs`
**Changes**:
- Add isLockedOn check at beginning
- Branch to new or existing logic

**Quality Gate**: Lock-on unchanged, unlocked uses new system

---

## Phase 3: Rotation System (Medium Risk)

### Task 3.1: Implement ApplyRotationWithMomentum()
**File**: `Assets/_Scripts/Player/Components/MovementController.cs`
**Changes**:
- Rotation speed varies with turn angle
- Faster rotation when more misaligned
- Smooth rotation using SmoothDampAngle

**Quality Gate**: Character rotates smoothly toward movement direction

### Task 3.2: Integrate Rotation with Movement
**File**: `Assets/_Scripts/Player/Components/MovementController.cs`
**Changes**:
- Call rotation method from ApplyMomentumMovement
- Update TurnAngle in smoothing state

**Quality Gate**: Rotation feels natural during movement

---

## Phase 4: Animation Integration (Medium Risk)

### Task 4.1: Add Animator Parameter Methods
**File**: `Assets/_Scripts/Player/Components/AnimationController.cs`
**Changes**:
- Add SetTurnAngle(float)
- Add SetVelocityMagnitude(float)
- Add static hash caches

**Quality Gate**: Methods callable, no errors

### Task 4.2: Update MovementController Animation Calls
**File**: `Assets/_Scripts/Player/Components/MovementController.cs`
**Changes**:
- Call SetTurnAngle() with current turn angle
- Call SetVelocityMagnitude() with current velocity

**Quality Gate**: Animator receives correct values (debug in Inspector)

### Task 4.3: Update Animator Controller
**File**: `Assets/PlayerAnimatorController.controller`
**Changes**:
- Add TurnAngle parameter (Float)
- Add VelocityMagnitude parameter (Float)
- (Turn states added later)

**Quality Gate**: Parameters visible in Animator window

---

## Phase 5: Turn-In-Place System (Higher Risk)

### Task 5.1: Implement ShouldTurnInPlace()
**File**: `Assets/_Scripts/Player/Components/MovementController.cs`
**Changes**:
- Check if stationary (velocity < threshold)
- Check if turn angle > threshold
- Return boolean

**Quality Gate**: Correctly detects turn-in-place scenarios

### Task 5.2: Implement HandleTurnInPlace()
**File**: `Assets/_Scripts/Player/Components/MovementController.cs`
**Changes**:
- Set IsTurningInPlace flag
- Update animator for turn state
- Rotation during turn
- Exit condition when turn complete

**Quality Gate**: Turn-in-place triggers and completes

### Task 5.3: Add Root Motion Handling
**File**: `Assets/_Scripts/Player/Components/AnimationController.cs`
**Changes**:
- Add OnAnimatorMove() handling
- Apply root rotation when turning
- Ignore root position

**Quality Gate**: Root motion rotation applied during turns

### Task 5.4: Integrate with ApplyMovement()
**File**: `Assets/_Scripts/Player/Components/MovementController.cs`
**Changes**:
- Check ShouldTurnInPlace before movement
- Branch to HandleTurnInPlace if needed

**Quality Gate**: Turn-in-place triggers from idle

---

## Phase 6: Animation Assets & Polish (Low Risk)

### Task 6.1: Import Mixamo Animations
**Manual Task**:
- Download from Mixamo: Idle, Walk, Run, Sprint, Turn90L, Turn90R
- Import into Unity
- Configure as Humanoid
- Enable root motion on turn animations

**Quality Gate**: Animations play in Preview

### Task 6.2: Setup Animator Blend Tree
**File**: `Assets/PlayerAnimatorController.controller`
**Changes**:
- Verify Locomotion blend tree uses Speed correctly
- Add Turn sub-state machine with Turn90L/R states
- Setup transitions based on TurnAngle

**Quality Gate**: Blend tree responds to parameter changes

### Task 6.3: Fine-tune Parameters
**Manual Task**:
- Adjust acceleration/deceleration curves
- Tune pivot thresholds
- Tune turn-in-place thresholds
- Test feel at 80/20 balance

**Quality Gate**: Movement feels right

---

## Dependency Graph

```
Phase 1 (Data) ──┬──► Phase 2 (Momentum) ──► Phase 3 (Rotation)
                 │                                    │
                 │                                    ▼
                 └──────────────► Phase 4 (Animation) ◄──┘
                                        │
                                        ▼
                                 Phase 5 (Turn-In-Place)
                                        │
                                        ▼
                                 Phase 6 (Polish)
```

---

## Risk Mitigation Checkpoints

| After Phase | Checkpoint | Rollback Plan |
|-------------|------------|---------------|
| Phase 2 | Movement works | Revert to SmoothDamp |
| Phase 3 | Rotation smooth | Revert rotation changes |
| Phase 4 | Animator updated | Animation always recoverable |
| Phase 5 | Turn-in-place works | Disable turn-in-place flag |

---

## Quality Gates Summary

- [ ] Phase 1: Data layer compiles, Inspector shows fields
- [ ] Phase 2: Character has visible acceleration/deceleration
- [ ] Phase 3: Rotation feels smooth and weighted
- [ ] Phase 4: Animator receives correct parameter values
- [ ] Phase 5: Turn-in-place triggers and completes correctly
- [ ] Phase 6: Final feel matches 80/20 responsive/realistic

---

## Notes

- Each task should be committed separately
- Test after each phase before proceeding
- Lock-on must work unchanged at all times
- Combat transitions must remain functional
