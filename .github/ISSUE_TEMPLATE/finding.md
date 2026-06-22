---
name: Finding (engine / parser / narrator)
about: Record a bug AdventureBreaker found. Verify against engine source first.
title: "[finding] "
labels: finding
---

<!--
  Keep the ledger credible: VERIFY against ZorkAI source (and original ZIL when available)
  and confirm against current origin/main BEFORE filing — prod often lags main. See
  CONTRIBUTING.md → "Logging a finding".
-->

**Game / target:** <!-- zork | planetfall · prod | local -->
**Area / mechanic:** <!-- location name or MECH:* -->
**Category:** <!-- contract | consistency | narrator-hallucination | parser-pronoun | ... -->
**Severity:** <!-- info | low | medium | high | critical -->
**Durable id (if recorded):** <!-- AB-NNN -->
**Target SHA verified against:** <!-- ZorkAI origin/main short SHA -->

### What happened

<!-- The command(s) and the observed response. Paste prose + structured envelope. -->

### Why it's a bug

<!-- The expected behavior and the source/ZIL reference that establishes it. -->

### Reproduction

```
# deterministic repro (use `quiet` to remove narrator randomness where possible)
```

### Narrator A/B (if a narrator bug)

- narrator OFF result:
- narrator ON result:
