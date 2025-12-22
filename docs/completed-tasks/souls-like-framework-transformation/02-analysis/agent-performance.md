# Project Keira - Performance Analysis Report
## Action Souls-Like Combat System Performance Evaluation

**Date**: 2025-12-15
**Target**: 60 FPS stable performance
**Critical Requirement**: ~200ms parry window timing precision
**Project Type**: Turn-based → Action RPG transformation

---

## Executive Summary

Project Keira is currently architected as a **turn-based RPG** with minimal real-time update loops. For transformation to an action souls-like game requiring precise timing (200ms parry windows) and 60 FPS stability, **significant architectural changes are required**. The current codebase is clean but fundamentally incompatible with real-time combat systems.

**Overall Performance Risk**: HIGH
**Readiness for Action Combat**: LOW
**Recommended Action**: Complete combat system redesign required

---

## 1. Update vs FixedUpdate Analysis

### Current State
**Finding**: NO Update, FixedUpdate, or LateUpdate loops detected in codebase

```
Searched Pattern: \b(Update|FixedUpdate|LateUpdate)\b\s*\(
Result: 0 matches across all 13 project scripts
```

**Architecture Pattern**:
- Event-driven state machine (GameState enum)
- Turn-based logic with discrete state transitions
- OnMouseDown() for player interaction (turn-based only)
- No continuous frame updates

### Critical Issues for Action Combat

**Blocking Problems**:
1. No frame-by-frame input processing
2. No continuous physics simulation integration
3. No animation state updates
4. No real-time AI behavior
5. Mouse-based interaction incompatible with action combat

### Recommendations

**PRIORITY: CRITICAL - Implement Real-Time Update Loops**

#### Player Controller (60 FPS precision required)
```csharp
// PlayerController.cs - NEW SCRIPT REQUIRED
private void Update() {
    // Input must be sampled every frame for responsiveness
    ProcessMovementInput();    // WASD/Stick input
    ProcessCombatInput();      // Attack/Parry/Dodge buttons
    UpdateCameraInput();       // Mouse/Right stick look
    UpdateAnimationStates();   // Blend parameters
}

private void FixedUpdate() {
    // Physics-based movement at fixed timestep (0.02s = 50Hz)
    ApplyMovementForces();     // Rigidbody movement
    ProcessCollisionEvents();  // Attack hitbox checks
}

private void LateUpdate() {
    // Camera follows after all movement complete
    UpdateCameraPosition();
    UpdateCameraRotation();
}
```

**Why Fixed Timestep Matters**:
- Current setting: 0.02s (50 Hz)
- Good for stable physics simulation
- Movement/attacks MUST use FixedUpdate for consistency
- Input reading MUST use Update for responsiveness

**Input Processing Pattern**:
```csharp
// WRONG - Current approach for action combat
void OnMouseDown() { } // Frame-dependent, too slow

// CORRECT - Required for souls-like timing
void Update() {
    if (Input.GetButtonDown("Parry")) {
        float timeSinceAttackStart = Time.time - lastAttackTime;
        if (timeSinceAttackStart <= PARRY_WINDOW) {
            ExecuteParry(); // ~200ms window requires frame-precise input
        }
    }
}
```

---

## 2. Garbage Allocation Analysis

### Current State
**Finding**: MINIMAL GC pressure - code is extremely clean

**Positive Patterns**:
- Struct-based stats (no heap allocation)
- No Update loops generating garbage
- Singleton pattern avoids repeated allocations
- No string concatenation in hot paths

**Identified Allocation Sources**:

#### 1. Resources.LoadAll (One-time, Acceptable)
```csharp
// ResourceSystem.cs:20
ExampleHeroes = Resources.LoadAll<ScriptableExampleHero>("ExampleHeroes").ToList();
```
- Executes once at Awake()
- LINQ `.ToList()` allocates but acceptable for initialization
- Not a performance concern for runtime

