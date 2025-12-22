# Project Keira - Architectural Analysis for Souls-Like Transformation

**Analysis Date**: 2025-12-15
**Project Path**: `/Users/christian/Desktop/PROGRAMMING/GameDEV/project-keira/`
**Target**: Transform turn-based framework to action RPG souls-like (Lies of P style)
**MVP Scope**: Movement, Stamina, Attack, Parry, Dodge, Lock-on, Health, 1 Enemy

---

## Executive Summary

The existing turn-based architecture demonstrates solid foundational patterns but requires substantial transformation to support real-time souls-like combat. The singleton hierarchy, unit system, and ScriptableObject configuration are reusable with modifications, while the state machine and turn-based logic require complete reimplementation. Critical gaps exist in input handling, physics-based combat, animation state management, and AI behavior systems.

**Transformation Strategy**: Hybrid approach - retain architectural patterns (60% reusable), replace turn-based logic (40% complete rewrite)

---

## 1. Existing Pattern Analysis

### 1.1 Singleton Hierarchy ⚖️ RETAIN WITH MODIFICATIONS

**Pattern Location**: `/Assets/_Scripts/Utilities/StaticInstance.cs`

**Current Implementation**:
```csharp
StaticInstance<T>           // Base - allows override
    ↓
Singleton<T>                // Destroys duplicates
    ↓
PersistentSingleton<T>      // Survives scene loads
```

**Evaluation**:
- ✅ **Strengths**: Clean hierarchy, scene-persistent capability, type-safe
- ⚠️ **Concerns**: Global state, potential tight coupling, no dependency injection
- 🎯 **Souls-Like Fit**: Excellent for persistent systems (InputManager, CombatSystem, AudioSystem)

**Adaptation Strategy**:
```
KEEP: All three classes as architectural foundation
MODIFY: Add lifecycle hooks for system initialization order
ADD: Optional dependency injection pattern for testing
USE FOR:
  - CombatManager (Singleton)
  - InputManager (PersistentSingleton)
  - StaminaSystem (Singleton)
  - LockOnSystem (Singleton)
  - AudioSystem (existing, PersistentSingleton)
  - ResourceSystem (existing, PersistentSingleton)
```

**Risk Level**: 🟢 Low - Well-tested pattern, minimal changes required

---

### 1.2 State Machine (GameManager) 🔄 COMPLETE REWRITE

**Pattern Location**: `/Assets/_Scripts/Managers/ExampleGameManager.cs`

**Current Implementation**:
```csharp
enum GameState { Starting, SpawningHeroes, SpawningEnemies, HeroTurn, EnemyTurn, Win, Lose }
- Event-driven state changes
- Switch-based state handling
- Turn-based progression
```

**Evaluation**:
- ✅ **Strengths**: Clear state transitions, event-based communication, simple to understand
- ❌ **Critical Issues**:
  - Turn-based states incompatible with real-time combat
  - No support for parallel state execution (player moving + enemy AI)
  - No hierarchical state management (combat substates: attacking, dodging, parrying)
- 🎯 **Souls-Like Fit**: Core concept valid, but implementation must be rebuilt

**Replacement Strategy**:
```
DISCARD: Current GameState enum and turn-based logic
REPLACE WITH: Hierarchical State Machine (HSM)

New State Hierarchy:
GameState (top-level)
  ├─ Initialization
  ├─ Gameplay
  │   ├─ Exploration (player movement, stamina regen)
  │   ├─ Combat
  │   │   ├─ PlayerCombatState (attacking, dodging, parrying, blocking)
  │   │   └─ EnemyCombatState (AI behavior tree)
  │   └─ Locked-On (camera + movement modifications)
  ├─ Pause
  ├─ Victory
  └─ Death

Key Changes:
- States run continuously (Update loop), not turn-based
- Support concurrent states (player + enemy states active simultaneously)
- Add state entry/exit/update methods
- Integrate with animation state machine
```

**Implementation Approach**:
```csharp
// New pattern - State interface
public interface IGameState {
    void Enter();
    void Update();
    void Exit();
    void HandleInput(InputData input);
}

// StateMachine manages state stack for hierarchical states
public class GameStateMachine : Singleton<GameStateMachine> {
    private Stack<IGameState> _stateStack;
    // Push/Pop for substates, ChangeState for top-level transitions
}
```

**Risk Level**: 🔴 High - Core gameplay logic, requires careful design and testing

---

### 1.3 Unit System 🏗️ SIGNIFICANT MODIFICATIONS

**Pattern Location**: `/Assets/_Scripts/Units/`

**Current Implementation**:
```
UnitBase (abstract)
  ├─ HeroUnitBase (turn-based input, state event listening)
  └─ EnemyUnitBase (empty placeholder)

Key Features:
- Stats struct (Health, AttackPower, TravelDistance)
- SetStats() for stat injection
- TakeDamage() placeholder
- Turn-based movement flags
```

