# Architecture: KCC Character System

## Overview

Architettura del character movement system basato su Kinematic Character Controller (KCC), con integrazione del momentum system esistente e supporto combat forces.

---

## Component Diagram

```
┌─────────────────────────────────────────────────────────────────┐
│                        UNITY UPDATE                              │
│  ┌──────────────┐                                               │
│  │ InputHandler │ → PlayerState.Execute() → MovementController  │
│  └──────────────┘                              .ApplyMovement() │
│                                                      │          │
│                                    ┌─────────────────▼────────┐ │
│                                    │    MovementIntent        │ │
│                                    │    (cached struct)       │ │
│                                    └─────────────────┬────────┘ │
└──────────────────────────────────────────────────────┼──────────┘
                                                       │
┌──────────────────────────────────────────────────────▼──────────┐
│                     UNITY FIXEDUPDATE (50Hz)                     │
│                                                                  │
│  ┌────────────────────────────────────────────────────────────┐ │
│  │              KinematicCharacterMotor                        │ │
│  │                                                             │ │
│  │  1. BeforeCharacterUpdate()  ─────┐                        │ │
│  │  2. UpdateVelocity(ref vel)  ─────┤                        │ │
│  │  3. UpdateRotation(ref rot)  ─────┼──► MovementController  │ │
│  │  4. [KCC Physics Simulation]      │    (ICharacterController)│
│  │  5. PostGroundingUpdate()    ─────┤                        │ │
│  │  6. AfterCharacterUpdate()   ─────┘                        │ │
│  └────────────────────────────────────────────────────────────┘ │
└──────────────────────────────────────────────────────────────────┘

┌──────────────────────────────────────────────────────────────────┐
│                     SUPPORTING SYSTEMS                           │
│                                                                  │
│  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐  │
│  │ ExternalForces  │  │ AnimationController│ │ LockOnController│ │
│  │ Manager         │  │ (unchanged)       │  │ (unchanged)     │ │
│  └────────┬────────┘  └────────┬──────────┘  └────────┬────────┘  │
│           │                    │                      │          │
│           └────────────────────┼──────────────────────┘          │
│                                ▼                                 │
│                      MovementController                          │
└──────────────────────────────────────────────────────────────────┘
```

---

## Data Flow

### Movement Intent Pipeline

```
┌─────────────────────────────────────────────────────────────────┐
│                        UPDATE FRAME                              │
└─────────────────────────────────────────────────────────────────┘
                              │
    InputHandler.GetMoveInput()
                              │
                              ▼
    PlayerState.Execute(context)
                              │
         ┌────────────────────┴────────────────────┐
         │                                         │
    PlayerWalkState              PlayerSprintState, etc.
         │                                         │
         └────────────────────┬────────────────────┘
                              │
    MovementController.ApplyMovement(input, mode)
                              │
                              ▼
    ┌─────────────────────────────────────────────┐
    │             MovementIntent                  │
    │  ┌─────────────────────────────────────┐   │
    │  │ RawInput: Vector2                   │   │
    │  │ WorldDirection: Vector3 (calculated)│   │
    │  │ Mode: LocomotionMode                │   │
    │  │ Timestamp: float                    │   │
    │  │ IsValid: bool                       │   │
    │  └─────────────────────────────────────┘   │
    └─────────────────────────────────────────────┘
                              │
                         [cached]
                              │
┌─────────────────────────────▼───────────────────────────────────┐
│                      FIXEDUPDATE FRAME                          │
└─────────────────────────────────────────────────────────────────┘
                              │
    BeforeCharacterUpdate()
        • Validate intent (check timestamp)
        • Update external forces
        • Check turn-in-place trigger
                              │
                              ▼
    UpdateVelocity(ref currentVelocity)
        │
        ├── IsLockedOn? ──► CalculateLockedOnVelocity()
        │                       • Orbital movement
        │                       • Distance maintenance
        │
        └── Normal ──► CalculateMomentumVelocity()
                          • Curve-based acceleration
                          • Pivot speed reduction
                              │
                              ▼
        Add External Forces ◄── ExternalForcesManager.GetCombined()
                              │
                              ▼
        Apply Gravity (if airborne)
                              │
                              ▼
    UpdateRotation(ref currentRotation)
        │
        ├── IsTurnInPlace? ──► return; (animation handles)
        │
        └── Normal ──► CalculateRotation()
                          • Smooth rotation
                          • Face movement direction
                              │
                              ▼
    [KCC Physics Simulation]
        • Collision sweeps
        • Ground detection
        • Step handling
                              │
                              ▼
    PostGroundingUpdate()
        • Update animator parameters
        • Trigger landing events
                              │
                              ▼
    AfterCharacterUpdate()
        • Invalidate intent
        • Cleanup expired forces
```

---

## Core Structures

### MovementIntent

