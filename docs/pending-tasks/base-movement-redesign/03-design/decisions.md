# Architecture Decision Records (ADR)

## ADR-001: Momentum System Approach

**Status**: Accepted
**Date**: 2025-12-19
**Decision Makers**: Claude AI, User

### Context
Il sistema di movimento attuale usa SmoothDamp lineare che non dà sensazione di peso/inerzia. Serve un sistema che aggiunga "feel" senza sacrificare responsività (80/20 balance).

### Options Considered

| Option | Pros | Cons |
|--------|------|------|
| A: Velocity Accumulation | Fisicamente accurato, full control | Richiede molto tuning, più codice |
| B: AnimationCurve Driven | Designer-friendly, facile tweaking | Meno preciso fisicamente |
| C: State-Based Substates | Massimo controllo, clear separation | Over-engineering, complessità |

### Decision
**Option B: AnimationCurve Driven** con elementi di A per controllo fine.

### Rationale
- AnimationCurve è modificabile in Unity Editor senza ricompilare
- Designer può iterare rapidamente sul feel
- Sufficientemente preciso per 80% responsive target
- Meno codice = meno bug potenziali
- Segue KISS principle da GAMEDEV_CODING_PATTERNS.md

### Consequences
- Acceleration/deceleration controllate da curve
- Velocity tracking semplificato (magnitude only)
- Tuning via Inspector, non codice

---

## ADR-002: Soft Pivot via Speed Modulation

**Status**: Accepted
**Date**: 2025-12-19

### Context
Quando il giocatore cambia direzione significativamente (>90°), serve un feedback visivo di "effort" senza creare uno stop completo.

### Options Considered

| Option | Pros | Cons |
|--------|------|------|
| A: Stato Pivot separato | Clear separation, animazione dedicata | Sluggish, interrompe flow |
| B: Speed modulation | Smooth, responsive, no interruption | Meno visivamente drammatico |
| C: Leaning animation layer | Visivamente ricco | Richiede animazioni extra |

### Decision
**Option B: Speed Modulation** come base, con possibile C come enhancement futuro.

### Rationale
- 80% responsive richiede continuità nel movimento
- Speed reduction è sufficiente per dare sensazione di pivot
- Il blend tree gestisce naturalmente la transizione animazione
- Non richiede animazioni aggiuntive per MVP
- Può essere enhanced in futuro con leaning layer

### Consequences
- Pivot è un modificatore di velocità, non uno stato
- pivotFactor = f(turnAngle) applicato alla velocità
- Rotazione accelerata per compensare durante pivot
- Visivamente: rallentamento + curva fluida

---

## ADR-003: Turn-In-Place con Root Motion Parziale

**Status**: Accepted
**Date**: 2025-12-19

### Context
Quando il giocatore è fermo e input in direzione diversa, serve una transizione visiva credibile.

### Options Considered

| Option | Pros | Cons |
|--------|------|------|
| A: Rotazione script-only | Massima responsività | Sembra artificiale |
| B: Full root motion | Massimo realismo | Può sentirsi sluggish |
| C: Hybrid (script + partial root) | Balance realismo/control | Più complesso |

### Decision
**Option C: Hybrid** con root motion solo per rotazione, position da script.

### Rationale
- La rotazione da root motion dà weight visivo
- La position rimane sotto controllo del codice
- Turn-in-place dura solo 0.3-0.5s, impatto minimo su responsività
- Può essere interrotto (combat priority)

### Implementation
```csharp
void OnAnimatorMove() {
    if (isTurningInPlace) {
        // Apply rotation from animation
        transform.rotation = animator.rootRotation;
        // Ignore position (or apply scaled)
    }
}
```

### Consequences
- Animazioni turn devono avere root motion rotation
- MovementController gestisce flag isTurningInPlace
- Turn può essere interrotto da combat actions

---

## ADR-004: Lock-On Path Preservation

**Status**: Accepted
**Date**: 2025-12-19

### Context
Il sistema di lock-on è già soddisfacente e non deve essere toccato.

### Decision
**Branch preservation**: tutto il codice lock-on rimane in un branch `if (isLockedOn)` separato.

### Implementation
```csharp
public void ApplyMovement(Vector2 input, LocomotionMode mode) {
    if (_lockOnController?.IsLockedOn ?? false) {
        // EXISTING LOCK-ON LOGIC (unchanged)
        ApplyLockedOnMovement(input, mode);
        return;
    }

    // NEW MOMENTUM-BASED LOGIC
    ApplyMomentumMovement(input, mode);
}
```

### Consequences
- Nessun rischio di regression su lock-on
- Duplicazione minima (solo il branch)
- Lock-on può evolversi indipendentemente in futuro

---

## ADR-005: Animator Parameter Strategy

**Status**: Accepted
**Date**: 2025-12-19

### Context
L'Animator attuale ha Speed, MoveX, MoveY, IsLockedOn. Servono parametri aggiuntivi per turn detection.

### Decision
**Additive parameters**: aggiungere TurnAngle e VelocityMagnitude senza modificare esistenti.

### New Parameters
| Parameter | Purpose |
|-----------|---------|
| TurnAngle | Trigger turn-in-place states (-180 to 180) |
| VelocityMagnitude | Transition conditions, actual speed |

### Rationale
- Non breaking: parametri esistenti non toccati
- Turn detection gestibile via transitions
- VelocityMagnitude utile per start/stop animations (future)

### Consequences
- AnimationController.SetTurnAngle() nuovo metodo
- AnimationController.SetVelocityMagnitude() nuovo metodo
- Animator transitions basate su nuovi parametri