**Evaluation**:
- ✅ **Strengths**: Clear inheritance hierarchy, stats abstraction
- ⚠️ **Issues**:
  - No real-time movement/physics
  - No animation state management
  - No stamina or poise mechanics
  - TakeDamage() not implemented
  - Turn-based input handling
- 🎯 **Souls-Like Fit**: Structure valid, internals need complete overhaul

**Transformation Plan**:

**Step 1: Expand Stats System**
```csharp
// From:
public struct Stats {
    public int Health;
    public int AttackPower;
    public int TravelDistance;
}

// To:
public struct CombatStats {
    // Core Stats
    public float MaxHealth;
    public float CurrentHealth;
    public float MaxStamina;
    public float CurrentStamina;

    // Combat Stats
    public float AttackDamage;
    public float StaminaCostLight;
    public float StaminaCostHeavy;
    public float StaminaCostDodge;
    public float StaminaCostBlock;

    // Defense Stats
    public float Poise;            // Resistance to stagger
    public float PhysicalDefense;
    public float ParryWindow;      // Timing window in seconds

    // Movement Stats
    public float MovementSpeed;
    public float DodgeDistance;
    public float StaminaRegenRate;
    public float StaminaRegenDelay;
}
```

**Step 2: Rewrite UnitBase**
```csharp
public abstract class UnitBase : MonoBehaviour {
    // Components (add via RequireComponent)
    protected Rigidbody _rigidbody;
    protected Animator _animator;
    protected CapsuleCollider _collider;

    // Stats
    public CombatStats Stats { get; protected set; }

    // State
    public bool IsDead { get; protected set; }
    public bool IsStaggered { get; protected set; }
    public float CurrentPoise { get; protected set; }

    // Core Methods (real-time)
    public abstract void HandleMovement(Vector3 direction);
    public abstract void HandleRotation(Vector3 lookDirection);
    public virtual void TakeDamage(float damage, Vector3 hitDirection);
    public virtual void ConsumeStamina(float amount);
    public virtual void Die();

    // Stamina Management
    protected virtual void UpdateStamina(float deltaTime);
    protected virtual bool HasStamina(float cost);
}
```

**Step 3: Specialize Hero and Enemy**
```csharp
// PlayerController - real-time input handling
public class PlayerController : UnitBase {
    private IInputHandler _inputHandler;
    private PlayerCombatStateMachine _combatStateMachine;
    private LockOnTarget _currentLockOn;

    public override void HandleMovement(Vector3 direction);
    public void PerformLightAttack();
    public void PerformHeavyAttack();
    public void PerformDodge(Vector3 direction);
    public void PerformParry();
    public void ToggleLockOn();
}

// EnemyController - AI-driven behavior
public class EnemyController : UnitBase {
    private IEnemyAI _ai;
    private EnemyCombatStateMachine _combatStateMachine;

    public override void HandleMovement(Vector3 direction);
    public void ExecuteAttackPattern(AttackPattern pattern);
    public float DetectionRange { get; set; }
    public Transform Player { get; set; }
}
```

**Risk Level**: 🟡 Medium - Clear upgrade path, manageable complexity

---

### 1.4 ScriptableObject Configuration 📦 RETAIN AND EXPAND

**Pattern Location**: `/Assets/_Scripts/Scriptables/`

**Current Implementation**:
```csharp
ScriptableExampleUnitBase (abstract)
  ├─ Faction enum
  ├─ Stats struct
  ├─ Prefab reference
  └─ Menu data (Description, Sprite)
```

**Evaluation**:
- ✅ **Strengths**: Designer-friendly, data-driven, serialized asset workflow
- ✅ **Unity Best Practice**: Excellent for configuration and balancing
- 🎯 **Souls-Like Fit**: Perfect for weapon/enemy/upgrade data

**Expansion Plan**:

**New ScriptableObjects for Souls-Like**:

```csharp
// 1. Weapon Configuration
[CreateAssetMenu(fileName = "New Weapon", menuName = "Combat/Weapon")]
public class ScriptableWeapon : ScriptableObject {
    public string WeaponName;
    public WeaponType Type; // Sword, Greatsword, Dagger

    // Combat Data
    public float BaseDamage;
    public float StaminaCostLight;
    public float StaminaCostHeavy;
    public float AttackSpeed;
    public float ParryWindow;

    // Animations
    public AnimationClip[] LightAttackAnims;
    public AnimationClip[] HeavyAttackAnims;
    public AnimationClip[] ComboAnims;

    // VFX/SFX
    public GameObject HitEffectPrefab;
    public AudioClip[] AttackSounds;
    public AudioClip ParrySound;
}

// 2. Enemy Configuration
[CreateAssetMenu(fileName = "New Enemy", menuName = "Combat/Enemy")]
public class ScriptableEnemy : ScriptableObject {
    public string EnemyName;
    public CombatStats BaseStats;
    public GameObject Prefab;

    // AI Behavior
    public float AggroRange;
    public float AttackRange;
    public float CirclingDistance;
    public AttackPattern[] AttackPatterns;
    public float AttackCooldown;

    // Animations
    public RuntimeAnimatorController AnimatorController;

    // Drops/Rewards
    public int SoulsOnDeath;
    public LootTable[] PossibleDrops;
}

// 3. Attack Pattern Configuration (for combos and enemy attacks)
[CreateAssetMenu(fileName = "New Attack Pattern", menuName = "Combat/AttackPattern")]
public class ScriptableAttackPattern : ScriptableObject {
    public AttackType[] AttackSequence; // Light, Heavy, Special
    public float[] AttackTimings;
    public float[] DamageMultipliers;
    public bool CanBeCancelled;
    public float CancelWindow;
}

// 4. Upgrade System Configuration
[CreateAssetMenu(fileName = "New Upgrade", menuName = "Progression/Upgrade")]
public class ScriptableUpgrade : ScriptableObject {
    public string UpgradeName;
    public UpgradeType Type; // Health, Stamina, Damage
    public int[] CostPerLevel;
    public float[] ValuePerLevel;
    public int MaxLevel;
}
```

