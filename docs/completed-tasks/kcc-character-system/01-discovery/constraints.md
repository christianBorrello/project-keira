# Constraints: KCC Character System

## Technical Constraints

### TC-001: KCC Architecture Pattern
**Type**: Architectural

KCC usa un pattern **pull-based** (callback) invece del pattern **push-based** (chiamata diretta) usato dal CharacterController standard.

**Implicazione**:
- Non possiamo chiamare `Move()` direttamente
- Dobbiamo implementare `UpdateVelocity(ref Vector3)` e `UpdateRotation(ref Quaternion)`
- La logica di movimento deve essere adattata al modello callback

### TC-002: Simulation Timing
**Type**: Technical

KCC simula in `FixedUpdate` gestito da `KinematicCharacterSystem`.

**Implicazione**:
- Input deve essere cached tra frame
- State machine chiama `SetMovementIntent()`, non applica direttamente
- Interpolation gestita da KCC (Settings.Interpolate)

### TC-003: Existing Combat System Integration
**Type**: Integration

Il combat system esistente (HealthPoiseController, stagger, parry) deve continuare a funzionare.

**Implicazione**:
- External forces devono integrarsi con stagger
- IsGrounded deve rimanere accessibile per combat states
- Animation events devono continuare a funzionare

### TC-004: KCC File Size
**Type**: Technical

`KinematicCharacterMotor.cs` è ~30k tokens, troppo grande per lettura completa.

**Implicazione**:
- Lavorare con l'interfaccia `ICharacterController` (piccola e chiara)
- Consultare documentazione KCC per dettagli interni
- Usare grep/search per sezioni specifiche quando necessario

---

## Design Constraints

### DC-001: Preserve Existing Feel
**Type**: Design

Il sistema momentum già implementato (80/20 responsive/realistico) deve essere preservato.

**Implicazione**:
- Le curve di accelerazione/decelerazione devono produrre lo stesso feel
- I valori di soglia (pivot, turn-in-place) devono rimanere configurabili

### DC-002: Lock-On Unchanged
**Type**: Design

Il sistema di lock-on (orbiting, facing target) è già soddisfacente.

**Implicazione**:
- Portare la logica esistente, non ridisegnarla
- Mantenere compatibilità con LockOnController

### DC-003: KCC Default Terrain Handling
**Type**: Design (User Choice)

Usare i settings KCC standard per terreni e pendenze.

**Implicazione**:
- Non implementare custom sliding
- Configurare: MaxStableSlopeAngle, StepHandling, LedgeHandling
- Lasciare a KCC la gestione ground detection

---

## Resource Constraints

### RC-001: Development Approach
**Type**: Process

Approccio incrementale: una feature alla volta con validazione.

**Implicazione**:
- Ogni fase deve essere testabile indipendentemente
- Commit atomici per fase
- Quality gate prima di procedere

### RC-002: Reference Code Available
**Type**: Resource

Il codice di `base-movement-redesign` è disponibile come reference.

**Implicazione**:
- Estrarre logica da MovementController esistente
- Adattare al pattern KCC callback
- Non copiare/incollare ma comprendere e reimplementare