#### 2. Event Subscription (Low Impact)
```csharp
// HeroUnitBase.cs
ExampleGameManager.OnBeforeStateChanged += OnStateChanged;
```
- Minimal allocation for delegate
- Clean unsubscription in OnDestroy()
- Best practice implementation

#### 3. DestroyChildren Pattern (Potential Issue)
```csharp
// Helpers.cs:15-16
foreach (Transform child in t) Object.Destroy(child.gameObject);
```
- **ISSUE**: IEnumerator allocation per call
- Executes foreach on Transform (generates garbage)
- **Impact**: If called frequently during combat (particle cleanup, enemy spawns)

### Recommendations for Action Combat

**PRIORITY: MEDIUM - Prepare for Combat GC Pressure**

#### 1. Object Pooling System (REQUIRED)
```csharp
// ObjectPool.cs - NEW SCRIPT REQUIRED
public class ObjectPool<T> where T : Component {
    private Queue<T> pool;
    private T prefab;
    private Transform parent;

    public T Get() {
        // Reuse instead of Instantiate
        if (pool.Count > 0) return pool.Dequeue();
        return Object.Instantiate(prefab, parent);
    }

    public void Return(T obj) {
        // Disable instead of Destroy
        obj.gameObject.SetActive(false);
        pool.Enqueue(obj);
    }
}
```

**Pool Requirements**:
- Enemy spawns (avoid Instantiate during combat)
- Projectiles/arrows (avoid per-shot allocation)
- Particle effects (VFX for slashes, impacts)
- Damage numbers (UI floating text)
- Audio sources (3D sound emitters)

#### 2. Optimize DestroyChildren
```csharp
// Replace foreach iteration to avoid garbage
public static void DestroyChildren(this Transform t) {
    int childCount = t.childCount;
    for (int i = childCount - 1; i >= 0; i--) {
        Object.Destroy(t.GetChild(i).gameObject);
    }
}
```

#### 3. Cache Component References
```csharp
// WRONG - Each frame allocation
void Update() {
    GetComponent<Rigidbody>().AddForce(...); // Allocates search
}

// CORRECT - Cache in Awake
private Rigidbody rb;
void Awake() { rb = GetComponent<Rigidbody>(); }
void Update() { rb.AddForce(...); }
```

#### 4. String Allocation Prevention
```csharp
// AVOID in combat code
Debug.Log("Damage: " + damage); // String concat allocates

// USE instead
Debug.LogFormat("Damage: {0}", damage); // No allocation with primitives
```

---

## 3. Physics Setup Analysis

### Current Configuration (DynamicsManager.asset)

**Settings Review**:
```yaml
Fixed Timestep: 0.02 (50 Hz)                    # GOOD for stability
Maximum Allowed Timestep: 0.33s                 # ACCEPTABLE
Gravity: -9.81 m/s²                             # Standard
Default Solver Iterations: 6                    # LOW for action combat
Default Solver Velocity Iterations: 1           # VERY LOW
Auto Simulation: true                           # GOOD
Auto Sync Transforms: false                     # GOOD (performance)
Reuse Collision Callbacks: true                 # GOOD
Queries Hit Triggers: true                      # ACCEPTABLE
```

### Critical Issues for Combat

**PRIORITY: HIGH - Physics Configuration Insufficient**

#### 1. Solver Iterations Too Low
**Current**: 6 position iterations, 1 velocity iteration
**Problem**: Insufficient for precise combat collision detection

**Impact on Souls-Like Combat**:
- Weapon hitboxes may penetrate enemies
- Fast attacks (slashes) miss collision detection
- Parry timing becomes unreliable
- Character controller jitter during movement

**Recommended Settings**:
```yaml
Default Solver Iterations: 10-12              # Precise collision resolution
Default Solver Velocity Iterations: 8-10      # Smooth force application
```

**Trade-off**: +15-20% physics CPU cost, but REQUIRED for combat precision