```csharp
/// <summary>
/// Cached movement input, bridges Update→FixedUpdate timing gap.
/// Struct: 48 bytes, zero GC.
/// </summary>
private struct MovementIntent
{
    public Vector2 RawInput;           // Original input
    public Vector3 WorldDirection;     // Camera-relative, pre-calculated
    public LocomotionMode Mode;        // Walk/Run/Sprint
    public float Timestamp;            // Time.time when cached
    public bool IsValid;               // Explicitly set

    public bool IsStale => IsValid && (Time.time - Timestamp > 0.1f);

    public void Invalidate()
    {
        IsValid = false;
        RawInput = Vector2.zero;
        WorldDirection = Vector3.zero;
    }
}
```

### ForceInstance

```csharp
/// <summary>
/// Single external force with decay and priority.
/// </summary>
public struct ForceInstance
{
    public Vector3 Force;
    public ForceMode Mode;      // Instant, Impulse, Continuous
    public float Duration;      // Remaining duration (-1 = infinite)
    public float DecayRate;     // Force units per second to reduce
    public int Priority;        // Higher priority can clear lower
    public float StartTime;     // When applied

    public bool IsExpired => Duration > 0 && (Time.time - StartTime > Duration);
}

public enum ForceMode
{
    Instant,     // Apply full force this frame only
    Impulse,     // Decay over duration
    Continuous   // Sustained until removed or duration ends
}
```

### SmoothingState (Unchanged)

```csharp
/// <summary>
/// Movement smoothing state tracker.
/// Already implemented in MovementController - NO CHANGES NEEDED.
/// </summary>
private struct SmoothingState
{
    // ... existing implementation preserved
}
```

---

## Interface Implementation

### ICharacterController Mapping

```csharp
public class MovementController : MonoBehaviour, ICharacterController
{
    // ═══════════════════════════════════════════════════════════════
    // CALLBACKS WITH LOGIC
    // ═══════════════════════════════════════════════════════════════

    public void BeforeCharacterUpdate(float deltaTime)
    {
        // 1. Validate cached intent (check stale)
        // 2. Update external forces (decay, remove expired)
        // 3. Check turn-in-place conditions
    }

    public void UpdateVelocity(ref Vector3 currentVelocity, float deltaTime)
    {
        // 1. Calculate base velocity (momentum or locked-on)
        // 2. Add external forces
        // 3. Apply gravity if airborne
        // Set velocity components - KCC integrates position
    }

    public void UpdateRotation(ref Quaternion currentRotation, float deltaTime)
    {
        // 1. Skip if turn-in-place (animation controls)
        // 2. Calculate target rotation
        // 3. Apply smooth rotation
    }

    public void PostGroundingUpdate(float deltaTime)
    {
        // 1. Update IsGrounded state for animations
        // 2. Update animator parameters (Speed, TurnAngle)
        // 3. Trigger landing events if just landed
    }

    public void AfterCharacterUpdate(float deltaTime)
    {
        // 1. Invalidate movement intent
        // 2. Cleanup expired forces
        // 3. Reset per-frame state
    }

    // ═══════════════════════════════════════════════════════════════
    // STUBS (FUTURE EXPANSION)
    // ═══════════════════════════════════════════════════════════════

    public bool IsColliderValidForCollisions(Collider coll)
    {
        // Future: layer filtering, trigger exclusion
        return true;
    }

    public void OnGroundHit(Collider hitCollider, Vector3 hitNormal,
        Vector3 hitPoint, ref HitStabilityReport hitStabilityReport)
    {
        // Future: footstep sounds, ground material detection
    }

    public void OnMovementHit(Collider hitCollider, Vector3 hitNormal,
        Vector3 hitPoint, ref HitStabilityReport hitStabilityReport)
    {
        // Future: wall slide, obstacle detection
    }

    public void ProcessHitStabilityReport(Collider hitCollider,
        Vector3 hitNormal, Vector3 hitPoint, Vector3 atCharacterPosition,
        Quaternion atCharacterRotation, ref HitStabilityReport hitStabilityReport)
    {
        // Future: custom slope stability
    }

    public void OnDiscreteCollisionDetected(Collider hitCollider)
    {
        // Future: trigger interactions, damage zones
    }
}
```

---

## External Forces System

### Manager Design

```csharp
public class ExternalForcesManager
{
    private List<ForceInstance> _activeForces = new(8); // Pre-allocated

    /// <summary>
    /// Add a force with specified behavior.
    /// </summary>
    public void AddForce(Vector3 force, ForceMode mode,
        float duration = 0.3f, float decayRate = 0f, int priority = 0)
    {
        // Higher priority clears lower priority forces
        if (priority > GetMaxActivePriority())
            ClearForcesBelowPriority(priority);

        _activeForces.Add(new ForceInstance
        {
            Force = force,
            Mode = mode,
            Duration = duration,
            DecayRate = decayRate,
            Priority = priority,
            StartTime = Time.time
        });
    }

    /// <summary>
    /// Calculate combined force for this frame.
    /// </summary>
    public Vector3 GetCombinedForce(float deltaTime)
    {
        Vector3 combined = Vector3.zero;

        for (int i = _activeForces.Count - 1; i >= 0; i--)
        {
            var force = _activeForces[i];

            if (force.IsExpired)
            {
                _activeForces.RemoveAt(i);
                continue;
            }

            switch (force.Mode)
            {
                case ForceMode.Instant:
                    combined += force.Force;
                    _activeForces.RemoveAt(i); // One frame only
                    break;

                case ForceMode.Impulse:
                    combined += force.Force;
                    force.Force -= force.Force.normalized * force.DecayRate * deltaTime;
                    _activeForces[i] = force;
                    break;

                case ForceMode.Continuous:
                    combined += force.Force;
                    break;
            }
        }

        return combined;
    }

    public void Clear() => _activeForces.Clear();
}
```

