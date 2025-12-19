# Project Keira

Unity 3D Action RPG featuring a Soulslike combat system with event-driven architecture.

## Features

### Combat System
- **Soulslike Combat**: Precise, timing-based combat with attack windows and recovery frames
- **Poise System**: Stagger mechanics based on accumulated damage
- **Stamina Management**: Resource-based action system for attacks, dodges, and blocks
- **Lock-On Targeting**: Dynamic enemy targeting with camera integration

### Event-Driven Architecture
- **ScriptableObject Events**: Decoupled communication between game systems
- **Generic Event System**: `GameEvent<T>` and `GameEventListener<T>` for type-safe events
- **Combat Events**: OnDamageDealt, OnParrySuccess, OnPlayerDeath, OnHealthChanged, OnPoiseBreak

### Player Systems
- **Third-Person Camera**: Cinemachine-based camera with lock-on support
- **Animator Controller**: State machine for grounded, airborne, and lock-on movement
- **Health & Poise Controller**: Integrated health and stagger management

### UI Components
- **Enemy Health Bar**: Event-driven health display for targeted enemies

## Project Structure

```
Assets/
├── _Scripts/
│   ├── Camera/           # Third-person camera and lock-on
│   ├── Combat/           # Combat data, hitboxes, adapters
│   ├── Core/Events/      # Generic event system
│   ├── Player/           # Player components and controllers
│   └── UI/               # UI components
├── Systems/              # Core game systems
├── Events/               # ScriptableObject event instances
├── Data/                 # Configuration assets
├── Animations/           # Player animation clips
└── Scenes/               # Game scenes
```

## Core Scripts

### Event System
- `GameEvent.cs` - Base non-generic event
- `GameEvent<T>.cs` - Generic typed event
- `GameEventListener.cs` - Event listener component
- `GameEventListener<T>.cs` - Generic typed listener

### Combat
- `CombatSystem.cs` - Main combat orchestrator
- `HealthPoiseController.cs` - Health and poise management
- `HitboxController.cs` - Attack hitbox management
- `DamageInfo.cs` - Damage data structure

### Systems
- `InputHandler.cs` - Input processing
- `LockOnSystem.cs` - Target lock-on management
- `StaminaSystem.cs` - Stamina resource management
- `PoiseSystem.cs` - Poise/stagger calculations

## Requirements

- Unity 2022.3 LTS or newer
- Universal Render Pipeline (URP)
- Input System Package
- Cinemachine
- TextMesh Pro

## Getting Started

1. Clone the repository
2. Open the project in Unity
3. Open `Assets/Scenes/Main.unity`
4. Press Play

## Architecture

The project follows an **event-driven architecture** using Unity's ScriptableObject system, inspired by Ryan Hipple's GDC talk "Game Architecture with Scriptable Objects".

Benefits:
- **Decoupled Systems**: Components communicate through events, not direct references
- **Editor-Friendly**: Events are assets that can be configured in the Inspector
- **Testable**: Systems can be tested in isolation
- **Extensible**: New listeners can subscribe without modifying existing code

## License

This project is for educational and portfolio purposes.

---

Built with Unity and C#