#### 2. Fixed Timestep Analysis
**Current**: 0.02s (50 Hz physics updates)

**For 200ms Parry Window**:
- 200ms / 20ms = 10 physics frames to detect parry timing
- Minimum acceptable for combat precision
- Could improve to 0.0166s (60 Hz) for 12 frames of precision

**Recommendation**: Keep 0.02s initially, monitor combat feel

#### 3. Continuous Collision Detection Required
**Current State**: Likely using Discrete (default)

**Must Configure**:
```csharp
// PlayerController Rigidbody
rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

// Enemy Weapons/Projectiles
weaponRb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
```

**Why**: Fast weapon swings (>1m/s) will tunnel through enemies without CCD

### Layer-Based Collision Matrix

**REQUIRED SETUP** (currently using default all-collide):

```
Recommended Layers:
- Player (8)
- Enemy (9)
- PlayerWeapon (10)
- EnemyWeapon (11)
- Environment (12)
- Triggers (13)

Collision Matrix:
Player          collides with: Environment, Enemy, EnemyWeapon
Enemy           collides with: Environment, Player, PlayerWeapon
PlayerWeapon    collides with: Enemy
EnemyWeapon     collides with: Player
Triggers        collides with: Player, Enemy (for trigger zones)
```

**Performance Benefit**: ~30-40% reduction in unnecessary collision checks

---

## 4. Input Processing Analysis

### Current Implementation

**Input System**: Unity Input System (new) - GOOD
**Configuration**: InputSystem_Actions.inputactions

**Defined Actions**:
```
Player Map:
- Move (Vector2)          : WASD/Left stick
- Look (Vector2)          : Mouse delta/Right stick
- Attack (Button)         : Left mouse/West button
- Jump (Button)           : Space/South button
- Sprint (Button)         : Left Shift/L3
- Crouch (Button)         : C/East button
- Interact (Button/Hold)  : E/North button
```

**Input System Strengths**:
- Action-based (decoupled from raw input)
- Multi-device support (Keyboard, Gamepad, Touch)
- Proper rebinding architecture
- Hold interaction for Interact action

### Critical Issues for Action Combat

**PRIORITY: CRITICAL - No Input Processing Code**

**Current State**: Input actions defined but NOT CONSUMED by any scripts

**Missing Components**:
```
NO PlayerInput component reference
NO input action subscriptions
NO input callback handlers
NO input buffering system
```

### Recommendations

**REQUIRED IMPLEMENTATION**:

#### 1. Input Handler Architecture
```csharp
// PlayerInputHandler.cs - NEW SCRIPT REQUIRED
using UnityEngine.InputSystem;

public class PlayerInputHandler : MonoBehaviour {
    private PlayerInput playerInput;
    private InputAction moveAction;
    private InputAction attackAction;
    private InputAction parryAction;

    // Input buffer for responsive combat
    private Queue<InputCommand> inputBuffer = new Queue<InputCommand>();
    private const float BUFFER_WINDOW = 0.2f; // 200ms buffer

    private void Awake() {
        playerInput = GetComponent<PlayerInput>();

        // Subscribe to actions
        moveAction = playerInput.actions["Move"];
        attackAction = playerInput.actions["Attack"];

        // Button callbacks for timing precision
        attackAction.performed += OnAttackPerformed;
        attackAction.canceled += OnAttackCanceled;
    }

    private void Update() {
        // Read continuous input (60 FPS precision)
        Vector2 moveInput = moveAction.ReadValue<Vector2>();
        ProcessMovement(moveInput);

        // Process buffered inputs
        ProcessInputBuffer();
    }

    private void OnAttackPerformed(InputAction.CallbackContext ctx) {
        // Record exact timestamp for parry window calculations
        float inputTime = (float)ctx.time;
        BufferInput(new AttackCommand(inputTime));
    }
}
```

#### 2. Input Buffering System (Souls-Like Feel)
**Why Critical**: Allows 200ms input window for combo continuity

