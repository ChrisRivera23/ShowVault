# Local-first integration execution checklist

## Purpose

This checklist converts the six audited extraction manifests into a single
dependency-ordered integration procedure. It is designed to prevent the
accumulated PR #25 comparison view from becoming an accidental merge strategy.

The checklist itself authorizes no action. Current branch pushes, PR changes,
merges, workflow dispatch, Docker/installed proofs, cloud resources, controlled
Windows equipment, personal data, and venue use remain subject to the active
handoff and explicit Product Owner authorization.

## Two tracks, two decisions

| Track | Outcome | Dependency | What it does not approve |
| --- | --- | --- | --- |
| Product integration | Six reviewable local-first milestones reach `main` in order | PRs #3–#24 are deliberately reviewed/integrated first | Windows runtime/readiness |
| Windows evidence bridge | One default-branch workflow checks out one immutable published green source and returns provenance-bound native evidence | Explicit source push, bridge update/merge, and dispatch approvals | Product-stack integration or attended Windows UX |

The tracks may inform each other but must not be conflated. A passing native run
does not approve PR #25 or any product milestone. A merged product milestone does
not authorize workflow dispatch or controlled-equipment use.

## Authorization legend

- **Local read** — inspect Git objects, code, docs, manifests, and test output.
- **Local implementation** — create a scoped local branch/worktree, edit source,
  run non-destructive local tests/builds, and create local commits when the
  prerequisite base exists and the Product Owner requested that implementation.
- **Scoped local execution approval** — required before installed harnesses,
  disposable Docker stacks/volumes, registry mutation, or other meaningful local
  machine state changes described by a proof run.
- **Remote approval** — required for fetch/push when it changes or relies on
  external state, PR creation/update/retarget/ready state, merge, workflow
  dispatch, artifact download tied to a run, release, or deployment.
- **Equipment approval** — required for controlled Windows or venue equipment.
- **Destructive approval** — required for object-prefix deletion, external-vault
  removal, production rollback, or any cleanup not already marker-scoped to a
  disposable proof.

When authorization is ambiguous, stop before the action and preserve local work.

## Stage 0 — freeze and verify the planning source

- [ ] Worktree is clean except intentionally untracked `NEXT_CONVERSATION.md`.
- [ ] Current branch and ahead/behind state are recorded.
- [ ] `CHAT_CONTINUATION_README.md`,
      `LOCAL_FIRST_INTEGRATION_CONSISTENCY_AUDIT.md`, and all six milestone
      manifests have been read completely.
- [ ] The consistency audit reproduces 52 selected commits, a 136-path union,
      and exactly 29 legacy-overlap paths.
- [ ] The four excluded interleaved commits are exactly `626e88d`, `0c174ba`,
      `a1c3c83`, and `65c50be`.
- [ ] The source references still exist and have not been rewritten.
- [ ] No secret, credential, token, personal path, or venue data is present in
      the planned diff or handoff.

Stop if any count, source object, or boundary differs. Update the consistency
audit before implementation rather than adjusting a milestone ad hoc.

## Stage 1 — integrate the published foundation deliberately

This stage is remote and is not currently authorized by this checklist.

- [ ] Obtain approval to review and integrate PRs #3–#24.
- [ ] Refresh the actual default branch and PR state only after that approval.
- [ ] Process PRs #3–#24 in dependency order.
- [ ] After each predecessor merges, retarget/rebase the next review unit only
      through the approved repository workflow.
- [ ] Reinspect the resulting diff rather than trusting the former stacked diff.
- [ ] Require current CI and mergeability for the resulting base.
- [ ] Record the final integrated `main` SHA used for milestone 1.

Do not merge or squash PR #25. Preserve it as a comparison view until all
required source has been reconstructed and reviewed elsewhere.

## Product-integration sequence

Each milestone starts only after its predecessor is merged to `main`. Suggested
local branch names use the required `codex/` prefix; creation and remote
publication are separate actions.

