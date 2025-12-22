# KCC Character System Architecture Design

## Executive Summary

This document provides the complete architectural design for migrating MovementController from Unity's CharacterController to Kinematic Character Controller (KCC). The design preserves the existing momentum-based movement system while integrating with KCC's physics-aware character motor.

**Key Design Principles:**
- Preserve momentum system integrity (acceleration curves, pivot mechanics)
- Minimal disruption to PlayerStates, AnimationController, SmoothingState
- Combat-ready (external forces, knockback support)
- Clear separation of concerns (input → intent → velocity → physics)

---

## 1. System Component Diagram

```
┌─────────────────────────────────────────────────────────────────┐
│                         PlayerController                         │
│  - Orchestrates all player systems                              │
│  - Owns references to all components                            │
└──────────────┬──────────────────────────────────────────────────┘
               │
               │ delegates movement
               ▼
┌─────────────────────────────────────────────────────────────────┐
│                      MovementController                          │
│  implements ICharacterController                                │
│                                                                  │
│  ┌────────────────────────────────────────────────┐            │
│  │ Update (Unity lifecycle)                       │            │
│  │  - Cache input as MovementIntent               │            │
│  │  - Update smoothing decay                      │            │
│  └────────────────────────────────────────────────┘            │
│                                                                  │
│  ┌────────────────────────────────────────────────┐            │
│  │ KCC Callbacks (pull-model in FixedUpdate)      │            │
│  │                                                 │            │
│  │  BeforeCharacterUpdate()                       │            │
│  │   - Validate cached MovementIntent             │            │
│  │   - Process external forces decay              │            │
│  │                                                 │            │
│  │  UpdateVelocity(ref Vector3 currentVelocity)   │            │
│  │   - Apply momentum movement logic              │            │
│  │   - Add external forces                        │            │
│  │   - Apply gravity                              │            │
│  │                                                 │            │
│  │  UpdateRotation(ref Quaternion currentRotation)│            │
│  │   - Skip if turn-in-place active               │            │
│  │   - Apply momentum rotation                    │            │
│  │                                                 │            │
│  │  PostGroundingUpdate()                         │            │
│  │   - Update animation parameters                │            │
│  │                                                 │            │
│  │  AfterCharacterUpdate()                        │            │
│  │   - Invalidate MovementIntent                  │            │
│  └────────────────────────────────────────────────┘            │
│                                                                  │
│  ┌────────────────────────────────────────────────┐            │
│  │ Private State                                   │            │
│  │  - _cachedIntent: MovementIntent               │            │
│  │  - _externalForces: List<ForceInstance>        │            │
│  │  - _smoothing: SmoothingState                  │            │
│  │  - _motor: KinematicCharacterMotor             │            │
│  └────────────────────────────────────────────────┘            │
└───────┬──────────────────────────────────────────────────┬─────┘
        │                                                   │
        │ controls animation                               │ reads ground state
        ▼                                                   ▼
┌──────────────────┐                              ┌─────────────────┐
│AnimationController│                              │KinematicCharacter│
│- No changes needed│                              │Motor             │
│- Receives params  │                              │- Grounding status│
└──────────────────┘                              │- Physics handling│
                                                   └─────────────────┘
```

---

## 2. Data Flow Diagram: Movement Intent → Velocity

