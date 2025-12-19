# Constraints: Base Movement Redesign

## Technical Constraints

### TC1: Unity Version
- Progetto Unity esistente
- CharacterController (non Rigidbody)
- Input System (new)

### TC2: Existing Architecture
- State Machine pattern già implementato
- MovementController già separato
- AnimationController esistente
- Non modificare lock-on system

### TC3: Animation Constraints
- Animazioni Mixamo (humanoid rig)
- Root motion deve essere configurabile per animazione
- Blend tree per locomotion

## Design Constraints

### DC1: Forward-Based Movement
- Il personaggio DEVE muoversi sempre in avanti
- Non è un sistema twin-stick o strafe-based quando unlocked
- La direzione è determinata dall'input WASD relativo alla camera

### DC2: Responsiveness Priority
- 80% responsive / 20% realistico
- Il giocatore deve sentirsi in controllo
- L'inerzia non deve frustrare, deve aggiungere peso

### DC3: Combat Compatibility
- Il movimento deve permettere reazione rapida per combat
- Non deve impedire dodge/attack durante la deceleration
- Gli stati di combat hanno priorità sulla locomotion

## Resource Constraints

### RC1: Animation Budget
- Mixamo fornisce animazioni
- Richiede setup animator manuale
- Blend tree complexity: moderate (non 8-directional completo)

### RC2: Code Complexity
- Mantenere codice leggibile e manutenibile
- Preferire soluzioni semplici che funzionano
- Non over-engineer con sistemi troppo complessi

## Time Constraints

### TIME1: Scope
- Solo movimento base (no jump, climb, etc.)
- Iterativo: prima funziona, poi si affina
- Si può espandere in futuro