| Order | Suggested branch | Manifest | Required base |
| ---: | --- | --- | --- |
| 1 | `codex/local-first-m1-direct-scan` | `LOCAL_FIRST_MILESTONE_1_EXTRACTION.md` | integrated PR #24 foundation on `main` |
| 2 | `codex/local-first-m2-offline-save` | `LOCAL_FIRST_MILESTONE_2_EXTRACTION.md` | merged milestone 1 |
| 3 | `codex/local-first-m3-sync-restore` | `LOCAL_FIRST_MILESTONE_3_EXTRACTION.md` | merged milestone 2 |
| 4 | `codex/local-first-m4-object-storage` | `LOCAL_FIRST_MILESTONE_4_EXTRACTION.md` | merged milestone 3 |
| 5 | `codex/local-first-m5-resilience-diagnostics` | `LOCAL_FIRST_MILESTONE_5_EXTRACTION.md` | merged milestone 4 |
| 6 | `codex/local-first-m6-windows-evidence` | `LOCAL_FIRST_MILESTONE_6_EXTRACTION.md` | merged milestone 5 |

Do not develop milestones concurrently against stale bases. The source manifests
can be reviewed in parallel, but implementation is dependency-ordered because
the same dashboard, API startup, recovery services, tests, and runbooks evolve
across milestones.

## Per-milestone implementation loop

Repeat this loop for every milestone:

### A. Base and scope

- [ ] Confirm the exact predecessor is present on the intended local `main`.
- [ ] Confirm no unrelated user changes would be overwritten.
- [ ] Create the scoped local branch/worktree only after the base is valid.
- [ ] Record base SHA, source boundary/selected commits, expected file counts,
      and overlap disposition.
- [ ] Generate a candidate file list from Git; compare it with the manifest.
- [ ] Exclude handoff/evidence history that does not describe reconstructed code.

### B. Reconstruction

- [ ] Implement the manifest's review units as distinct local commits.
- [ ] Reconstruct mixed files by concern; do not apply their full historical
      diff when the manifest says split or regenerate.
- [ ] Regenerate dependency/plugin outputs and EF snapshots from the integrated
      base rather than copying generated files blindly.
- [ ] Preserve local-first behavior, offline operation, tenant authorization,
      path containment, verified-only state transitions, and immutable data.
- [ ] Keep legacy Agent behavior compatibility-only and out of customer setup.
- [ ] Update current product/runbook docs after code behavior is verified.

### C. Focused verification

- [ ] Run every focused test listed in the milestone manifest.
- [ ] Add adversarial coverage for every conflict or deliberate deviation found
      during reconstruction.
- [ ] Run formatting/static analysis for changed languages and workflow files.
- [ ] Resolve warnings and failures; do not reclassify them as limitations merely
      to advance the milestone.

### D. Full gate

- [ ] `flutter analyze` passes.
- [ ] Complete Flutter tests pass, with only explicitly platform-gated skips.
- [ ] Contracts, platform, Agent, and API test projects pass.
- [ ] EF pending-model check reports no pending changes.
- [ ] Relevant release/container/package build passes for that milestone.
- [ ] `git diff --check` passes.
- [ ] Worktree contains only the milestone diff and intentionally untracked
      handoff file.

### E. Security/privacy review

- [ ] No credential, token, secret, personal path, host identity, or content leak.
- [ ] All tenant routes independently authorize organization and venue.
- [ ] Client-controlled values cannot choose local/server storage roots.
- [ ] Paths are canonical, segment-bounded, link-safe, and platform-appropriate.
- [ ] Cloud failure cannot delete or downgrade the only verified local copy.
- [ ] Detection remains distinct from backup, verification, Restore, and
      Recovery Confidence.
- [ ] Synthetic/test command modes are compile-time and environment gated.
- [ ] Cleanup is absent or exact ownership/identity scoped.

### F. Local handoff and remote stop

- [ ] Commit implementation and handoff documentation separately.
- [ ] Record exact local head, tests, builds, evidence, and limitations.
- [ ] Keep `NEXT_CONVERSATION.md` untracked and copy/paste-ready.
- [ ] Stop before push or PR mutation unless that exact action is authorized.
- [ ] After an authorized PR is published, wait for current CI and review before
      requesting separate ready/merge authorization.

## Milestone-specific release gates

| Milestone | Non-negotiable extra gate |
| --- | --- |
| 1 | Empty newer scan supersedes older detections; unknown/path keys and unauthorized roles fail; no Agent onboarding appears |
| 2 | Save publishes only after independent verification; picker access is exact/session-scoped; vault reopens without source rescan; `ddfcaa6` behavior is in `RecoveryPackageWriter` |
| 3 | Queue journal is append-only/resumable; hosted API is tenant-bound; selected-target Restore uses internal owned staging; status refresh is immediate |
| 4 | Production accepts only S3 and fails closed; object keys are tenant-derived/path-hashed/create-only; receipt publishes last; runtime has no delete permission |
| 5 | Normal builds cannot enter harness command modes; diagnostics read metadata only and remain path-free; replacement/removal preserves the external vault |
| 6 | Windows path rules and NTFS junction test pass natively; current-user installer adds no service/Agent; evidence is checksum/provenance/run bound; attended/native claims stay separate |