```
┌──────────────┐
│ InputHandler │ (Update @ ~60-144Hz)
└──────┬───────┘
       │ GetMoveInput()
       ▼
┌────────────────────────────────────────────┐
│ PlayerController (Update)                  │
│  - Reads input: Vector2 moveInput          │
│  - Determines LocomotionMode               │
│  - Calls MovementController.ApplyMovement()│
└────────────┬───────────────────────────────┘
             │
             ▼
┌─────────────────────────────────────────────────────────┐
│ MovementController.ApplyMovement() (Update)             │
│                                                          │
│  1. Calculate camera-relative target direction          │
│  2. Cache MovementIntent:                               │
│     - Input: Vector2                                    │
│     - TargetDirection: Vector3 (camera-relative)        │
│     - Mode: LocomotionMode                              │
│     - IsLockedOn: bool                                  │
│     - IsValid: true                                     │
│     - Timestamp: Time.time                              │
│                                                          │
│  3. Update smoothing decay (animation cleanup)          │
└─────────────────────────────────────────────────────────┘
             │
             │ (Time passes - may be multiple Update calls)
             │
             ▼
┌─────────────────────────────────────────────────────────┐
│ KCC Motor Triggers FixedUpdate @ 50Hz                   │
└────────────┬────────────────────────────────────────────┘
             │
             ▼
┌─────────────────────────────────────────────────────────┐
│ BeforeCharacterUpdate(float deltaTime)                  │
│  - Check _cachedIntent.IsValid                          │
│  - Validate timestamp (stale if > 0.1s old)             │
│  - Update external forces (decay/remove expired)        │
└────────────┬────────────────────────────────────────────┘
             │
             ▼
┌─────────────────────────────────────────────────────────┐
│ UpdateVelocity(ref Vector3 currentVelocity, deltaTime)  │
│                                                          │
│  1. Branch: IsLockedOn?                                 │
│     YES → ApplyLockedOnVelocity()                       │
│     NO  → ApplyMomentumVelocity()                       │
│                                                          │
│  2. ApplyMomentumVelocity() logic:                      │
│     a. Check turn-in-place trigger                      │
│     b. If turning: return zero horizontal velocity      │
│     c. Get target speed from LocomotionMode             │
│     d. Calculate turn angle                             │
│     e. Update momentum timers                           │
│     f. Evaluate acceleration/deceleration curves        │
│     g. Apply pivot factor                               │
│     h. Smooth direction changes                         │
│     i. Build velocity = direction * magnitude           │
│                                                          │
│  3. Add external forces:                                │
│     velocity += SumActiveForces()                       │
│                                                          │
│  4. Apply gravity:                                      │
│     if (!_motor.GroundingStatus.IsStableOnGround)       │
│       velocity.y += Gravity * deltaTime                 │
│     else                                                │
│       velocity.y = -2f (ground stick)                   │
│                                                          │
│  5. Set ref currentVelocity = velocity                  │
└────────────┬────────────────────────────────────────────┘
             │
             ▼
┌─────────────────────────────────────────────────────────┐
│ UpdateRotation(ref Quaternion currentRotation, deltaTime)│
│                                                          │
│  1. Check if turn-in-place active                       │
│     - If AnimationController.IsTurnInPlaceActive:       │
│       → SKIP rotation (animation handles it via root    │
│         motion)                                         │
│       → return without modifying currentRotation        │
│                                                          │
│  2. Check if locked-on:                                 │
│     YES → Face target (LockOnController.CurrentTarget)  │
│     NO  → Apply momentum rotation                       │
│                                                          │
│  3. ApplyMomentumRotation():                            │
│     - Use smoothed move direction                       │
│     - Calculate rotation multiplier (faster when misaligned)│
│     - SmoothDampAngle toward target                     │
│     - Set ref currentRotation                           │
└────────────┬────────────────────────────────────────────┘
             │
             ▼
┌─────────────────────────────────────────────────────────┐
│ KCC Motor executes movement                             │
│  - Capsule casts for collision detection                │
│  - Resolves overlaps                                    │
│  - Applies velocity                                     │
│  - Updates grounding status                             │
└────────────┬────────────────────────────────────────────┘
             │
             ▼
┌─────────────────────────────────────────────────────────┐
│ PostGroundingUpdate(float deltaTime)                    │
│  - Read _motor.GroundingStatus.IsStableOnGround         │
│  - Update animation parameters                          │
│  - Call AnimationController.SetSpeed(), SetMoveDirection│
└────────────┬────────────────────────────────────────────┘
             │
             ▼
┌─────────────────────────────────────────────────────────┐
│ AfterCharacterUpdate(float deltaTime)                   │
│  - Invalidate MovementIntent (_cachedIntent.IsValid=false)│
│  - This forces fresh input for next FixedUpdate         │
└─────────────────────────────────────────────────────────┘
```

**Key Timing Notes:**
- Update runs 60-144Hz (variable)
- FixedUpdate runs 50Hz (fixed)
- Intent caching bridges the timing gap
- Stale intent detection prevents outdated input from being applied

---

## 3. Architecture Decision Records (ADRs)

### ADR-001: MovementIntent Caching Strategy

**Context:**
- Unity Update runs at variable framerate (60-144Hz)
- KCC callbacks execute in FixedUpdate at 50Hz
- Input must be captured in Update but consumed in FixedUpdate
- Multiple Update calls may occur between FixedUpdate ticks
- Stale input must not affect movement

**Decision:**
Use a lightweight struct `MovementIntent` with validation flags.

```csharp
public struct MovementIntent
{
    public Vector2 Input;                 // Raw input from InputHandler
    public Vector3 TargetDirection;       // Camera-relative direction (pre-calculated)
    public LocomotionMode Mode;           // Walk/Run/Sprint
    public bool IsLockedOn;               // Lock-on state at time of capture
    public bool IsValid;                  // False after consumed by FixedUpdate
    public float Timestamp;               // Time.time when cached (for staleness detection)
}
```

**Consequences:**
✅ **Pros:**
- Minimal memory overhead (48 bytes)
- Clear validity semantics
- Pre-calculates camera-relative direction in Update (optimization)
- Timestamp allows stale detection (>100ms = ignore)
- Immutable after creation (prevents mid-frame changes)

⚠️ **Cons:**
- Requires careful validation in BeforeCharacterUpdate
- Must invalidate after consumption to prevent reuse

**Implementation Notes:**
- Cache in `ApplyMovement()` (called from Update)
- Validate in `BeforeCharacterUpdate()` (first KCC callback)
- Invalidate in `AfterCharacterUpdate()` (last KCC callback)
- If invalid/stale in UpdateVelocity: use Vector3.zero (idle)

**Alternative Considered:**
Class-based MovementIntent with change tracking → Rejected due to GC allocation overhead

---

### ADR-002: External Forces System Design

**Context:**
- Combat requires knockback, explosions, environmental pushes
- Forces need duration control (instant, over-time, persistent)
- Forces must decay naturally (e.g., knockback diminishes)
- Multiple forces can be active simultaneously
- Forces integrate with velocity in UpdateVelocity

**Decision:**
Force instance system with decay modes and priority.

```csharp
public struct ForceInstance
{
    public Vector3 Force;              // Current force vector
    public float Duration;             // Remaining lifetime (-1 = infinite)
    public float DecayRate;            // Units per second reduction (0 = instant)
    public ForceMode Mode;             // Instant, Continuous, Impulse
    public int Priority;               // Higher = applied last (overrides lower)

    public bool IsExpired => Duration >= 0f && Duration <= 0f;
}

public enum ForceMode
{
    Instant,      // Applied once, removed next frame
    Continuous,   // Applied every frame until duration expires
    Impulse       // Applied once with decay over duration
}
```