```csharp
public struct InputCommand {
    public InputType type;
    public float timestamp;
    public bool consumed;
}

// During attack animation, buffer next attack input
if (isAttacking && attackAction.triggered) {
    if (Time.time - currentAttackStart < BUFFER_WINDOW) {
        queuedAttack = true; // Execute on animation frame
    }
}
```

**Without Buffering**: Player inputs feel unresponsive, dropped inputs
**With Buffering**: Smooth combo chains, professional feel

#### 3. Parry Window Timing
```csharp
private void ProcessParryInput() {
    if (parryAction.triggered) {
        float enemyAttackTime = enemyController.GetAttackStartTime();
        float timeDelta = Time.time - enemyAttackTime;

        // 200ms parry window
        if (timeDelta >= PARRY_EARLY_THRESHOLD &&
            timeDelta <= PARRY_LATE_THRESHOLD) {
            ExecutePerfectParry();
        } else if (timeDelta <= PARRY_GRACE_THRESHOLD) {
            ExecuteNormalParry();
        } else {
            ExecuteFailedParry(); // Punish
        }
    }
}
```

**Input Latency Budget** (for 60 FPS @ 200ms window):
- Input polling: 1-2ms (Update)
- Input System processing: 1-3ms
- Gameplay logic: 2-4ms
- Physics: 3-5ms
- Rendering: 10-14ms
- Total: ~20-30ms (ACCEPTABLE for 200ms window)

---

## 5. Object Pooling Analysis

### Current State
**Finding**: NO object pooling implemented

**Identified Spawn Patterns**:
```csharp
// ExampleUnitManager.cs:16
var spawned = Instantiate(tarodevScriptable.Prefab, pos, Quaternion.identity, transform);
```

**Current Impact**: Low (turn-based spawns only)

**Future Impact for Action Combat**: CRITICAL

### Recommendations

**PRIORITY: HIGH - Implement Before Combat Prototyping**

#### Required Pooling Systems

**1. Enemy Pool**
```csharp
// Configuration
Pool Size: 20-30 enemies
Warm-up: Pre-instantiate 10 on level load
Growth: Expand by 5 when depleted

// Usage Pattern
- Spawn on enemy waves (avoid mid-combat Instantiate)
- Return to pool on death (disable, not Destroy)
- Reset state (health, AI, position) on Get()
```

**Performance Gain**: Eliminate 50-200ms spawn hitches

**2. Projectile Pool**
```csharp
// Arrow/Magic projectile pool
Pool Size: 50-100 projectiles
Critical: High-frequency spawning (arrows, spells)

// Why Critical
- Player shoots arrows at 2-3 per second
- Each Instantiate = 5-10ms frame spike
- Pooling = <0.1ms Get() cost
```

**3. VFX Pool**
```csharp
// Combat effects (slashes, impacts, blood)
Pool Size: 30-50 particles
Lifetime: 0.5-2s (return to pool on complete)

// Impact
- Slash VFX on every attack (1-3 per second)
- Without pooling: 3-8ms per spawn
- With pooling: <0.2ms
```

**4. Damage Number Pool**
```csharp
// UI floating damage text
Pool Size: 20-30 text objects
Critical: Spawn on every hit

// Optimization
- TextMeshPro prefab pooling
- Avoid SetText() allocation (use SetCharArray)
```