**ResourceSystem Extension**:
```csharp
public class ResourceSystem : PersistentSingleton<ResourceSystem> {
    // Existing
    public List<ScriptableExampleHero> ExampleHeroes { get; private set; }

    // New Collections
    public List<ScriptableWeapon> Weapons { get; private set; }
    public List<ScriptableEnemy> Enemies { get; private set; }
    public List<ScriptableAttackPattern> AttackPatterns { get; private set; }
    public List<ScriptableUpgrade> Upgrades { get; private set; }

    // Query Methods
    public ScriptableWeapon GetWeapon(string name);
    public ScriptableEnemy GetEnemy(string name);
    public ScriptableAttackPattern GetAttackPattern(string id);
}
```

**Risk Level**: 🟢 Low - Extends existing working pattern

---

### 1.5 Systems Architecture 🔧 RETAIN AND AUGMENT

**Pattern Location**: `/Assets/_Scripts/Systems/`

**Current Implementation**:
```csharp
Systems : PersistentSingleton<Systems>  // Root persistent object
  ├─ AudioSystem : StaticInstance (music + 3D sounds)
  └─ ResourceSystem : StaticInstance (ScriptableObject loading)
```

**Evaluation**:
- ✅ **Strengths**: Clean parent-child hierarchy, persistent across scenes, centralized access
- ✅ **AudioSystem**: Already supports 3D spatial audio (useful for combat)
- 🎯 **Souls-Like Fit**: Excellent foundation for combat subsystems

**Augmentation Plan**:

**Add New Combat Systems**:

```csharp
// 1. Input System (unified input handling)
public class InputSystem : Singleton<InputSystem> {
    public InputActions Actions { get; private set; } // Unity Input System

    // Input Events
    public event Action<Vector2> OnMovementInput;
    public event Action OnLightAttackPressed;
    public event Action OnHeavyAttackPressed;
    public event Action OnDodgePressed;
    public event Action OnParryPressed;
    public event Action OnLockOnToggled;
    public event Action OnInteractPressed;

    // Input State Queries
    public Vector2 GetMovementInput();
    public Vector2 GetCameraInput();
    public bool IsBlockHeld();
}

// 2. Stamina System (centralized stamina management)
public class StaminaSystem : Singleton<StaminaSystem> {
    public void ConsumeStamina(UnitBase unit, float cost);
    public void RegenerateStamina(UnitBase unit, float deltaTime);
    public bool CanAfford(UnitBase unit, float cost);
    public void SetRegenDelay(UnitBase unit, float delay);
}

// 3. Combat System (damage calculation, hit detection)
public class CombatSystem : Singleton<CombatSystem> {
    public void ProcessMeleeAttack(UnitBase attacker, AttackData attackData);
    public void ProcessDamage(UnitBase target, float damage, Vector3 hitDirection);
    public bool ProcessParry(UnitBase defender, UnitBase attacker);
    public void ProcessPoise(UnitBase target, float poiseDamage);

    // Hit Detection
    public List<Collider> DetectHitsInCone(Vector3 origin, Vector3 direction, float range, float angle);
}

// 4. Lock-On System (target acquisition and tracking)
public class LockOnSystem : Singleton<LockOnSystem> {
    public void AcquireTarget(Transform player, float maxDistance, float maxAngle);
    public void ReleaseTarget();
    public void CycleTarget(int direction);
    public Transform CurrentTarget { get; private set; }
    public bool IsLockedOn { get; private set; }
}

// 5. Animation System (animation coordination)
public class AnimationSystem : Singleton<AnimationSystem> {
    public void PlayAnimation(Animator animator, string animationName, float transitionTime);
    public void SetAnimatorParameter(Animator animator, string paramName, object value);
    public bool IsAnimationPlaying(Animator animator, string animationName);
    public float GetAnimationLength(Animator animator, string animationName);
}

// 6. VFX System (particle effects, hit effects)
public class VFXSystem : StaticInstance<VFXSystem> {
    public void PlayHitEffect(Vector3 position, Vector3 normal, HitEffectType type);
    public void PlayParryEffect(Vector3 position);
    public void PlayBloodEffect(Vector3 position, float intensity);
}
```