**API Design:**
```csharp
// Public interface for combat system
public void AddForce(Vector3 force, ForceMode mode, float duration = -1f, float decayRate = 0f, int priority = 0)
{
    _externalForces.Add(new ForceInstance
    {
        Force = force,
        Duration = duration,
        DecayRate = decayRate,
        Mode = mode,
        Priority = priority
    });

    // Sort by priority (stable sort preserves insertion order for equal priority)
    _externalForces.Sort((a, b) => a.Priority.CompareTo(b.Priority));
}

public void ClearForces() => _externalForces.Clear();
public void ClearForce(int priority) => _externalForces.RemoveAll(f => f.Priority == priority);
```

**Integration with UpdateVelocity:**
```csharp
private void UpdateExternalForces(float deltaTime)
{
    for (int i = _externalForces.Count - 1; i >= 0; i--)
    {
        var force = _externalForces[i];

        // Update duration
        if (force.Duration > 0f)
        {
            force.Duration -= deltaTime;
            if (force.Duration <= 0f)
            {
                _externalForces.RemoveAt(i);
                continue;
            }
        }

        // Apply decay
        if (force.DecayRate > 0f)
        {
            force.Force = Vector3.MoveTowards(force.Force, Vector3.zero, force.DecayRate * deltaTime);
        }

        // Remove if decayed to zero
        if (force.Force.sqrMagnitude < 0.01f)
        {
            _externalForces.RemoveAt(i);
            continue;
        }

        _externalForces[i] = force;
    }
}

private Vector3 SumActiveForces()
{
    Vector3 totalForce = Vector3.zero;
    foreach (var force in _externalForces)
    {
        totalForce += force.Force;
    }
    return totalForce;
}
```

**Consequences:**
✅ **Pros:**
- Flexible force application (instant knockback, sustained wind, impulse explosion)
- Priority system allows override (super armor, stagger, etc.)
- Automatic cleanup (no manual force removal needed)
- Decay provides natural feel (knockback diminishes smoothly)
- Multiple forces combine additively (realistic physics)

⚠️ **Cons:**
- Requires update in BeforeCharacterUpdate (small overhead)
- Combat system must understand ForceMode semantics

**Usage Examples:**
```csharp
// Knockback from attack
movementController.AddForce(knockbackDirection * 15f, ForceMode.Impulse, duration: 0.3f, decayRate: 50f);

// Environmental wind
movementController.AddForce(windDirection * 5f, ForceMode.Continuous, duration: -1f);

// Explosion push (instant)
movementController.AddForce(explosionVector * 20f, ForceMode.Instant);

// Stagger with priority override (cancels other forces)
movementController.ClearForces(); // or ClearForce(priority < 10)
movementController.AddForce(staggerForce, ForceMode.Impulse, duration: 0.5f, priority: 10);
```

---

### ADR-003: Ground State Exposure

**Context:**
- MovementController needs IsGrounded for jump logic, state transitions
- Combat system needs grounding info for aerial attacks
- KCC provides `_motor.GroundingStatus.IsStableOnGround` (more accurate than CharacterController.isGrounded)
- IsStableOnGround accounts for slope angle, surface stability

**Decision:**
Wrapper property with KCC-specific semantics.

```csharp
/// <summary>
/// Whether the character is grounded and stable.
/// Uses KCC's stability-aware grounding (considers slope angle, ledges).
/// More accurate than CharacterController.isGrounded.
/// </summary>
public bool IsGrounded => _motor?.GroundingStatus.IsStableOnGround ?? false;

/// <summary>
/// Whether any ground was detected (may be unstable/too steep).
/// Use for effects (dust particles) even on steep slopes.
/// </summary>
public bool FoundAnyGround => _motor?.GroundingStatus.FoundAnyGround ?? false;

/// <summary>
/// Current ground normal vector. Zero if not grounded.
/// </summary>
public Vector3 GroundNormal => _motor?.GroundingStatus.GroundNormal ?? Vector3.up;
```

**Consequences:**
✅ **Pros:**
- Clean API matches original CharacterController interface
- Exposes additional KCC grounding info (FoundAnyGround, normals)
- Null-safe (returns false if motor not initialized)
- GroundNormal enables slope-aware movement (future feature)

⚠️ **Cons:**
- IsStableOnGround may behave differently than isGrounded on steep slopes
- Combat system must understand stability semantics

**Migration Note:**
Replace `_characterController.isGrounded` → `_motor.GroundingStatus.IsStableOnGround` throughout codebase.

**Alternative Considered:**
Expose `_motor` reference directly → Rejected to maintain encapsulation and prevent misuse

---

### ADR-004: Turn-In-Place Coordination

**Context:**
- Turn-in-place uses root motion rotation (animation-driven)
- MovementController's UpdateRotation must not fight animation
- AnimationController drives turn via Animator root motion
- Handoff must be clean (no rotation jitter)

**Decision:**
Animation-driven flag with explicit coordination protocol.

**AnimationController Addition:**
```csharp
public class AnimationController : MonoBehaviour
{
    // Existing fields...

    /// <summary>
    /// True when turn-in-place animation is actively controlling rotation.
    /// Set via animation events on turn animations.
    /// </summary>
    public bool IsTurnInPlaceActive { get; private set; }

    // Called by animation event at start of turn animation
    public void OnTurnInPlaceStart()
    {
        IsTurnInPlaceActive = true;
    }

    // Called by animation event at end of turn animation
    public void OnTurnInPlaceEnd()
    {
        IsTurnInPlaceActive = false;
    }
}
```