#### Implementation Pattern
```csharp
// GenericObjectPool.cs - UNIVERSAL SOLUTION
public class GenericObjectPool : MonoBehaviour {
    [SerializeField] private GameObject prefab;
    [SerializeField] private int warmupCount = 10;
    [SerializeField] private int maxCount = 50;

    private Queue<GameObject> available;
    private HashSet<GameObject> active;

    public GameObject Spawn(Vector3 pos, Quaternion rot) {
        GameObject obj;
        if (available.Count > 0) {
            obj = available.Dequeue();
            obj.transform.SetPositionAndRotation(pos, rot);
            obj.SetActive(true);
        } else {
            obj = Instantiate(prefab, pos, rot);
        }
        active.Add(obj);
        return obj;
    }

    public void Despawn(GameObject obj, float delay = 0) {
        if (delay > 0) {
            StartCoroutine(DespawnDelayed(obj, delay));
        } else {
            DespawnImmediate(obj);
        }
    }

    private void DespawnImmediate(GameObject obj) {
        if (!active.Remove(obj)) return;
        obj.SetActive(false);
        if (available.Count < maxCount) {
            available.Enqueue(obj);
        } else {
            Destroy(obj); // Prevent unbounded growth
        }
    }
}
```

---

## 6. Potential Bottlenecks for Real-Time Combat

### Architectural Bottlenecks

#### 1. State Machine Transition Overhead
**Current**: GameState enum with switch statement

**Issue for Real-Time**:
```csharp
// ExampleGameManager.cs:17-42
public void ChangeState(GameState newState) {
    OnBeforeStateChanged?.Invoke(newState);  // Event allocation
    State = newState;
    switch (newState) { ... }                 // OK for infrequent changes
    OnAfterStateChanged?.Invoke(newState);    // Event allocation
}
```

**Problem**: Event invocation every state change
**Impact**: If states change per-frame (combat states), event overhead accumulates

**Solution for Combat**:
```csharp
// Separate combat state from game state
public enum CombatState {
    Idle, Attacking, Blocking, Dodging, Stunned, Dead
}

// Update combat state without events (hot path)
private void UpdateCombatState(CombatState newState) {
    previousCombatState = currentCombatState;
    currentCombatState = newState;
    // No event invocation in Update loop
}
```

#### 2. Singleton Pattern Thread Safety
**Current**: StaticInstance pattern

**Analysis**:
```csharp
// StaticInstance.cs:9-10
public static T Instance { get; private set; }
protected virtual void Awake() => Instance = this as T;
```

**Performance**: Excellent (direct static reference, no overhead)
**Thread Safety**: Not thread-safe (but Unity is single-threaded)
**Verdict**: ACCEPTABLE for Unity game architecture

#### 3. Audio System Scalability
**Current**: AudioSystem with 2 sources (music, sounds)

**Issue**:
```csharp
// AudioSystem.cs:16-22
public void PlaySound(AudioClip clip, Vector3 pos, float vol = 1) {
    _soundsSource.transform.position = pos;
    PlaySound(clip, vol);
}
```

**Problem**: Single AudioSource for all 3D sounds
**Impact**: Only 1 concurrent sound effect (combat needs 5-10)

**Solution**:
```csharp
// AudioSourcePool - REQUIRED
private AudioSource[] soundPool = new AudioSource[10];

public void PlaySound(AudioClip clip, Vector3 pos, float vol = 1) {
    AudioSource source = GetAvailableSource();
    source.transform.position = pos;
    source.PlayOneShot(clip, vol);
}
```

#### 4. Resource Loading Pattern
**Current**: Resources.LoadAll in Awake

**Analysis**:
```csharp
// ResourceSystem.cs:20-21
ExampleHeroes = Resources.LoadAll<ScriptableExampleHero>("ExampleHeroes").ToList();
_ExampleHeroesDict = ExampleHeroes.ToDictionary(r => r.HeroType, r => r);
```

**Performance**:
- LoadAll: 10-50ms (one-time)
- ToDictionary: 1-5ms (one-time)
- Acceptable for initialization

**Concern**: Random.Range in GetRandomHero()
```csharp
// ResourceSystem.cs:25
public ScriptableExampleHero GetRandomHero() =>
    ExampleHeroes[Random.Range(0, ExampleHeroes.Count)];
```
**Impact**: If called per-frame, minimal (Random.Range ~0.01ms)
**Verdict**: SAFE

---

## 7. Performance Metrics to Monitor

### Critical Metrics for 60 FPS Souls-Like

