# Requirements: Base Movement Redesign

## Problem Statement

Il sistema di movimento attuale manca di **peso e inerzia**. Il personaggio sembra fluttuare, non ha la sensazione fisica di un corpo con massa.

## Desired Feel

**80% Responsive / 20% Realistico** (stile Dark Souls 3)
- Priorità alla responsività per il combattimento
- Leggero senso di peso senza sacrificare il controllo
- Non pesante come Red Dead 2, non arcade come giochi mobile

## Functional Requirements

### FR1: Movement Direction
- Il personaggio si muove SEMPRE in avanti (forward) verso la direzione risultante dall'input WASD
- La camera definisce il riferimento per la direzione
- Non è un sistema strafe quando non in lock-on

### FR2: Acceleration & Deceleration
- **Acceleration curve**: Ease-in quando il personaggio parte da fermo
- **Deceleration curve**: Ease-out quando il personaggio si ferma
- Il personaggio non deve partire/fermarsi istantaneamente

### FR3: Momentum/Inertia
- Il movimento deve avere inerzia
- Cambiare direzione deve richiedere un minimo "effort" percepibile
- L'input deve "guidare" il movimento, non comandarlo istantaneamente

### FR4: Pivot System (Soft Pivot)
- Quando cambi direzione di un angolo significativo (>90°):
  - Rallenta progressivamente
  - Curva dolcemente verso la nuova direzione
  - Non stop-and-turn completo, ma smooth transition
- Per angoli minori: smooth rotation durante il movimento

### FR5: Root Motion (Parziale)
- **Root Motion ON**: Turn-in-place animations, pivot animations
- **Root Motion OFF**: Locomotion standard (codice guida velocità)
- L'animator deve blendare seamlessly tra i due

### FR6: Locomotion Modes (Unchanged)
- Walk: lento (Control)
- Run: default
- Sprint: veloce (Shift)
- Le velocità relative devono essere preservate o ribilanciate

## Non-Functional Requirements

### NFR1: Performance
- Nessun impatto significativo su FPS
- Nessuna allocazione in hot path (Update/FixedUpdate)

### NFR2: Code Quality
- Seguire GAMEDEV_CODING_PATTERNS.md
- Single Responsibility: MovementController gestisce solo movimento
- Configurazione tramite ScriptableObject o SerializeField

### NFR3: Animation Requirements
Le animazioni saranno prese da Mixamo. Necessarie:
- Idle
- Walk forward
- Run forward
- Sprint forward
- Turn-in-place 90° left/right
- Turn-in-place 180°
- Start locomotion (da fermo a movimento)
- Stop locomotion (da movimento a fermo)
- Opzionale: Pivot durante movimento

### NFR4: Compatibility
- Deve integrarsi con il sistema di lock-on esistente (non modificarlo)
- Deve integrarsi con gli stati esistenti (Idle, Walk, Run, Sprint)
- Deve mantenere compatibilità con sistema di combattimento

## Out of Scope

- Sistema di lock-on (già soddisfacente)
- Sistema di combattimento
- Sistema di dodge/roll
- Jump system
- Climbing system

## Acceptance Criteria

- [ ] AC1: Partenza da fermo ha acceleration visibile (non istantanea)
- [ ] AC2: Stop da corsa ha deceleration visibile (non istantaneo)
- [ ] AC3: Cambio direzione >90° rallenta il personaggio
- [ ] AC4: Turn-in-place funziona quando fermo e si input direzione diversa
- [ ] AC5: Il movimento "sente" peso senza essere sluggish
- [ ] AC6: Responsività adeguata per combattimento (80% responsive)
- [ ] AC7: Animator usa blend tree con transizioni smooth
- [ ] AC8: No regression su lock-on movement