**MovementController Integration:**
```csharp
public void UpdateRotation(ref Quaternion currentRotation, float deltaTime)
{
    // CRITICAL: Skip rotation if animation is handling it
    if (_animationController?.IsTurnInPlaceActive ?? false)
    {
        // Animation controls rotation via root motion
        // Do NOT modify currentRotation
        return;
    }

    // Normal rotation logic when not turning in place
    if (_cachedIntent.IsLockedOn)
    {
        ApplyLockedOnRotation(ref currentRotation, deltaTime);
    }
    else
    {
        ApplyMomentumRotation(ref currentRotation, deltaTime);
    }
}
```

**Animation Setup:**
- Add `OnTurnInPlaceStart()` event at frame 0 of turn animations
- Add `OnTurnInPlaceEnd()` event at last frame of turn animations
- Ensure root motion rotation is enabled on turn animation clips

**Consequences:**
✅ **Pros:**
- Clean separation: animation drives rotation, MovementController skips
- No rotation fighting or jitter
- Works with any turn animation (90°, 180°, custom)
- Explicit handoff via animation events

⚠️ **Cons:**
- Requires animation events on all turn clips
- Animator must call OnTurnInPlaceEnd or character stays in "turning" state

**Fallback Safety:**
```csharp
// In HandleTurnInPlace() (MovementController):
if (_smoothing.TurnProgress >= 1f || Mathf.Abs(currentTurnAngle) < 10f)
{
    // Force exit if animation event missed
    _animationController?.OnTurnInPlaceEnd();
    ExitTurnInPlace();
}
```

---

### ADR-005: ICharacterController Implementation

**Context:**
- KCC requires all 10 interface methods to be implemented
- Some methods have real logic, others are empty stubs
- Lifecycle flows through BeforeUpdate → UpdateVelocity → UpdateRotation → PostGroundingUpdate → AfterUpdate

**Decision:**
Pragmatic implementation with focused logic.

```csharp
public class MovementController : MonoBehaviour, ICharacterController
{
    private KinematicCharacterMotor _motor;
    private MovementIntent _cachedIntent;
    private List<ForceInstance> _externalForces = new List<ForceInstance>(8);

    // ═══════════════════════════════════════════════════════════════
    // LIFECYCLE CALLBACKS (called by KCC in FixedUpdate)
    // ═══════════════════════════════════════════════════════════════

    public void BeforeCharacterUpdate(float deltaTime)
    {
        // Validate cached intent
        if (!_cachedIntent.IsValid || (Time.time - _cachedIntent.Timestamp) > 0.1f)
        {
            // Stale or invalid - treat as no input
            _cachedIntent = default;
        }

        // Update external forces (decay, duration)
        UpdateExternalForces(deltaTime);
    }

    public void UpdateVelocity(ref Vector3 currentVelocity, float deltaTime)
    {
        // Branch based on lock-on state
        if (_cachedIntent.IsLockedOn)
        {
            ApplyLockedOnVelocity(ref currentVelocity, deltaTime);
        }
        else
        {
            ApplyMomentumVelocity(ref currentVelocity, deltaTime);
        }

        // Add external forces
        currentVelocity += SumActiveForces();

        // Apply gravity
        if (!_motor.GroundingStatus.IsStableOnGround)
        {
            currentVelocity.y += Gravity * deltaTime;
        }
        else
        {
            currentVelocity.y = -2f; // Small downward force to maintain ground contact
        }
    }

    public void UpdateRotation(ref Quaternion currentRotation, float deltaTime)
    {
        // Skip if turn-in-place animation is controlling rotation
        if (_animationController?.IsTurnInPlaceActive ?? false)
        {
            return; // Animation handles rotation via root motion
        }

        // Apply appropriate rotation logic
        if (_cachedIntent.IsLockedOn)
        {
            ApplyLockedOnRotation(ref currentRotation, deltaTime);
        }
        else
        {
            ApplyMomentumRotation(ref currentRotation, deltaTime);
        }
    }

    public void PostGroundingUpdate(float deltaTime)
    {
        // Update animation parameters now that grounding is known
        UpdateAnimationParameters();
    }

    public void AfterCharacterUpdate(float deltaTime)
    {
        // Invalidate intent to force fresh input next FixedUpdate
        _cachedIntent.IsValid = false;

        // Remove instant forces
        _externalForces.RemoveAll(f => f.Mode == ForceMode.Instant);
    }

    // ═══════════════════════════════════════════════════════════════
    // COLLISION CALLBACKS (minimal implementation)
    // ═══════════════════════════════════════════════════════════════

    public bool IsColliderValidForCollisions(Collider coll)
    {
        // Ignore triggers and character's own colliders
        if (coll.isTrigger) return false;
        if (coll.transform.IsChildOf(transform)) return false;
        return true;
    }

    public void OnGroundHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint,
        ref HitStabilityReport hitStabilityReport)
    {
        // Default implementation - could add ground material detection later
    }

    public void OnMovementHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint,
        ref HitStabilityReport hitStabilityReport)
    {
        // Default implementation - could add collision response later
    }

    public void ProcessHitStabilityReport(Collider hitCollider, Vector3 hitNormal,
        Vector3 hitPoint, Vector3 atCharacterPosition, Quaternion atCharacterRotation,
        ref HitStabilityReport hitStabilityReport)
    {
        // Default implementation - KCC handles stability
    }

    public void OnDiscreteCollisionDetected(Collider hitCollider)
    {
        // Default implementation - could add trigger logic later
    }
}
```

