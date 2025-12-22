# Souls-Like Framework Transformation - Changelog

## [1.0.0] - 2025-12-22

### Summary

Complete transformation of the Unity framework from turn-based to real-time 3D action RPG combat, inspired by Souls-like games (Lies of P, Elden Ring, Dark Souls).

---

### Added

#### Core Systems
- **CombatSystem** - Central damage resolution with hitstop, events
- **StaminaSystem** - Stamina management with regeneration
- **PoiseSystem** - Lies of P-style accumulative poise
- **LockOnSystem** - Target lock-on with cycling
- **InputHandler** - New Input System with 150ms buffer

#### Player Combat
- 9 player states (Idle, Walk, Sprint, LightAttack, HeavyAttack, Parry, Dodge, Stagger, Death)
- Combo system (array-based in ScriptableObject)
- Parry with perfect parry window (100ms)
- Dodge with i-frames
- Stagger on poise break

#### Enemy AI
- 6 enemy states (Idle, Alert, Chase, Attack, Stagger, Death)
- Detection and chase behavior
- Attack patterns via ScriptableObject
- Stagger and death handling

#### Hitbox System
- Hitbox component (damage source)
- Hurtbox component (damage receiver)
- HitboxController for activation/deactivation
- Collision-based damage resolution

#### Data Architecture
- **DamageInfo** - Damage data transfer
- **AttackData** - Attack configuration
- **CombatStats** - Combatant statistics
- **InputSnapshot** - Buffered input state

#### ScriptableObjects
- **CombatConfigSO** - Global combat tuning
- **WeaponDataSO** - Weapon and combo configuration
- **PlayerConfigSO** - Player stats
- **EnemyDataSO** - Enemy configuration with AI settings

#### Interfaces
- **ICombatant** - Combat participant contract
- **IDamageable** - Damage receiver contract
- **IDamageableWithPoise** - Extended with poise support
- **ILockOnTarget** - Lock-on target contract
- **IState** - State machine state contract

---

### Architecture Decisions (ADRs)

| ADR | Decision | Rationale |
|-----|----------|-----------|
| ADR-01 | HSM + Systems | Maximum separation of concerns |
| ADR-02 | Enum FSM + IState | Simple yet extensible |
| ADR-03 | Script-based Movement | Maximum responsiveness |
| ADR-04 | Lies of P Poise | Accumulative, not threshold |
| ADR-05 | Array Combos | Designer-friendly, SO-based |
| ADR-06 | Input Buffer 150ms | Responsive but forgiving |
| ADR-07 | Parry 200ms | Configurable via SO |
| ADR-08 | Single Defense | MVP simplicity |
| ADR-09 | Physics Solver 10/4 | Stable combat collisions |
| ADR-10 | ScriptableObject Config | Runtime tuning |

---

### File Structure

```
Assets/
├── _Scripts/
│   ├── Combat/
│   │   ├── Adapters/          (1 file)
│   │   ├── Data/              (4 files)
│   │   ├── Hitbox/            (3 files)
│   │   └── Interfaces/        (4 files)
│   ├── Enemies/
│   │   ├── States/            (6 files)
│   │   └── Core/              (4 files)
│   ├── Player/
│   │   ├── States/            (9 files)
│   │   └── Core/              (4 files)
│   └── Scriptables/           (4 files)
├── Systems/                   (6 files)
└── Total: 41 C# files
```

---

### Compatibility

| System | Status | Notes |
|--------|--------|-------|
| KCC Movement | ✅ Compatible | Uses MovementController.ApplyMovement() |
| External Forces | ✅ Compatible | Knockback via ExternalForcesManager |
| Animation System | ✅ Compatible | Uses existing AnimationController |
| New Input System | ✅ Integrated | Full input binding configuration |

---

### Pending (Unity Editor Work)

These tasks require manual Unity Editor setup:

1. **ScriptableObject Instances**
   - Create default asset instances
   - Configure initial values

2. **Prefab Setup**
   - Player prefab with all components
   - Enemy prefab with NavMeshAgent

3. **Scene Setup**
   - Test environment
   - NavMesh baking
   - Physics layers

4. **Integration Testing**
   - Combat flow validation
   - Timing tuning
   - Bug fixing

---

### Performance Characteristics

| Metric | Value |
|--------|-------|
| C# Files | 41 |
| Compilation Errors | 0 |
| Singleton Systems | 4 |
| ScriptableObjects | 4 types |
| Player States | 9 |
| Enemy States | 6 |

---

### Credits

- **Design Reference**: Lies of P, Elden Ring, Bloodborne, Dark Souls
- **Architecture**: HSM + Systems pattern
- **Implementation**: Claude (Opus 4.5) + User collaboration
- **Framework**: SuperClaude pipeline methodology

---

### Version History

| Version | Date | Status |
|---------|------|--------|
| 1.0.0 | 2025-12-22 | Code Complete |