## Windows evidence bridge sequence

This sequence is independent from the six-PR product integration sequence and
requires explicit authorization at each external stage.

- [ ] Obtain authorization to push the current provenance-protected source.
- [ ] Push only the approved branch/head and wait for all required CI to pass.
- [ ] Select the exact lowercase full published green source SHA.
- [ ] Prepare an absent one-file bridge from the current default branch with
      `prepare_windows_evidence_bridge.dart`.
- [ ] Verify it byte-for-byte with `verify_windows_evidence_bridge.dart`.
- [ ] Confirm digest, exact action pins, manual/read-only policy, immutable
      checkout, and one-file diff locally.
- [ ] Obtain authorization to update/push the bridge PR.
- [ ] Review the actual remote one-file diff and current checks.
- [ ] Obtain separate ready/merge authorization.
- [ ] Confirm the workflow now exists on the default branch at the reviewed
      revision.
- [ ] Obtain separate authorization for exactly one manual dispatch.
- [ ] Wait for completion without changing the run.
- [ ] Run `verify_windows_run.dart` into an absent local directory and review the
      bounded output, provenance, cleanup, checksums, and limitations.
- [ ] Record native headless evidence without claiming attended or clean-machine
      Windows readiness.

Any changed source SHA, workflow revision, action pin, artifact name, run
attempt, or failed/timed-out/cancelled job invalidates the sequence and requires
fresh review before another dispatch.

## Controlled Windows equipment sequence

This path requires separate equipment authorization and does not reuse CI as
attended evidence.

- [ ] Confirm the controlled user has no existing `showvault` callback.
- [ ] Record Windows edition/version/build and architecture without host/user
      identity.
- [ ] Verify PowerShell 7, Flutter Windows, Visual Studio Desktop C++, and Inno
      Setup 6 on the build machine.
- [ ] Run packaging and installed proof into absent local-drive directories.
- [ ] Run full Flutter tests including the NTFS-junction test.
- [ ] Verify installer/portable deployment, callback registration, launch,
      upgrade, uninstall, vault retention, checksums, and actual signature states.
- [ ] Separately perform attended exact-catalog Scan, pickers, offline Save,
      restart/rehydration, Restore, diagnostic, replacement, and Auth0 callback.
- [ ] Remove only the installed app, callback, and owned synthetic workspace;
      retain or remove the synthetic vault only through the exact approved proof
      cleanup.
- [ ] Record failures and limitations without weakening Windows security or
      broadening cleanup.

## Stop conditions

Stop the current stage immediately when any of these occurs:

- source/count/overlap differs from the consistency audit;
- base branch or remote head changes unexpectedly;
- unrelated user work overlaps the planned files;
- generated files or migrations cannot be reproduced;
- a focused or full required check fails;
- a bypass/test mode is reachable outside its exact gates;
- path, secret, token, identity, content, or tenant-isolation leakage appears;
- a local or remote operation would broaden beyond explicit authorization;
- cleanup ownership or exact target cannot be proven;
- a native/evidence artifact is incomplete, unlisted, unsigned contrary to its
  claim, checksum-invalid, provenance-mismatched, or path-bearing; or
- a requested readiness claim exceeds direct evidence.

Preserve bounded diagnostic output and local commits. Do not push a workaround,
rerun a workflow, delete evidence, or mutate external state without the required
new authorization.

## Milestone completion record

For every milestone, record:

```text
Milestone:
Base SHA:
Local head SHA:
Historical source boundary/selected commits:
Expected and actual files:
Focused checks:
Full regression:
Build/package/container checks:
Migration/model result:
Privacy/authorization/path review:
Synthetic or installed evidence:
Explicit limitations:
Untracked files preserved:
Remote actions performed (normally none):
Next approval required:
```

Do not record credentials, personal paths, host identity, customer content, or
venue topology in this completion record.

## Current next gate

The six manifests and consistency audit are local and complete. The next product
integration gate is the deliberate review/integration of PRs #3–#24. The next
native-evidence gate is authorization to publish the provenance-protected source.
Neither gate is authorized by this checklist.