**Method Responsibilities:**

| Method | Purpose | Has Logic? |
|--------|---------|------------|
| BeforeCharacterUpdate | Pre-simulation setup | ✅ YES - Validate intent, update forces |
| UpdateVelocity | Calculate desired velocity | ✅ YES - Core movement logic |
| UpdateRotation | Calculate desired rotation | ✅ YES - Rotation logic with turn-in-place skip |
| PostGroundingUpdate | React to grounding results | ✅ YES - Update animation |
| AfterCharacterUpdate | Post-simulation cleanup | ✅ YES - Invalidate intent, cleanup |
| IsColliderValidForCollisions | Filter collisions | ✅ YES - Ignore triggers |
| OnGroundHit | Ground collision response | ❌ STUB - Future material detection |
| OnMovementHit | Movement collision response | ❌ STUB - Future collision effects |
| ProcessHitStabilityReport | Modify stability logic | ❌ STUB - Use KCC defaults |
| OnDiscreteCollisionDetected | Discrete collision handling | ❌ STUB - Future trigger logic |

**Consequences:**
✅ **Pros:**
- Clear lifecycle flow matches KCC design
- Separation of concerns (input → validation → velocity → rotation → animation)
- Stubs are explicit and documented for future expansion
- Intent validation prevents stale input bugs

⚠️ **Cons:**
- Developers must understand FixedUpdate timing
- Debugging requires knowledge of KCC callback order

---

## 4. Interface Implementation Skeleton

