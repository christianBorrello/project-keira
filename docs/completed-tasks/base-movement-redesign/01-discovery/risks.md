# Risks: Base Movement Redesign

## Technical Risks

### RISK1: Root Motion Blending Complexity
**Probability**: Medium | **Impact**: High
**Description**: Blendare tra root motion e script-driven movement può causare glitch visivi o comportamenti inaspettati.
**Mitigation**:
- Usare Animator.applyRootMotion toggle dinamico
- Testare transizioni edge case
- Avere fallback a full script-driven se necessario

### RISK2: Animation Mismatch
**Probability**: Medium | **Impact**: Medium
**Description**: Le animazioni Mixamo potrebbero non matchare perfettamente le velocità/tempi del codice.
**Mitigation**:
- Usare animation speed multiplier
- Configurare foot IK se necessario
- Ajustare curve di accelerazione per matchare animazioni

### RISK3: Lock-On Regression
**Probability**: Low | **Impact**: High
**Description**: Modifiche al MovementController potrebbero rompere il movimento lock-on.
**Mitigation**:
- Non toccare codice lock-on esistente
- Testare lock-on dopo ogni modifica significativa
- Avere branch separation chiara

## Design Risks

### RISK4: Feel Subjettivo
**Probability**: High | **Impact**: Medium
**Description**: "Peso" e "responsività" sono soggettivi, difficile da quantificare.
**Mitigation**:
- Iterazione rapida con feedback utente
- Parametri configurabili (ScriptableObject)
- Test playtesting frequente

### RISK5: Over-Engineering
**Probability**: Medium | **Impact**: Medium
**Description**: Tentazione di creare sistema troppo complesso (motion matching, procedural, etc.)
**Mitigation**:
- KISS principle
- MVP first, poi espansione
- Seguire GAMEDEV_CODING_PATTERNS.md

## Integration Risks

### RISK6: State Machine Conflicts
**Probability**: Low | **Impact**: Medium
**Description**: Nuovi stati (turn-in-place, pivot) potrebbero confliggere con stati esistenti.
**Mitigation**:
- Gestire transizioni nell'MovementController, non come stati separati
- Mantenerlo come "substates" interni al movement

## Contingency Plans

| Risk | Trigger | Contingency |
|------|---------|-------------|
| RISK1 | Root motion glitches | Disabilita root motion, usa full script-driven |
| RISK2 | Animation desync | Ajusta speed multiplier, accetta leggero desync |
| RISK3 | Lock-on broken | Revert changes, separate logic paths |
| RISK4 | Feel wrong | Iterate con parametri configurabili |
| RISK5 | Too complex | Simplify, remove features |
| RISK6 | State conflicts | Merge into existing states |