**Updated Systems Hierarchy**:
```csharp
public class Systems : PersistentSingleton<Systems> {
    // Existing
    public AudioSystem Audio { get; private set; }
    public ResourceSystem Resources { get; private set; }

    // New Combat Systems
    public InputSystem Input { get; private set; }
    public StaminaSystem Stamina { get; private set; }
    public CombatSystem Combat { get; private set; }
    public LockOnSystem LockOn { get; private set; }
    public AnimationSystem Animation { get; private set; }
    public VFXSystem VFX { get; private set; }

    protected override void Awake() {
        base.Awake();
        InitializeSystems();
    }

    private void InitializeSystems() {
        // Initialize in dependency order
        // Input -> Combat -> Stamina -> LockOn -> Animation -> VFX
    }
}
```

**Risk Level**: 🟢 Low - Extends existing architecture naturally

---

## 2. Architectural Gaps for Souls-Like Combat

### 2.1 Input Handling System ❌ MISSING

**Current State**: Mouse-based turn interaction (OnMouseDown)

**Required for Souls-Like**:
- Unity Input System integration (gamepad + keyboard/mouse)
- Input buffering (queue inputs during animations)
- Context-sensitive input mapping (locked-on vs free movement)
- Input cancellation and priority system

**Implementation Priority**: 🔴 Critical - Foundation for all player interaction

---

### 2.2 Physics-Based Combat ❌ MISSING

**Current State**: No collision detection, no physics-based movement

**Required for Souls-Like**:
- Rigidbody movement with momentum
- Hitbox/hurtbox collision detection
- Physics layers for weapon hits, player, enemies
- Root motion animation support
- Ground detection and gravity

**Implementation Priority**: 🔴 Critical - Core combat mechanics

---

### 2.3 Animation State Machine Integration ❌ MISSING

**Current State**: No animation controller, no state synchronization

**Required for Souls-Like**:
- Animator controller with combat states (idle, walk, run, attack, dodge, parry, hurt, death)
- Animation event system (trigger damage at specific animation frames)
- Root motion for realistic movement
- Animation cancelling rules
- Blend trees for directional movement

**Implementation Priority**: 🔴 Critical - Visual feedback and timing

---

### 2.4 AI Behavior System ❌ MISSING

**Current State**: Empty EnemyUnitBase placeholder

**Required for Souls-Like**:
- Behavior tree or finite state machine for enemy AI
- Pathfinding/NavMesh integration
- Detection and aggro system
- Attack pattern execution
- Circling and positioning behavior
- Reaction to player actions (parry response, dodge tracking)

**Implementation Priority**: 🟡 High - Required for MVP (1 enemy)

---

### 2.5 Camera System ❌ MISSING

**Current State**: No camera controller

**Required for Souls-Like**:
- Third-person camera with collision avoidance
- Lock-on camera mode (focus on target)
- Free-look camera mode
- Camera shake for impacts
- Dynamic camera angle adjustment

**Implementation Priority**: 🟡 High - Player experience depends on it

---

### 2.6 Damage and Poise System ❌ PARTIALLY MISSING

**Current State**: TakeDamage() method stub exists but not implemented

**Required for Souls-Like**:
- Damage calculation (base damage, defense, resistances)
- Poise system (accumulate poise damage → stagger)
- Invincibility frames (i-frames) during dodge
- Knockback and hitstun
- Critical hit system (backstab, parry riposte)

**Implementation Priority**: 🔴 Critical - Combat feel and balance

---

### 2.7 Combo and Attack Chain System ❌ MISSING

**Current State**: No attack sequencing

**Required for Souls-Like**:
- Light attack combos (3-hit chains)
- Heavy attack charged mechanics
- Combo window timing
- Attack cancelling into dodge
- Animation state tracking for combo progression

**Implementation Priority**: 🟡 High - Depth of combat system

---

## 3. Recommended Transformation Roadmap

### Phase 1: Core Infrastructure (Week 1-2)
**Goal**: Replace turn-based foundation with real-time systems

**Tasks**:
1. ✅ Implement Hierarchical State Machine
2. ✅ Integrate Unity Input System
3. ✅ Expand Stats system to CombatStats
4. ✅ Rewrite UnitBase for real-time physics
5. ✅ Create InputSystem singleton
6. ✅ Create StaminaSystem singleton
7. ✅ Setup physics layers and collision matrix

**Deliverable**: Player can move with WASD, stamina drains/regenerates

---

### Phase 2: Player Combat Mechanics (Week 3-4)
**Goal**: Implement core player actions