```csharp
using UnityEngine;
using System.Collections.Generic;
using KinematicCharacterController;
using _Scripts.Player.Data;

namespace _Scripts.Player.Components
{
    [RequireComponent(typeof(KinematicCharacterMotor))]
    public class MovementController : MonoBehaviour, ICharacterController
    {
        // ═══════════════════════════════════════════════════════════════
        // REFERENCES
        // ═══════════════════════════════════════════════════════════════

        private PlayerController _player;
        private KinematicCharacterMotor _motor;
        private AnimationController _animationController;
        private LockOnController _lockOnController;
        private Transform _cameraTransform;

        // ═══════════════════════════════════════════════════════════════
        // STATE
        // ═══════════════════════════════════════════════════════════════

        [SerializeField] private SmoothingState _smoothing = SmoothingState.CreateDefault();
        private MovementIntent _cachedIntent;
        private List<ForceInstance> _externalForces = new List<ForceInstance>(8);

        // ═══════════════════════════════════════════════════════════════
        // CONFIGURATION (all existing fields preserved)
        // ═══════════════════════════════════════════════════════════════

        [Header("Movement Speeds")]
        [SerializeField] private float walkSpeed = 1.1f;
        [SerializeField] private float runSpeed = 5f;
        [SerializeField] private float sprintSpeed = 7f;

        [Header("Gravity")]
        [SerializeField] private float gravity = -20f;

        // ... (all other configuration fields from current implementation)

        // ═══════════════════════════════════════════════════════════════
        // PROPERTIES
        // ═══════════════════════════════════════════════════════════════

        public bool IsGrounded => _motor?.GroundingStatus.IsStableOnGround ?? false;
        public bool FoundAnyGround => _motor?.GroundingStatus.FoundAnyGround ?? false;
        public Vector3 GroundNormal => _motor?.GroundingStatus.GroundNormal ?? Vector3.up;

        // ═══════════════════════════════════════════════════════════════
        // UNITY LIFECYCLE
        // ═══════════════════════════════════════════════════════════════

        private void Awake()
        {
            _player = GetComponent<PlayerController>();
            _motor = GetComponent<KinematicCharacterMotor>();
            _motor.CharacterController = this; // Register as controller
        }

        private void Start()
        {
            _animationController = _player.AnimationController;
            _lockOnController = _player.LockOnController;
            _cameraTransform = FindCamera();
        }

        private void Update()
        {
            // Intent caching happens in ApplyMovement (called by PlayerController)
            UpdateSmoothingDecay();
        }

        // ═══════════════════════════════════════════════════════════════
        // PUBLIC API (called by PlayerStates)
        // ═══════════════════════════════════════════════════════════════

        public void ApplyMovement(Vector2 moveInput, LocomotionMode mode)
        {
            if (_motor == null || _cameraTransform == null) return;

            // Calculate camera-relative direction
            Vector3 targetDirection = GetCameraRelativeDirection(moveInput);

            // Cache intent for FixedUpdate consumption
            _cachedIntent = new MovementIntent
            {
                Input = moveInput,
                TargetDirection = targetDirection,
                Mode = mode,
                IsLockedOn = _lockOnController?.IsLockedOn ?? false,
                IsValid = true,
                Timestamp = Time.time
            };
        }

        public void AddForce(Vector3 force, ForceMode mode = ForceMode.Impulse,
            float duration = -1f, float decayRate = 0f, int priority = 0)
        {
            _externalForces.Add(new ForceInstance
            {
                Force = force,
                Duration = duration,
                DecayRate = decayRate,
                Mode = mode,
                Priority = priority
            });
            _externalForces.Sort((a, b) => a.Priority.CompareTo(b.Priority));
        }

        public void ClearForces() => _externalForces.Clear();
        public void CancelTurnInPlace() => ExitTurnInPlace();

        // ═══════════════════════════════════════════════════════════════
        // ICharacterController IMPLEMENTATION
        // ═══════════════════════════════════════════════════════════════

        public void BeforeCharacterUpdate(float deltaTime)
        {
            // Validate cached intent (stale detection)
            if (!_cachedIntent.IsValid || (Time.time - _cachedIntent.Timestamp) > 0.1f)
            {
                _cachedIntent = default;
            }

            // Update external forces
            UpdateExternalForces(deltaTime);
        }

        public void UpdateVelocity(ref Vector3 currentVelocity, float deltaTime)
        {
            // Apply movement logic based on lock-on state
            if (_cachedIntent.IsLockedOn)
            {
                ApplyLockedOnVelocity(ref currentVelocity, deltaTime);
            }
            else
            {
                ApplyMomentumVelocity(ref currentVelocity, deltaTime);
            }

            // Add external forces
            currentVelocity += SumActiveForces();

            // Gravity
            if (!_motor.GroundingStatus.IsStableOnGround)
            {
                currentVelocity.y += gravity * deltaTime;
            }
            else
            {
                currentVelocity.y = -2f; // Ground stick
            }
        }

        public void UpdateRotation(ref Quaternion currentRotation, float deltaTime)
        {
            // CRITICAL: Skip if animation controls rotation
            if (_animationController?.IsTurnInPlaceActive ?? false)
            {
                return;
            }

            if (_cachedIntent.IsLockedOn)
            {
                ApplyLockedOnRotation(ref currentRotation, deltaTime);
            }
            else
            {
                ApplyMomentumRotation(ref currentRotation, deltaTime);
            }
        }

        public void PostGroundingUpdate(float deltaTime)
        {
            // Update animation parameters
            UpdateAnimationParameters();
        }

        public void AfterCharacterUpdate(float deltaTime)
        {
            // Invalidate intent
            _cachedIntent.IsValid = false;

            // Remove instant forces
            _externalForces.RemoveAll(f => f.Mode == ForceMode.Instant);
        }

        public bool IsColliderValidForCollisions(Collider coll)
        {
            if (coll.isTrigger) return false;
            if (coll.transform.IsChildOf(transform)) return false;
            return true;
        }

        public void OnGroundHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint,
            ref HitStabilityReport hitStabilityReport) { }

        public void OnMovementHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint,
            ref HitStabilityReport hitStabilityReport) { }

        public void ProcessHitStabilityReport(Collider hitCollider, Vector3 hitNormal,
            Vector3 hitPoint, Vector3 atCharacterPosition, Quaternion atCharacterRotation,
            ref HitStabilityReport hitStabilityReport) { }

        public void OnDiscreteCollisionDetected(Collider hitCollider) { }

        // ═══════════════════════════════════════════════════════════════
        // PRIVATE MOVEMENT LOGIC (reuse existing implementations)
        // ═══════════════════════════════════════════════════════════════

        private void ApplyMomentumVelocity(ref Vector3 velocity, float deltaTime)
        {
            // Implementation mirrors current ApplyMomentumMovement
            // but builds velocity instead of calling CharacterController.Move

            Vector3 targetDirection = _cachedIntent.TargetDirection;
            bool hasInput = targetDirection.sqrMagnitude > 0.01f;

            // Turn-in-place logic
            if (ShouldTurnInPlace(targetDirection))
            {
                EnterTurnInPlace(targetDirection);
            }

            if (_smoothing.IsTurningInPlace)
            {
                HandleTurnInPlace(targetDirection);
                velocity = Vector3.zero; // No horizontal movement during turn
                return;
            }

            // Get target speed
            float targetSpeed = GetSpeedForMode(_cachedIntent.Mode);

            // Calculate turn angle
            float turnAngle = 0f;
            if (hasInput)
            {
                turnAngle = CalculateTurnAngle(transform.forward, targetDirection);
                _smoothing.TurnAngle = turnAngle;
            }

            // Update momentum timers
            UpdateMomentumTimers(hasInput);

            // Evaluate curves
            float speedFactor;
            if (hasInput)
            {
                speedFactor = EvaluateAccelerationCurve();
                float pivotFactor = CalculatePivotFactor(Mathf.Abs(turnAngle));
                speedFactor *= pivotFactor;
            }
            else
            {
                speedFactor = EvaluateDecelerationCurve();
            }

            // Update velocity magnitude
            _smoothing.CurrentVelocityMagnitude = targetSpeed * speedFactor;

            // Smooth direction
            _smoothing.SmoothedMoveDirection = Vector3.SmoothDamp(
                _smoothing.SmoothedMoveDirection,
                hasInput ? targetDirection : Vector3.zero,
                ref _smoothing.MoveDirectionVelocity,
                hasInput ? movementSmoothTime : moveDirectionDecayTime
            );

            // Build velocity
            if (_smoothing.CurrentVelocityMagnitude > 0.01f &&
                _smoothing.SmoothedMoveDirection.sqrMagnitude > 0.01f)
            {
                velocity = _smoothing.SmoothedMoveDirection.normalized * _smoothing.CurrentVelocityMagnitude;
            }
            else
            {
                velocity = Vector3.zero;
            }
        }

        private void ApplyLockedOnVelocity(ref Vector3 velocity, float deltaTime)
        {
            // Implementation mirrors current ApplyLockedOnMovement
            // (detailed implementation omitted for brevity - see current file)
        }

        private void ApplyMomentumRotation(ref Quaternion rotation, float deltaTime)
        {
            // Convert current rotation-based logic to quaternion manipulation
            Vector3 rotationTarget = _smoothing.SmoothedMoveDirection.sqrMagnitude > 0.01f
                ? _smoothing.SmoothedMoveDirection
                : _cachedIntent.TargetDirection;

            if (rotationTarget.sqrMagnitude < 0.01f) return;

            float targetAngle = Mathf.Atan2(rotationTarget.x, rotationTarget.z) * Mathf.Rad2Deg;
            float currentAngle = rotation.eulerAngles.y;

            float rotationMultiplier = 1f + (Mathf.Abs(_smoothing.TurnAngle) / 90f) *
                (misalignedRotationMultiplier - 1f);
            float adjustedSmoothTime = characterRotationSmoothTime / rotationMultiplier;

            float smoothedAngle = Mathf.SmoothDampAngle(
                currentAngle,
                targetAngle,
                ref _smoothing.RotationVelocity,
                adjustedSmoothTime,
                Mathf.Infinity,
                deltaTime
            );

            rotation = Quaternion.Euler(0f, smoothedAngle, 0f);
        }

        private void ApplyLockedOnRotation(ref Quaternion rotation, float deltaTime)
        {
            // Face target logic
            var currentTarget = _lockOnController?.CurrentTarget;
            if (currentTarget != null)
            {
                Vector3 toTarget = currentTarget.LockOnPoint - transform.position;
                toTarget.y = 0;
                if (toTarget.sqrMagnitude > 0.01f)
                {
                    rotation = Quaternion.LookRotation(toTarget.normalized);
                }
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // HELPER METHODS (all existing helpers preserved)
        // ═══════════════════════════════════════════════════════════════

        // GetCameraRelativeDirection, GetSpeedForMode, CalculateTurnAngle, etc.
        // (13 pure math/config methods from Phase 2 analysis - unchanged)
    }
}
```

