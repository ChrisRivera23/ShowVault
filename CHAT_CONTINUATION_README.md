# ShowVault active continuation handoff

Read this file completely at the start of a new ShowVault development chat. It is the concise authority for the current state and next task.

Do not automatically read `README.md` or other long records in full. Use the reference map below to inspect only the sections relevant to the active task. Repository contracts, ADRs, tests, migrations, commits, and implementation remain authoritative when they are more specific than this summary.

## Product goal

ShowVault is a recovery-first venue-resilience platform:

**Scan → Backup → Verify → Restore**

It must recognize supported venue equipment and software at an unknown venue without pre-entered paths, addresses, models, vendors, or computer specifications. Preserve documented older macOS/Windows compatibility and direct laptop-to-device Ethernet as product boundaries.

## Current repository state

- Repository: `/Users/infamous/Documents/ChatGPT/showvault`
- Branch: `codex/isadora-catalog-detection`
- Latest completed feature: `9a676c0 feat: detect documented Isadora 4 applications`
- Latest product handoff: the HEAD documentation commit containing this file
- Expected worktree: clean except intentionally untracked `NEXT_CONVERSATION.md`
- Verified baseline: 2 contract tests, 21 platform tests, 305 Agent tests, 7 API tests, Flutter analysis, and 15 Flutter tests passing
- Agent Release build passes and EF Core reports no pending model changes
- Repository-wide Agent formatting has four pre-existing whitespace findings in unchanged `AgentCommandExecutorTests.cs` at lines 985, 991, 997, and 1003

## Current discovery position

- Local application discovery is catalog-driven and checks candidate existence only.
- Resolved paths remain Agent-local. The control plane receives opaque candidate IDs and bounded product/type/evidence metadata.
- Installed, recoverable-data, approved, validated, protected, verified, and restored states remain distinct.
- Existing local catalog coverage includes Resolume, supported DJ applications, disguise Designer, WATCHOUT, Hippotizer, PIXERA, Christie Pandoras Box, TouchDesigner, MadMapper 6, Isadora 4, and bounded Engine OS removable roots.
- HeavyM, Millumin, and Ventuz automatic detection are deferred because official primary sources do not publish dependable standard application/project roots.
- Real venue hardware, installed applications, projects, and removable media remain uninspected unless the Product Owner explicitly authorizes testing.
- `docs/INTEGRATION_CATALOG.md` is the authoritative first-prototype testing matrix.

## Next bounded objective

Research official Ventana primary sources. Add catalog-driven installed-application and project-root detection only if those sources document dependable stable standard locations. Use synthetic platform-shaped fixtures. If dependable paths are not documented, record an evidence-backed deferral instead of guessing.

Required boundaries:

- Extend the reusable catalog/provider architecture; do not add a product-specific host scanner.
- Check only declared standard paths and candidate existence; do not read candidate contents.
- Keep paths Agent-local and publish only opaque, bounded metadata.
- Do not claim Ventana validation, backup, verification, restore, hardware identification, or media support in this detection-only slice.

## Required workflow

1. Inspect Git status, recent commits, and task-relevant code, tests, contracts, migrations, and documentation.
2. Preserve unrelated changes and untracked files.
3. Announce one bounded outcome before changing files.
4. Research official primary sources autonomously; use Chrome only when the Product Owner or current task explicitly requires Chrome.
5. Create a new `codex/` branch for the slice.
6. Implement the smallest safe vertical slice or document the evidence-backed deferral.
7. Run focused verification and the relevant regression baseline. Avoid noisy output when a quiet equivalent is sufficient.
8. Review the final diff for privacy, bounded behavior, tenant isolation, and accidental file-content or network access.
9. Commit the feature or research decision separately.
10. Update this file with current branch, commits, verification, limitations, and the exact next task; commit that handoff separately.

## Communication and context policy

- Give concise progress updates only at meaningful milestones, each with an explicit next step.
- Exact context-window usage is unavailable. Do not invent escalating percentages or end a healthy chat solely because of an estimate.
- In the final response, state that context is unmeasured and recommend a new chat only when the platform signals pressure, automatic compaction has materially degraded working state, or the thread has become demonstrably unwieldy.
- Let automatic compaction preserve continuity when it occurs. Refresh this handoff once per completed bounded task, not repeatedly at guessed thresholds.
- Final responses must report outcome, safety boundaries, exact verification, feature/research and handoff commits, limitations, next task, and intentionally untouched files.

## Reference map

Consult these only as needed:

- `README.md` — long-form product architecture, completed milestones, and historical branch ledger.
- `docs/AUTOMATIC_DISCOVERY.md` — detailed discovery design, protocol behavior, and official-source decisions.
- `docs/INTEGRATION_CATALOG.md` — authoritative prototype product/testing matrix.
- `docs/adr/` — approved architectural decisions.
- `services/contracts/` — Agent/control-plane protocol authority.
- Relevant implementation, tests, migrations, and Git history — most specific behavioral authority.