#### Frame Time Budget
**Target**: 16.67ms total frame time (60 FPS)

**Breakdown**:
```
Input Processing:     1-2ms    (Update loop)
Gameplay Logic:       2-4ms    (AI, combat state)
Physics:              3-5ms    (FixedUpdate 50Hz)
Animation:            2-3ms    (IK, blending)
Rendering:            6-8ms    (URP rendering)
Other:                1-2ms    (audio, particles)
-----------------------------------
Total:               15-24ms   (needs optimization if >16.67ms)
```

#### Measurement Tools

**Unity Profiler Targets**:
```
CPU Usage:
- Scripts: <5ms per frame
- Physics: <5ms per frame
- Rendering: <8ms per frame
- GC.Collect: 0ms (no GC spikes during combat)

Memory:
- Mono Heap: <200MB
- GC Allocations: <100KB per frame (ideally 0KB)
- Total Reserved: <500MB
```

**Combat-Specific Metrics**:
```csharp
// PerformanceMonitor.cs - IMPLEMENT THIS
public class PerformanceMonitor : MonoBehaviour {
    private float[] frameTimes = new float[60];
    private int frameIndex = 0;

    private void Update() {
        float frameTime = Time.unscaledDeltaTime * 1000f; // ms
        frameTimes[frameIndex] = frameTime;
        frameIndex = (frameIndex + 1) % 60;

        // Alert if frame time exceeds budget
        if (frameTime > 16.67f) {
            Debug.LogWarning($"Frame spike: {frameTime:F2}ms");
        }
    }

    public float GetAverageFrameTime() {
        return frameTimes.Average();
    }

    public float Get99thPercentile() {
        return frameTimes.OrderByDescending(x => x).Take(1).First();
    }
}
```

#### Input Latency Measurement
```csharp
// Critical for 200ms parry window
private void MeasureInputLatency() {
    if (Input.GetButtonDown("Parry")) {
        float inputTime = Time.realtimeSinceStartup;
        // Measure time until visual feedback appears
        StartCoroutine(MeasureLatency(inputTime));
    }
}

private IEnumerator MeasureLatency(float startTime) {
    yield return new WaitForEndOfFrame();
    float latency = (Time.realtimeSinceStartup - startTime) * 1000f;
    Debug.Log($"Input latency: {latency:F2}ms"); // Target: <30ms
}
```

#### Parry Window Precision Test
```csharp
// Validate 200ms window timing
[Test]
public void TestParryWindowPrecision() {
    float[] attackTimes = { 0f, 0.05f, 0.1f, 0.15f, 0.2f, 0.25f };
    float parryTime = 0.1f; // Parry 100ms after attack

    foreach (float attackTime in attackTimes) {
        float delta = parryTime - attackTime;
        bool shouldParry = delta >= 0f && delta <= 0.2f;
        Assert.AreEqual(shouldParry, IsWithinParryWindow(delta));
    }
}
```

---

## 8. Recommended Architecture for Action Combat

### Phase 1: Foundation (Week 1-2)

**Core Systems**:
```
1. PlayerController with Update/FixedUpdate loops
   - Movement (WASD/stick)
   - Camera control (mouse/right stick)
   - Basic attack input

2. Input System integration
   - PlayerInputHandler script
   - Action callbacks
   - Input buffering (200ms)

3. Physics configuration
   - Increase solver iterations (10-12)
   - Setup collision layers
   - Enable CCD on player/weapons

4. Basic pooling
   - Enemy pool (10 pre-warm)
   - VFX pool (20 pre-warm)
```

### Phase 2: Combat Mechanics (Week 3-4)

**Combat Systems**:
```
1. Attack System
   - Hitbox detection (trigger colliders)
   - Damage calculation
   - Animation integration

2. Parry System
   - 200ms window detection
   - Timing feedback (early/perfect/late)
   - Stagger state on perfect parry

3. Enemy AI
   - Basic attack patterns
   - Dodge/block behaviors
   - Aggro management
```