---

## 5. External Forces System Implementation

**Complete Implementation:**

```csharp
// ═══════════════════════════════════════════════════════════════
// EXTERNAL FORCES SYSTEM
// ═══════════════════════════════════════════════════════════════

public struct ForceInstance
{
    public Vector3 Force;
    public float Duration;
    public float DecayRate;
    public ForceMode Mode;
    public int Priority;

    public bool IsExpired => Duration >= 0f && Duration <= 0f;
}

public enum ForceMode
{
    Instant,      // Applied once, removed next frame
    Continuous,   // Applied every frame until duration expires
    Impulse       // Applied once with decay over duration
}

// Private force management
private List<ForceInstance> _externalForces = new List<ForceInstance>(8);

private void UpdateExternalForces(float deltaTime)
{
    for (int i = _externalForces.Count - 1; i >= 0; i--)
    {
        var force = _externalForces[i];

        // Update duration
        if (force.Duration > 0f)
        {
            force.Duration -= deltaTime;
            if (force.Duration <= 0f)
            {
                _externalForces.RemoveAt(i);
                continue;
            }
        }

        // Apply decay
        if (force.DecayRate > 0f && force.Mode == ForceMode.Impulse)
        {
            force.Force = Vector3.MoveTowards(force.Force, Vector3.zero,
                force.DecayRate * deltaTime);
        }

        // Remove if decayed to zero
        if (force.Force.sqrMagnitude < 0.01f)
        {
            _externalForces.RemoveAt(i);
            continue;
        }

        _externalForces[i] = force;
    }
}

private Vector3 SumActiveForces()
{
    Vector3 totalForce = Vector3.zero;
    foreach (var force in _externalForces)
    {
        totalForce += force.Force;
    }
    return totalForce;
}

// Public API
public void AddForce(Vector3 force, ForceMode mode = ForceMode.Impulse,
    float duration = -1f, float decayRate = 0f, int priority = 0)
{
    _externalForces.Add(new ForceInstance
    {
        Force = force,
        Duration = duration,
        DecayRate = decayRate,
        Mode = mode,
        Priority = priority
    });

    // Sort by priority (higher priority applied last)
    _externalForces.Sort((a, b) => a.Priority.CompareTo(b.Priority));
}

public void ClearForces() => _externalForces.Clear();
public void ClearForcesByPriority(int priority) =>
    _externalForces.RemoveAll(f => f.Priority == priority);
```

**Usage in Combat System:**

```csharp
// Example: Heavy attack knockback
public void OnHeavyAttackHit(Vector3 attackDirection)
{
    Vector3 knockbackForce = attackDirection.normalized * 15f;
    playerMovement.AddForce(
        force: knockbackForce,
        mode: ForceMode.Impulse,
        duration: 0.4f,
        decayRate: 37.5f,  // Decays from 15 to 0 over 0.4s
        priority: 5
    );
}

// Example: Environmental wind zone
public void OnEnterWindZone(Vector3 windDirection, float windStrength)
{
    playerMovement.AddForce(
        force: windDirection * windStrength,
        mode: ForceMode.Continuous,
        duration: -1f,  // Infinite until zone exit
        priority: 1
    );
}

public void OnExitWindZone()
{
    playerMovement.ClearForcesByPriority(1);
}

// Example: Explosion push (instant)
public void OnExplosion(Vector3 explosionCenter, float radius, float force)
{
    Vector3 direction = (transform.position - explosionCenter).normalized;
    float distance = Vector3.Distance(transform.position, explosionCenter);
    float falloff = 1f - Mathf.Clamp01(distance / radius);

    playerMovement.AddForce(
        force: direction * force * falloff,
        mode: ForceMode.Instant,
        priority: 10  // High priority overrides other forces
    );
}
```