**Tasks**:
1. ✅ Setup Animator Controller with combat states
2. ✅ Implement basic attack (light attack, no combo)
3. ✅ Implement dodge with i-frames
4. ✅ Implement stamina consumption for actions
5. ✅ Create CombatSystem singleton
6. ✅ Implement hitbox detection system
7. ✅ Create ScriptableWeapon configuration
8. ✅ Implement TakeDamage with health reduction

**Deliverable**: Player can attack, dodge, take damage, die

---

### Phase 3: Lock-On and Camera (Week 5)
**Goal**: Enhance player control and visibility

**Tasks**:
1. ✅ Implement third-person camera controller
2. ✅ Create LockOnSystem singleton
3. ✅ Implement target acquisition logic
4. ✅ Implement lock-on camera mode
5. ✅ Add camera collision avoidance
6. ✅ Add lock-on target switching

**Deliverable**: Player can lock onto targets, camera follows smoothly

---

### Phase 4: Enemy Implementation (Week 6-7)
**Goal**: First functional enemy

**Tasks**:
1. ✅ Create ScriptableEnemy configuration
2. ✅ Implement EnemyController with basic AI
3. ✅ Implement enemy attack pattern
4. ✅ Setup enemy Animator Controller
5. ✅ Implement enemy detection/aggro
6. ✅ Implement enemy damage and death
7. ✅ Balance enemy stats for difficulty

**Deliverable**: 1 enemy that detects player, attacks, and can be defeated

---

### Phase 5: Advanced Combat Mechanics (Week 8-9)
**Goal**: Parry system and combat depth

**Tasks**:
1. ✅ Implement parry mechanic with timing window
2. ✅ Implement parry riposte (critical attack)
3. ✅ Implement poise and stagger system
4. ✅ Add light attack combo chain (3 hits)
5. ✅ Add VFXSystem for hit effects
6. ✅ Polish animation transitions
7. ✅ Add audio feedback for combat

**Deliverable**: Full combat loop with parry, combos, stagger

---

### Phase 6: Polish and Balancing (Week 10)
**Goal**: MVP feature-complete

**Tasks**:
1. ✅ Balance all combat stats
2. ✅ Playtest and iterate
3. ✅ Add UI for health/stamina bars
4. ✅ Add enemy health bar
5. ✅ Camera shake for impacts
6. ✅ Particle effects for hits/parry
7. ✅ Death screen and respawn logic

**Deliverable**: Polished MVP ready for demonstration

---

## 4. Component Dependency Diagram

```
┌─────────────────────────────────────────────────────────────────┐
│                    PERSISTENT LAYER (DontDestroyOnLoad)         │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │  Systems (PersistentSingleton)                           │   │
│  │  ├─ InputSystem (handles input actions)                  │   │
│  │  ├─ AudioSystem (music, SFX)                            │   │
│  │  ├─ ResourceSystem (loads ScriptableObjects)            │   │
│  │  ├─ StaminaSystem (stamina management)                  │   │
│  │  └─ VFXSystem (particle effects)                        │   │
│  └──────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────┘
                              ↓ provides services to
┌─────────────────────────────────────────────────────────────────┐
│                      SCENE-SPECIFIC LAYER                       │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │  GameStateMachine (Singleton per scene)                  │   │
│  │  ├─ Manages: Initialization, Gameplay, Combat, Pause     │   │
│  │  └─ Coordinates: Player, Enemy, Camera states            │   │
│  └──────────────────────────────────────────────────────────┘   │
│                              ↓                                   │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │  CombatSystem (Singleton per scene)                      │   │
│  │  ├─ Hit detection                                        │   │
│  │  ├─ Damage calculation                                   │   │
│  │  ├─ Parry validation                                     │   │
│  │  └─ Poise management                                     │   │
│  └──────────────────────────────────────────────────────────┘   │
│                              ↓                                   │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │  LockOnSystem (Singleton per scene)                      │   │
│  │  ├─ Target acquisition                                   │   │
│  │  ├─ Target tracking                                      │   │
│  │  └─ Camera coordination                                  │   │
│  └──────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────┘
                              ↓ used by
┌─────────────────────────────────────────────────────────────────┐
│                        ENTITY LAYER                              │
│  ┌─────────────────────┐         ┌─────────────────────┐        │
│  │  PlayerController   │         │  EnemyController     │        │
│  │  extends UnitBase   │         │  extends UnitBase    │        │
│  │  ├─ Movement        │         │  ├─ AI Behavior      │        │
│  │  ├─ Combat Actions  │         │  ├─ Attack Patterns  │        │
│  │  ├─ Stamina        │         │  ├─ Detection        │        │
│  │  └─ Animation      │         │  └─ Animation        │        │
│  └─────────────────────┘         └─────────────────────┘        │
│           ↑                                ↑                     │
│           │ configured by                  │ configured by       │
│           ↓                                ↓                     │
│  ┌─────────────────────┐         ┌─────────────────────┐        │
│  │ ScriptableWeapon    │         │ ScriptableEnemy      │        │
│  │ - Stats             │         │ - CombatStats        │        │
│  │ - Animations        │         │ - AI Parameters      │        │
│  │ - VFX/SFX          │         │ - AttackPatterns     │        │
│  └─────────────────────┘         └─────────────────────┘        │
└─────────────────────────────────────────────────────────────────┘
                              ↑ loaded by
                    ┌─────────────────────┐
                    │  ResourceSystem     │
                    │  (at startup)       │
                    └─────────────────────┘
```