### Combat Integration Examples

```csharp
// Light attack knockback
_forcesManager.AddForce(
    force: knockbackDir * 8f,
    mode: ForceMode.Impulse,
    duration: 0.25f,
    decayRate: 32f,
    priority: 5
);

// Heavy stagger (high priority, clears other forces)
_forcesManager.Clear();
_forcesManager.AddForce(
    force: staggerDir * 12f,
    mode: ForceMode.Impulse,
    duration: 0.5f,
    decayRate: 24f,
    priority: 10
);

// Environmental wind zone
_forcesManager.AddForce(
    force: windDirection * 3f,
    mode: ForceMode.Continuous,
    duration: -1f, // Infinite until removed
    priority: 1
);
```

---

## Turn-In-Place Coordination

### Protocol

```
┌─────────────────┐                    ┌──────────────────────┐
│MovementController│                    │ AnimationController  │
└────────┬────────┘                    └──────────┬───────────┘
         │                                        │
         │  ShouldTurnInPlace() == true           │
         │ ───────────────────────────────────────►
         │                                        │
         │         IsTurnInPlaceActive = true     │
         │ ◄───────────────────────────────────────
         │                                        │
         │  UpdateRotation() → return (skip)      │
         │                                        │
         │         [Animation plays with          │
         │          root motion rotation]         │
         │                                        │
         │  OnTurnInPlaceComplete() event         │
         │ ◄───────────────────────────────────────
         │                                        │
         │  Resume normal rotation                │
         │                                        │
```

### Implementation

```csharp
// In MovementController.UpdateRotation()
public void UpdateRotation(ref Quaternion currentRotation, float deltaTime)
{
    // Check if animation is handling rotation
    if (_animationController?.IsTurnInPlaceActive ?? false)
    {
        // Don't modify rotation - animation's root motion handles it
        return;
    }

    // Normal rotation logic
    if (!_intent.IsValid || _intent.WorldDirection.sqrMagnitude < 0.01f)
        return;

    Quaternion targetRotation = Quaternion.LookRotation(_intent.WorldDirection);
    currentRotation = Quaternion.Slerp(
        currentRotation,
        targetRotation,
        _rotationSpeed * deltaTime
    );
}
```

---

## Ground State Wrapper

```csharp
// Clean API, wraps KCC internals
public bool IsGrounded => _motor?.GroundingStatus.IsStableOnGround ?? false;
public bool IsOnSlope => _motor?.GroundingStatus.FoundAnyGround ?? false
    && Vector3.Angle(Vector3.up, GroundNormal) > 5f;
public Vector3 GroundNormal => _motor?.GroundingStatus.GroundNormal ?? Vector3.up;
public float GroundAngle => Vector3.Angle(Vector3.up, GroundNormal);

// Combat system can check these directly
// Example: if (!movementController.IsGrounded) { ApplyAerialKnockback(); }
```

---

## Migration Checklist

### Phase 1: Core KCC
- [ ] Add `KinematicCharacterMotor` component to player prefab
- [ ] Implement `ICharacterController` interface (stubs)
- [ ] Replace `CharacterController` reference with `KinematicCharacterMotor`
- [ ] Wire up `_motor.CharacterController = this` in Awake

### Phase 2: Movement Intent
- [ ] Add `MovementIntent` struct
- [ ] Change `ApplyMovement()` to cache intent
- [ ] Implement `UpdateVelocity()` with intent consumption

### Phase 3: Velocity Calculation
- [ ] Port momentum calculation to `UpdateVelocity()`
- [ ] Port locked-on movement to `UpdateVelocity()`
- [ ] Add gravity handling

### Phase 4: Rotation
- [ ] Port rotation logic to `UpdateRotation()`
- [ ] Add turn-in-place skip logic

### Phase 5: External Forces
- [ ] Implement `ExternalForcesManager`
- [ ] Integrate with `UpdateVelocity()`
- [ ] Connect to combat system

### Phase 6: Animator Integration
- [ ] Move parameter updates to `PostGroundingUpdate()`
- [ ] Verify blend tree behavior

### Phase 7: Cleanup
- [ ] Remove old `CharacterController` code
- [ ] Remove `Move()` calls
- [ ] Test all player states
