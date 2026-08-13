# ShowVault active continuation handoff

Read this file, `docs/LOCAL_FIRST_PRODUCT_BIBLE.md`,
`docs/CONTROLLED_LOCAL_RESTORE.md`, and
`docs/LOCAL_FIRST_MILESTONE_3_IMPLEMENTATION_2026-08-13.md` completely before
continuing work from this branch.

## Current checkpoint — 2026-08-13

- Branch: `codex/local-first-milestone-3`
- Worktree: `/private/tmp/showvault-local-first-m3-plan/worktree`
- Exact authorized planning foundation:
  `2d6c3d2241b582678a6c475fffd88a3f2fa940a7`
- Source implementation head:
  `88a9c5bbbf2ac0fbce32811fb6c5dd6f0ff72b8b`
- Documentation/evidence commit:
  `e915d8b39bad2545b3abe99db2124c2c93e88f5a`
- Product outcome:
  **Open local vault → select a verified point → Restore or Cancel → verify the
  restored copy → retain path-free local evidence**

Milestone 3 is complete locally. Restore remains signed-out/offline and
freshly verified-point-only. The packaged .NET engine retains package and
selected-sandbox identities, stages and hashes exact content, publishes only
the fixed `ShowVault Restored Files` child, reverifies it, and commits durable
path-free SQLite/evidence state before returning Restored locally. Cancellation
is honored before publication; exact-owned rollback and reselect repair
preserve every unknown or ambiguous entry as Restore attention.

Flutter owns only the warning, independent native target picker, progress,
Cancel, Restored locally, and Restore attention surfaces. The host accepts only
Save, inspect, Restore, and in-process Cancel records. It exposes no network,
upload, arbitrary output, Agent identity/enrollment/command/service lifecycle,
application/device loading, or Recovery Confidence behavior.

Validation passed: local engine 60, including synthetic packaged-host Restore;
Flutter 26; Agent 291; contracts 22; platform 15; API 19; EF model gate;
zero-warning local-host, Agent, and API Release builds; format, plugin drift,
project/plist, shell, packaging-guard, path-leak, and complete-diff checks.

## Authorization boundary

No external product-system or native action is authorized by this checkpoint.
Do not fetch or push Git state, create or mutate a PR, dispatch a workflow,
retrieve artifacts, build or install a meaningful native package, use
equipment, access personal/customer/venue data, use cloud resources,
upload/synchronize, release, deploy, or clean up destructively without new
explicit authorization.

No native-platform proof is claimed. macOS/Windows Flutter build, signing,
sandbox/helper behavior, notarization, installation, upgrades, protocol
activation, Gatekeeper, personal-Keychain, privileged mount/reparse behavior,
equipment, and live application/device loading remain unproven.

## Next gated decision

Stop for Product Owner direction. Per the ordered roadmap, the likely next
bounded slice is hosted synchronization and account/billing administration,
but no extraction, design, implementation, cloud action, or external-system
mutation is pre-authorized here. Before implementation, select one exact
outcome, account for its historical source, write its current security/data
contract, and obtain explicit authorization.

The existing untracked `NEXT_CONVERSATION.md` in the user's primary worktree
is outside this branch and was not added or changed.