---

## 5. Data Flow Analysis

### 5.1 Player Attack Flow
```
1. Input: Player presses Light Attack button
   ↓
2. InputSystem.OnLightAttackPressed event fires
   ↓
3. PlayerController receives event
   ↓
4. Check: PlayerCombatState allows attack? (not dodging/stunned)
   ↓
5. Check: StaminaSystem.CanAfford(player, attackCost)?
   ↓
6. StaminaSystem.ConsumeStamina(player, attackCost)
   ↓
7. AnimationSystem.PlayAnimation(animator, "LightAttack_1")
   ↓
8. Animation Event (at hit frame): TriggerHitDetection()
   ↓
9. CombatSystem.DetectHitsInCone(weaponPosition, forward, range, angle)
   ↓
10. For each hit: CombatSystem.ProcessDamage(enemy, damage, hitDirection)
    ↓
11. Enemy: TakeDamage(damage, hitDirection)
    ↓
12. Enemy: UpdatePoise(poiseDamage) → stagger if poise broken
    ↓
13. VFXSystem.PlayHitEffect(hitPosition, hitNormal)
    ↓
14. AudioSystem.PlaySound(hitSound, hitPosition)
    ↓
15. Animation completes → PlayerCombatState returns to Idle
```

### 5.2 Enemy AI Decision Flow
```
1. Update: EnemyAI.Update() called each frame
   ↓
2. Check: Player in detection range?
   ↓ YES
3. StateTransition: Patrol → Aggro
   ↓
4. Check: Player in attack range?
   ↓ NO
5. Action: NavMeshAgent.SetDestination(playerPosition)
   ↓ (continues moving toward player)
   ↓ YES (player now in attack range)
6. Check: Attack on cooldown?
   ↓ NO
7. SelectAttackPattern: Choose from ScriptableEnemy.AttackPatterns[]
   ↓
8. AnimationSystem.PlayAnimation(animator, attackPattern.AnimationName)
   ↓
9. Animation Event: TriggerHitDetection()
   ↓
10. CombatSystem.DetectHitsInCone(weaponPosition, forward, range, angle)
    ↓
11. If player hit AND not dodging (i-frames): PlayerController.TakeDamage()
    ↓
12. StartCoroutine: AttackCooldown(attackPattern.Cooldown)
    ↓
13. StateTransition: Attacking → Circling (maintain distance)
```

### 5.3 Lock-On Toggle Flow
```
1. Input: Player presses Lock-On button
   ↓
2. InputSystem.OnLockOnToggled event fires
   ↓
3. PlayerController receives event
   ↓
4. Check: LockOnSystem.IsLockedOn?
   ↓ NO (acquire target)
5. LockOnSystem.AcquireTarget(playerTransform, maxDistance, maxAngle)
   ↓
6. RaycastAll: Find all enemies in range and view cone
   ↓
7. Filter: Closest enemy to screen center
   ↓
8. Set: LockOnSystem.CurrentTarget = closestEnemy
   ↓
9. CameraController.SetLockOnMode(true, currentTarget)
   ↓
10. PlayerController.MovementMode = LockOn (strafing enabled)
    ↓
11. UI: Show lock-on reticle on target
    ↓ YES (release target)
12. LockOnSystem.ReleaseTarget()
    ↓
13. CameraController.SetLockOnMode(false)
    ↓
14. PlayerController.MovementMode = Free
    ↓
15. UI: Hide lock-on reticle
```

---

## 6. Critical Technical Decisions

### 6.1 Character Controller vs Rigidbody

**Recommendation**: Use Rigidbody with constraints

**Rationale**:
- Souls-like combat requires knockback, momentum, and physics interactions
- CharacterController has no physics reactions (immune to forces)
- Rigidbody allows: weapon knockback, environmental physics, ragdoll death
- Constraints prevent unwanted rotation (freeze X/Z rotation)

**Configuration**:
```csharp
Rigidbody rb = GetComponent<Rigidbody>();
rb.constraints = RigidbodyConstraints.FreezeRotationX |
                 RigidbodyConstraints.FreezeRotationZ;
rb.interpolation = RigidbodyInterpolation.Interpolate; // smooth movement
rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic; // prevent tunneling
```

---

### 6.2 Animation System: Mechanim vs Custom

**Recommendation**: Unity Animator (Mechanim) with hybrid root motion

**Rationale**:
- Mechanim provides blend trees for smooth directional movement
- Animation events for hit detection timing
- State machine integration with gameplay states
- Root motion for realistic attack movement