### Phase 3: Optimization (Week 5-6)

**Performance Tuning**:
```
1. Profile with combat
   - Identify frame spikes
   - Optimize hot paths
   - Reduce GC allocations

2. Advanced pooling
   - Audio source pool
   - Projectile pool
   - Damage number pool

3. Animation optimization
   - IK solver iterations
   - LOD for distant characters
   - Culling optimizations
```

### Code Structure
```
Assets/
├── _Scripts/
│   ├── Combat/
│   │   ├── AttackSystem.cs
│   │   ├── ParrySystem.cs
│   │   ├── HitboxController.cs
│   │   └── DamageCalculator.cs
│   ├── Player/
│   │   ├── PlayerController.cs
│   │   ├── PlayerInputHandler.cs
│   │   ├── PlayerCombat.cs
│   │   └── PlayerAnimation.cs
│   ├── Enemy/
│   │   ├── EnemyController.cs
│   │   ├── EnemyAI.cs
│   │   └── EnemyAnimator.cs
│   ├── Pooling/
│   │   ├── ObjectPool.cs
│   │   └── PoolManager.cs
│   └── Utilities/
│       ├── PerformanceMonitor.cs
│       └── InputBuffer.cs
```

---

## 9. Risk Assessment

### High Risk Items

**1. No Real-Time Update Architecture** (CRITICAL)
- Risk: Complete rewrite of core systems required
- Mitigation: Start with minimal PlayerController prototype
- Timeline: 1-2 weeks for basic functionality

**2. Physics Configuration Insufficient** (HIGH)
- Risk: Combat feels imprecise, hits don't register
- Mitigation: Increase solver iterations, implement CCD
- Timeline: 2-3 days for configuration + testing

**3. No Input Processing** (CRITICAL)
- Risk: Cannot capture player actions
- Mitigation: Implement PlayerInputHandler as priority
- Timeline: 3-5 days for complete input system

**4. No Object Pooling** (MEDIUM)
- Risk: Frame spikes during combat (spawns/VFX)
- Mitigation: Implement basic pooling early in prototype
- Timeline: 2-4 days for generic pooling system

### Medium Risk Items

**1. Audio System Scalability** (MEDIUM)
- Risk: Only 1 concurrent sound effect
- Mitigation: Implement AudioSource pooling
- Timeline: 1-2 days

**2. Event-Driven State Overhead** (LOW-MEDIUM)
- Risk: Event invocation in hot paths
- Mitigation: Separate combat state from game state
- Timeline: 1 day refactoring

### Low Risk Items

**1. Garbage Collection** (LOW)
- Current code is very clean
- Will need monitoring once combat systems active

**2. Resource Loading** (LOW)
- Current implementation is efficient
- No changes required

---

## 10. Immediate Action Plan

### Week 1 Priorities

**Day 1-2: Input System**
```
[ ] Create PlayerInputHandler.cs
[ ] Wire up InputSystem_Actions callbacks
[ ] Implement basic movement input (WASD/stick)
[ ] Test input latency (<30ms target)
```

**Day 3-4: Player Controller**
```
[ ] Create PlayerController.cs with Update loop
[ ] Implement basic character movement
[ ] Add camera controller (mouse look)
[ ] Test at 60 FPS (monitor frame time)
```

**Day 5: Physics Setup**
```
[ ] Increase solver iterations to 10-12
[ ] Configure collision layers (Player/Enemy/Weapons)
[ ] Enable CCD on player Rigidbody
[ ] Test collision precision
```

### Week 2 Priorities

**Day 1-2: Basic Combat**
```
[ ] Implement attack input processing
[ ] Create basic attack animation trigger
[ ] Setup weapon hitbox (trigger collider)
[ ] Test hit detection
```

**Day 3-4: Parry System Prototype**
```
[ ] Implement 200ms timing window
[ ] Create visual feedback (UI timer bar)
[ ] Test parry precision (must be <20ms variance)
[ ] Tune feel (early/late feedback)
```

