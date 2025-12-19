# Phase 1: Discovery - Risks

## Task: Deep Refactoring GameDev Patterns
**Status**: In Progress
**Date**: 2025-12-19

---

## Risk Register

### High Priority Risks

| ID | Risk | Probability | Impact | Mitigation |
|----|------|-------------|--------|------------|
| R1 | **Breaking existing functionality** | Medium | Critical | Incremental changes with git commits after each working change. Test gameplay after every modification. |
| R2 | **State machine behavior change** | Medium | Critical | Document current state transitions before refactoring. Create test cases for each transition. |
| R3 | **Performance regression** | Low | High | Profile before/after each major change. Watch for GC allocations in Update loops. |
| R4 | **Input feel changes** | Medium | High | Preserve exact smoothing values. Test input responsiveness after movement refactoring. |

### Medium Priority Risks

| ID | Risk | Probability | Impact | Mitigation |
|----|------|-------------|--------|------------|
| R5 | **Animation timing breaks** | Medium | Medium | Document current animation event timing. Verify AnimationEventBridge works after decoupling. |
| R6 | **Combat system regression** | Low | Medium | Test hitbox/hurtbox interaction after each change. Verify damage calculations. |
| R7 | **Lock-on system breaks** | Medium | Medium | Test lock-on acquisition and camera behavior after PlayerController decomposition. |
| R8 | **Scene references break** | Medium | Medium | Update prefab references after component restructuring. User accepts this risk (flessibile). |

### Low Priority Risks

| ID | Risk | Probability | Impact | Mitigation |
|----|------|-------------|--------|------------|
| R9 | **Scope creep** | Medium | Low | Strict adherence to requirements.md. Document any additions for future work. |
| R10 | **Over-engineering** | Low | Low | Follow YAGNI principle. Only refactor what's in requirements. |
| R11 | **Merge conflicts** | Low | Low | Single developer on feature branch. Commit frequently. |

---

## Risk Mitigation Strategies

### Strategy 1: Incremental Refactoring
- Make one logical change at a time
- Commit after each successful change
- Test gameplay after each commit
- Easy rollback with `git revert`

### Strategy 2: Behavior Documentation
Before refactoring a component:
1. Document current behavior
2. List all public methods and their callers
3. Note any side effects
4. Create mental test cases

### Strategy 3: Preserve Contracts
- Extract interfaces before changing implementations
- Ensure new components implement same interfaces
- Update only internal implementation, not contracts

### Strategy 4: Performance Monitoring
- Profile baseline before changes
- Profile after each major change
- Watch for:
  - GC allocations in hot paths
  - Frame time increases
  - Memory usage spikes

---

## Risk Monitoring

_Updated during implementation_

| ID | Status | Last Check | Notes |
|----|--------|------------|-------|
| R1 | Active | - | Monitor after each change |
| R2 | Active | - | Document state transitions before FSM work |
| R3 | Active | - | Profile baseline needed |
| R4 | Active | - | Test after movement refactoring |
| R5 | Active | - | Test after AnimationEventBridge work |
| R6 | Active | - | Test after combat-related changes |
| R7 | Active | - | Test after PlayerController decomposition |
| R8 | Accepted | - | User accepts prefab rebuild if needed |
| R9 | Active | - | Review scope regularly |
| R10 | Active | - | Apply YAGNI |
| R11 | Low | - | Single developer |

---

## Rollback Plan

### For Each Phase
1. **Git commit before starting**: `git commit -m "Checkpoint before [phase]"`
2. **Test after changes**: Manual gameplay verification
3. **If issues found**: `git revert` to last known good state
4. **Re-evaluate approach**: Understand why it broke before retrying

### Emergency Rollback
```bash
# Return to last working state
git reset --hard HEAD~N  # N = number of commits to undo

# Or return to specific commit
git reset --hard <commit-hash>
```

### Point of No Return
- After merging to main branch
- After deleting old code without backup
- After updating Unity project settings

---

## Contingency Plans

### If State Machine Refactoring Fails
- Keep separate Player/Enemy implementations
- Document why consolidation didn't work
- Focus on other refactoring goals

### If PlayerController Decomposition Causes Issues
- Start with smaller extractions (e.g., just Health first)
- Keep more logic in PlayerController if needed
- Document what couldn't be extracted and why

### If Performance Degrades
- Profile to find bottleneck
- Revert problematic change
- Find alternative approach
- Consider accepting slightly more LOC for better performance

---

## Context Loading
When loading this phase:
1. Review Active risks before making changes
2. Check Risk Monitoring table for current status
3. Follow Rollback Plan if issues occur