**Configuration**:
- Root motion: Enabled for attacks/dodges, disabled for locomotion
- Update mode: Animate Physics (sync with Rigidbody)
- Blend trees: 2D Freeform Directional for 8-way movement

---

### 6.3 Hit Detection: Raycasts vs Trigger Colliders

**Recommendation**: Hybrid approach

**Raycasts for weapons**:
- Precise hit detection at animation frame
- No continuous collider overhead
- Easy to visualize in debug mode

**Trigger Colliders for hitboxes**:
- Enemy attack windups with active hitbox during animation
- Player hurtbox (CapsuleCollider with isTrigger)

**Implementation**:
```csharp
// Player weapon swing (raycast at animation event)
public void OnAttackFrame() {
    Vector3 origin = weaponTransform.position;
    Vector3 direction = weaponTransform.forward;
    RaycastHit[] hits = Physics.SphereCastAll(origin, hitRadius, direction, hitRange, enemyLayer);
    foreach (var hit in hits) {
        CombatSystem.Instance.ProcessDamage(hit.collider.GetComponent<UnitBase>(), damage, direction);
    }
}

// Enemy attack (trigger collider enabled during animation)
void OnTriggerEnter(Collider other) {
    if (other.CompareTag("Player") && isAttacking) {
        other.GetComponent<PlayerController>().TakeDamage(attackDamage, transform.forward);
    }
}
```

---

### 6.4 Input System: Legacy vs New Input System

**Recommendation**: Unity Input System (com.unity.inputsystem)

**Rationale**:
- Unified gamepad + keyboard/mouse support
- Action-based API (semantic actions, not raw input)
- Input rebinding support
- Control scheme switching (gamepad detected → auto-switch)
- Input buffering and composite actions built-in

**Configuration**:
```csharp
// Create InputActions asset in project
[CreateAssetMenu(fileName = "InputActions", menuName = "Input/InputActions")]
public class InputActions : ScriptableObject {
    public InputActionAsset Asset;
}

// Actions:
- Movement (Vector2)
- LightAttack (Button)
- HeavyAttack (Button)
- Dodge (Button)
- Parry (Button)
- LockOn (Button)
- Interact (Button)
```

---

## 7. Performance Considerations

### 7.1 Optimization Targets
- **Target Frame Rate**: 60 FPS (souls-like standard)
- **Physics Update**: Fixed 60Hz (FixedUpdate every 0.0167s)
- **Max Concurrent Enemies**: 3-5 (for MVP, scale later)

### 7.2 Optimization Strategies

**Physics**:
- Use Physics.OverlapSphere sparingly (cache results)
- Limit raycasts per frame (max 10)
- Use layer masks aggressively (reduce collision checks)

**Animation**:
- Limit Animator Controller complexity (max 20 states)
- Use Animation Layers for upper/lower body separation
- Disable Animator on distant enemies (outside camera view)

**AI**:
- Update AI on staggered schedule (not all enemies every frame)
- Use NavMesh for pathfinding (optimized by Unity)
- Simple behavior trees (max depth 3)

---

## 8. Testing Strategy

### 8.1 Unit Testing Approach
- **Tools**: Unity Test Framework, NUnit
- **Test Targets**:
  - CombatStats calculations (damage, stamina costs)
  - State machine transitions
  - Input buffering logic
  - Stamina regeneration timing

### 8.2 Integration Testing
- **Combat Loop**: Player attacks → Enemy damaged → Enemy retaliates → Player parries
- **Lock-On**: Target acquisition → Target switch → Target lost (out of range)
- **Stamina**: Exhaust stamina → Actions locked → Regeneration → Actions unlocked

### 8.3 Playtesting Focus
- **Combat Feel**: Attack weight, dodge responsiveness, parry satisfaction
- **Difficulty**: Can player defeat enemy with skill? Is it fair?
- **Camera**: Does lock-on help? Is free camera smooth?
- **Feedback**: Are VFX/SFX clear and satisfying?

---

## 9. Risk Assessment

### 9.1 High-Risk Areas (🔴 Requires Prototyping)

1. **Animation-Driven Combat Timing**
   - Risk: Attacks feel unresponsive or sluggish
   - Mitigation: Prototype early, iterate on animation speeds
   - Fallback: Reduce root motion, increase responsiveness

2. **Parry Window Balance**
   - Risk: Too easy (trivial combat) or too hard (frustrating)
   - Mitigation: Expose parry window as tunable parameter in ScriptableWeapon
   - Fallback: Add visual cue (enemy weapon flash) to telegraph

3. **Enemy AI Engagement**
   - Risk: AI too passive (boring) or too aggressive (unfair)
   - Mitigation: Behavior tree with tunable aggression parameters
   - Fallback: Simple state machine with fixed attack patterns

### 9.2 Medium-Risk Areas (🟡 Known Solutions Exist)