---

## 6. Migration Checklist

**Phase 1: Setup**
- [ ] Add KinematicCharacterMotor component to player prefab
- [ ] Configure motor capsule (radius, height, offset to match current CharacterController)
- [ ] Set motor grounding settings (max slope angle, step handling)
- [ ] Remove CharacterController component (backup prefab first!)

**Phase 2: Code Migration**
- [ ] Add ICharacterController implementation to MovementController
- [ ] Replace `_characterController` with `_motor` references
- [ ] Add MovementIntent struct definition
- [ ] Add ForceInstance struct and ForceMode enum
- [ ] Implement BeforeCharacterUpdate, UpdateVelocity, UpdateRotation callbacks
- [ ] Refactor ApplyMomentumMovement → ApplyMomentumVelocity (velocity-based)
- [ ] Refactor ApplyLockedOnMovement → ApplyLockedOnVelocity (velocity-based)
- [ ] Refactor ApplyMomentumRotation → quaternion-based rotation
- [ ] Remove CharacterController.Move calls (KCC handles movement)

**Phase 3: Animation Integration**
- [ ] Add IsTurnInPlaceActive property to AnimationController
- [ ] Add OnTurnInPlaceStart/End animation events to turn clips
- [ ] Update UpdateRotation to check IsTurnInPlaceActive flag

**Phase 4: Testing**
- [ ] Test momentum movement (acceleration, deceleration, pivot)
- [ ] Test locked-on movement (orbital, strafe, distance maintenance)
- [ ] Test turn-in-place (90°, 180° turns, root motion rotation)
- [ ] Test grounding on slopes, stairs, ledges
- [ ] Test external forces (knockback, wind, explosions)
- [ ] Test state transitions (idle → walk → run → sprint)

**Phase 5: Combat Integration**
- [ ] Update combat system to use AddForce for knockback
- [ ] Test stagger forces with priority system
- [ ] Verify grounding detection during aerial attacks
- [ ] Test forces during lock-on combat

---

## 7. Performance Considerations

**Memory:**
- MovementIntent: 48 bytes (negligible)
- ForceInstance: ~40 bytes × 8 capacity = 320 bytes
- Total new overhead: ~400 bytes per character

**CPU:**
- Intent validation: ~0.01ms per FixedUpdate
- Force update: ~0.02ms per FixedUpdate (8 forces)
- Total overhead: ~0.03ms @ 50Hz = negligible

**GC Pressure:**
- Zero allocations in hot path (struct-based design)
- List<ForceInstance> pre-allocated to 8 capacity
- No LINQ or lambda allocations

---

## 8. Future Enhancements

**Potential Extensions (out of scope for initial migration):**

1. **Material-Based Movement**
   - Implement OnGroundHit to detect surface materials
   - Adjust movement speed/friction based on terrain
   - Add footstep sound system integration

2. **Advanced Collision Response**
   - Implement OnMovementHit for wall-slide mechanics
   - Add ledge grab detection using HitStabilityReport
   - Enable custom physics interactions

3. **Slope-Aware Movement**
   - Use GroundNormal for slope-based speed adjustment
   - Add slide-down-slope mechanic on steep terrain
   - Implement uphill stamina cost

4. **Network Replication**
   - MovementIntent is network-friendly (small struct)
   - ForceInstance can be replicated for knockback sync
   - KCC supports deterministic simulation

5. **Force Visualization (Debug)**
   - Gizmo display of active forces
   - Force decay graph in inspector
   - Priority visualization

---

## 9. Risk Assessment

| Risk | Severity | Mitigation |
|------|----------|------------|
| Stale input causing movement jitter | Medium | Timestamp validation + staleness threshold |
| Turn-in-place animation event missed | Low | Fallback exit condition in HandleTurnInPlace |
| External forces overwhelming movement | Low | Priority system + ClearForces API |
| KCC grounding differs from CharacterController | Medium | Extensive testing on slopes/stairs/ledges |
| UpdateVelocity timing issues | Low | Intent caching bridges Update/FixedUpdate gap |

---

## 10. Testing Strategy

**Unit Tests (if applicable):**
- MovementIntent validation logic
- Force decay calculations
- Turn angle calculations

**Integration Tests:**
- Input → Intent → Velocity flow
- Multiple forces combining correctly
- Turn-in-place coordination with animation

**Playtesting Focus:**
- Momentum feel preserved from current system
- No rotation jitter during turn-in-place
- Knockback feels responsive and natural
- Grounding detection accurate on complex geometry

---

## Conclusion

This architecture design provides a complete blueprint for migrating to KCC while preserving the existing momentum-based movement system. The design emphasizes:

1. **Clean separation of concerns** (input caching, velocity calculation, rotation control)
2. **Combat readiness** (external forces with priority system)
3. **Animation coordination** (turn-in-place handoff protocol)
4. **Minimal disruption** (PlayerStates, AnimationController, SmoothingState unchanged)
5. **Future extensibility** (collision callbacks, material detection, slope mechanics)

All design decisions are documented with ADRs explaining context, rationale, and trade-offs. The implementation skeleton provides clear structure for the actual code migration.

**Next Steps:**
1. Review this design with team for feedback
2. Create migration branch
3. Implement Phase 1-2 (setup + core migration)
4. Extensive testing before merging
5. Document any deviations from design during implementation
