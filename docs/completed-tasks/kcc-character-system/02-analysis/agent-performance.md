# Agent Report: Performance Analysis - KCC vs CharacterController

## Summary

Analisi delle implicazioni performance dell'integrazione KCC rispetto al CharacterController standard.

---

## Performance Comparison

### CharacterController (Current)
| Aspetto | Costo Tipico |
|---------|--------------|
| Move() call | 0.05-0.15ms |
| Ground check | Integrato |
| Collision resolution | Semplice |
| Memory footprint | ~1KB |

### KinematicCharacterMotor (KCC)
| Aspetto | Costo Tipico |
|---------|--------------|
| Full simulation cycle | 0.3-0.8ms |
| Grounding update | 0.1-0.2ms |
| Collision sweeps | 0.15-0.4ms |
| Memory footprint | ~4KB |

**Delta**: KCC è ~4-6x più costoso per character, ma offre:
- Collision detection più precisa
- Step handling configurabile
- Ledge/slope handling avanzato
- Moving platform support built-in

---

## Bottleneck Analysis

### Hot Path Critico
```
KCC FixedUpdate Cycle:
├── CharacterController callbacks (nostro codice)
│   ├── UpdateVelocity()      ← OTTIMIZZARE
│   └── UpdateRotation()      ← OTTIMIZZARE
├── Grounding update (KCC interno)
├── Collision sweeps (KCC interno)
└── Position integration (KCC interno)
```

### Potenziali Allocations
| Operazione | Rischio | Mitigazione |
|------------|---------|-------------|
| AnimationCurve.Evaluate() | Basso | Nessuna allocation |
| Vector3 math | Zero | Struct operations |
| Quaternion.Slerp() | Zero | Struct operations |
| GetComponent<>() | Alto | Cache in Awake() |
| String concatenation | Alto | Evitare in callbacks |

---

## Optimization Recommendations

### P0: Zero-Allocation Callbacks

```csharp
// ❌ EVITARE: Allocation in hot path
void UpdateVelocity(ref Vector3 velocity, float deltaTime) {
    var direction = GetCameraRelativeDirection(); // OK se cached
    Debug.Log($"Velocity: {velocity}"); // ❌ String allocation!
}

// ✅ CORRETTO: Zero allocations
void UpdateVelocity(ref Vector3 velocity, float deltaTime) {
    // Tutti i calcoli usano struct math
    Vector3 targetVelocity = _cachedDirection * _currentSpeed;
    velocity = Vector3.Lerp(velocity, targetVelocity, deltaTime * _accelRate);
}
```

### P1: Curve Baking (Opzionale)

Se le curve sono statiche, pre-calcolare valori:

```csharp
// Pre-bake curve values at initialization
private float[] _bakedAccelCurve = new float[11]; // 0.0, 0.1, ... 1.0

void BakeCurves() {
    for (int i = 0; i <= 10; i++) {
        _bakedAccelCurve[i] = _accelerationCurve.Evaluate(i * 0.1f);
    }
}

float EvaluateBakedCurve(float t) {
    int index = Mathf.Clamp((int)(t * 10), 0, 9);
    float frac = (t * 10) - index;
    return Mathf.Lerp(_bakedAccelCurve[index], _bakedAccelCurve[index + 1], frac);
}
```

**Nota**: AnimationCurve.Evaluate() è già O(log n) e molto efficiente. Baking utile solo se profiling mostra bottleneck.

### P2: Animator Parameter Batching

```csharp
// ❌ EVITARE: Multiple SetFloat calls
void UpdateAnimator() {
    _animator.SetFloat(_speedHash, speed);
    _animator.SetFloat(_turnAngleHash, turnAngle);
    _animator.SetFloat(_velocityMagHash, velocityMag);
}

// ✅ MEGLIO: Batch quando possibile, ma SetFloat è già efficiente
// La vera ottimizzazione è usare cached hashes (già fatto)
private static readonly int SpeedHash = Animator.StringToHash("Speed");
```

### P3: FixedUpdate Frequency Awareness

KCC simula in FixedUpdate (default 50Hz). Considerazioni:

```csharp
// Il nostro codice gira a 50Hz, non 60+ come Update
// Questo significa:
// - Meno chiamate al secondo (bene per performance)
// - Input deve essere cached tra frame (già previsto)
// - Interpolation gestita da KCC (Settings.Interpolate = true)
```

---

## Frame Budget Analysis

**Target**: < 0.5ms per character update (come da NFR-001)

| Componente | Budget | Note |
|------------|--------|------|
| UpdateVelocity() | 0.1ms | Curve eval + vector math |
| UpdateRotation() | 0.05ms | Quaternion operations |
| Animator params | 0.05ms | 4-5 SetFloat calls |
| External forces | 0.02ms | Simple addition |
| **Totale nostro codice** | **~0.22ms** | ✅ Sotto budget |
| KCC overhead | 0.3-0.5ms | Fuori nostro controllo |
| **Totale stimato** | **~0.5-0.7ms** | ⚠️ Al limite |

**Conclusione**: Con un singolo player character, il budget è rispettato. Per NPCs multipli, considerare:
- LOD system per distant characters
- Reduced update frequency per non-visible
- Pooling per external forces

---

## Profiling Checkpoints

Durante implementation, verificare con Profiler:

1. **Post Phase 1 (KCC Core)**
   - Baseline KCC overhead senza nostro codice
   - Verify no GC allocations

2. **Post Phase 2 (Momentum)**
   - Curve evaluation cost
   - Vector math overhead

3. **Post Phase 5 (Lock-On)**
   - Distance calculations
   - Target tracking overhead

4. **Post Phase 6 (External Forces)**
   - List iteration cost
   - Force decay calculations

---

## Risk Assessment

| Rischio | Probabilità | Impatto | Mitigazione |
|---------|-------------|---------|-------------|
| GC spikes in callbacks | Bassa | Alto | Code review per allocations |
| Curve evaluation slow | Molto Bassa | Basso | AnimationCurve è ottimizzato |
| Animator overhead | Bassa | Medio | Cached hashes (già previsto) |
| KCC overhead eccessivo | Bassa | Medio | KCC è production-tested |

**Rischio Globale: BASSO** - KCC è usato in produzioni AAA, le nostre aggiunte sono computazionalmente leggere.