1. **Camera Collision**
   - Risk: Camera clips through walls, disorienting player
   - Mitigation: Use Cinemachine ClearShot or manual raycast adjustment

2. **Lock-On Target Switching**
   - Risk: Wrong target selected, awkward switching
   - Mitigation: Weight by screen position + distance, allow manual override

3. **Stamina Balance**
   - Risk: Too restrictive (annoying) or too generous (trivial)
   - Mitigation: Playtesting with adjustable costs/regen rates

### 9.3 Low-Risk Areas (🟢 Standard Implementations)

1. **Input Handling** - Unity Input System is mature
2. **Audio System** - Already functional, just add new sounds
3. **ScriptableObjects** - Proven pattern in current codebase
4. **Singleton Management** - Working hierarchy, no changes needed

---

## 10. Recommended Technology Stack

### 10.1 Unity Packages (Required)
```json
{
  "com.unity.inputsystem": "1.7.0",           // New Input System
  "com.unity.cinemachine": "2.9.7",          // Camera system
  "com.unity.ai.navigation": "1.1.5",        // NavMesh for enemy AI
  "com.unity.timeline": "1.7.6",             // Cutscenes (future)
  "com.unity.test-framework": "1.1.33"       // Unit testing
}
```

### 10.2 Unity Packages (Optional but Recommended)
```json
{
  "com.unity.visualeffectgraph": "14.0.8",   // VFX (blood, sparks)
  "com.unity.postprocessing": "3.2.2",       // Screen effects (damage vignette)
  "com.unity.probuilder": "5.2.2"            // Level prototyping
}
```

### 10.3 Third-Party Assets (Consider)
- **Animancer** (Kybernetik): More flexible animation control than Mechanim
- **Behavior Designer** (Opsive): Visual behavior tree editor for enemy AI
- **Final IK** (RootMotion): Foot placement and look-at IK

---

## 11. Migration Strategy Summary

### 11.1 Retain (60% of existing code)
✅ **Singleton Hierarchy** → Foundation for all systems
✅ **ScriptableObject Pattern** → Expand for combat configuration
✅ **Systems Architecture** → Add new combat systems as children
✅ **AudioSystem** → Keep as-is, add new combat sounds
✅ **ResourceSystem** → Extend with new ScriptableObject types

### 11.2 Modify (20% of existing code)
⚖️ **UnitBase** → Add real-time physics, animations, stamina
⚖️ **Stats Struct** → Expand to CombatStats with stamina/poise
⚖️ **HeroUnitBase** → Rebuild as PlayerController with real-time input
⚖️ **EnemyUnitBase** → Implement as EnemyController with AI

### 11.3 Replace (20% of existing code)
🔄 **GameState Enum** → Replace with Hierarchical State Machine
🔄 **Turn-Based Logic** → Replace with real-time Update loop
🔄 **Mouse Input (OnMouseDown)** → Replace with Unity Input System
🔄 **ExampleGameManager** → Replace with GameStateMachine

---

## 12. Next Steps (Immediate Actions)

### Week 1 - Foundation Sprint
1. Create new branch: `feature/souls-like-transformation`
2. Install Unity packages: InputSystem, Cinemachine, NavMesh
3. Create hierarchical state machine architecture
4. Implement InputSystem with action map
5. Expand Stats to CombatStats
6. Prototype player movement with Rigidbody

### Week 2 - Core Combat
1. Create basic Animator Controller (idle, walk, attack, dodge)
2. Implement PlayerController with attack/dodge actions
3. Add stamina consumption and regeneration
4. Implement hitbox detection system
5. Create first ScriptableWeapon asset
6. Test: Player can attack and dodge with stamina

### Documentation Deliverables
- [ ] Hierarchical State Machine Design Document
- [ ] Input Action Map Specification
- [ ] CombatStats Balance Spreadsheet
- [ ] Animation State Flow Diagram
- [ ] ScriptableObject Template Library

---

## 13. Conclusion

The existing Project Keira architecture provides a solid foundation for transformation into a souls-like framework. The singleton hierarchy, ScriptableObject configuration pattern, and systems architecture are production-ready and should be retained with minimal modifications. The primary transformation work focuses on replacing turn-based logic with real-time combat systems, implementing physics-based movement and combat, and building new subsystems for input, stamina, lock-on, and AI.

**Key Success Factors**:
1. Prototype combat feel early (Week 2) to validate approach
2. Maintain clean separation between systems using existing singleton pattern
3. Leverage ScriptableObjects for all balance-sensitive data
4. Implement animation-driven combat with proper event timing
5. Playtest continuously to validate combat satisfaction

**Estimated Timeline**: 10 weeks to polished MVP
**Risk Level**: Medium (well-defined requirements, proven patterns, clear gaps)
**Architectural Debt**: Low (existing patterns align with souls-like needs)

The transformation is architecturally sound and technically feasible with the recommended approach.

---

**Analysis Completed**: 2025-12-15
**Analyzed By**: Claude (System Architect)
**Report Version**: 1.0