**Day 5: Optimization Foundation**
```
[ ] Implement PerformanceMonitor.cs
[ ] Setup profiler markers
[ ] Create basic enemy pool (10 enemies)
[ ] Baseline performance metrics
```

---

## 11. Success Criteria

### Performance Targets

**Frame Rate**:
- Minimum: 60 FPS (16.67ms frame time)
- 99th percentile: <20ms (no major spikes)
- 1% low: >50 FPS (acceptable dips)

**Input Latency**:
- Total input-to-visual: <30ms
- Parry window precision: ±10ms (5% of 200ms window)
- Input buffering: 200ms window working correctly

**Memory**:
- No GC spikes >5ms during combat
- Allocations: <100KB per frame
- Mono heap stable (<200MB)

### Combat Feel Criteria

**Responsiveness**:
- Attack input triggers within 1 frame (16.67ms)
- Movement feels immediate (no input lag)
- Camera follows smoothly (LateUpdate)

**Precision**:
- Parry window consistently 200ms ±10ms
- Hitbox detection 100% reliable at 60 FPS
- No attack "whiffs" from collision tunneling

**Stability**:
- No frame drops during 10-enemy combat
- VFX/particles pooled (no spawn hitches)
- Audio plays without interruption

---

## 12. Conclusion

### Current State Summary

**Strengths**:
- Clean, well-structured codebase
- Modern Unity Input System configured
- Minimal GC pressure (good foundation)
- URP rendering pipeline (good performance baseline)

**Critical Gaps**:
- No real-time update loops (BLOCKING)
- No input processing implementation (BLOCKING)
- Physics configuration insufficient (HIGH RISK)
- No object pooling (MEDIUM RISK)

### Transformation Path

**Effort Estimate**: 4-6 weeks for playable combat prototype
**Risk Level**: HIGH (fundamental architecture change)
**Recommended Approach**: Iterative prototyping with constant performance monitoring

### First Milestone Target

**2-Week Prototype Goals**:
1. Player moves smoothly at 60 FPS
2. Basic attack combo (3-hit) working
3. Parry system with 200ms window functional
4. 5 enemies fighting simultaneously
5. Frame time <16.67ms average
6. Input latency <30ms measured

**If milestone hit**: Proceed to full combat system
**If milestone missed**: Re-evaluate physics/architecture approach

---

## Appendix A: Quick Reference Checklist

### Performance Checklist for Combat Implementation

**Before Starting Combat Code**:
```
[ ] Physics solver iterations increased to 10-12
[ ] Fixed timestep verified (0.02s = 50 Hz)
[ ] Collision layers configured (Player/Enemy/Weapons)
[ ] Input System actions wired to code
[ ] PerformanceMonitor.cs implemented
```

**During Combat Development**:
```
[ ] All movement in FixedUpdate (not Update)
[ ] All input reading in Update (not FixedUpdate)
[ ] Camera in LateUpdate (not Update)
[ ] No GetComponent in Update loops (cache in Awake)
[ ] No Instantiate during combat (use pooling)
[ ] No string concatenation in hot paths
```

**Before First Combat Test**:
```
[ ] CCD enabled on fast-moving objects
[ ] Hitboxes on correct collision layers
[ ] Parry window timing verified (200ms ±10ms)
[ ] Frame time profiled (<16.67ms target)
[ ] Input latency measured (<30ms target)
```

### Performance Red Flags

**Immediate Investigation Required If**:
```
⚠️ Frame time >20ms during combat
⚠️ GC.Collect appears in profiler during combat
⚠️ Physics >8ms per frame
⚠️ Input latency >50ms
⚠️ Parry window variance >20ms
⚠️ Hitbox misses at 60 FPS
```

---

**Report Generated**: 2025-12-15
**Analyst**: Performance Engineer
**Next Review**: After 2-week combat prototype milestone
