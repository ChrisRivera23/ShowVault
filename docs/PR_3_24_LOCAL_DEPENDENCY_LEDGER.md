# PR #3–#24 local dependency ledger

## Scope and authority

This ledger reconstructs the documented PR #3–#24 dependency order entirely
from existing local remote-tracking refs and Git objects. No fetch, GitHub query,
PR mutation, or other network action was performed.

The PR-number mapping follows the order already recorded in
`WINDOWS_EVIDENCE_INTEGRATION_PLAN.md`. It is a local snapshot, not a claim about
current GitHub head SHAs, mergeability, checks, approvals, or open/closed state.
After explicit remote-review authorization, revalidate every row against the
actual repository before acting.

## Verified local topology

- Local default-branch ref: `origin/main` at `8100728`
- Final documented stack ref: `origin/codex/yamaha-dme5-dme3` at `254cbbf`
- Branches: 22
- Commits ahead of local `origin/main`: 32
- Combined net diff: 237 files, 16,079 insertions, 106 deletions
- Ancestry: every row's head contains the immediately preceding row's head

The chain is linear locally. That does not make the accumulated stack a safe
single merge unit. Review and integrate each resulting diff in order.

## Dependency ledger

| PR | Local branch ref | Required local base | Head | Commits | Net diff | Local purpose |
| ---: | --- | --- | --- | ---: | --- | --- |
| #3 | `codex/auth-tenancy-foundation` | `main` (`8100728`) | `236fa22` | 4 | 26 files, +1,176/-9 | Organization/venue domain, Auth0 API boundary, tenant persistence |
| #4 | `codex/agent-enrollment-identity` | #3 (`236fa22`) | `6749d99` | 2 | 30 files, +1,783/-12 | Agent identity and enrollment bootstrap |
| #5 | `codex/agent-outbound-queue` | #4 (`6749d99`) | `7180241` | 1 | 20 files, +1,044/-7 | Durable Agent event queue |
| #6 | `codex/agent-command-delivery` | #5 (`7180241`) | `755859b` | 1 | 18 files, +1,146/-13 | Durable Agent command delivery |
| #7 | `codex/file-discovery-plugin` | #6 (`755859b`) | `8e79f7f` | 1 | 13 files, +671/-11 | Filesystem discovery plugin |
| #8 | `codex/immutable-recovery-package` | #7 (`8e79f7f`) | `dc8fd2c` | 1 | 13 files, +749/-42 | Immutable recovery-package format |
| #9 | `codex/package-verification` | #8 (`dc8fd2c`) | `4a1ab0f` | 1 | 10 files, +746/-10 | Independent package verification |
| #10 | `codex/controlled-local-restore` | #9 (`4a1ab0f`) | `a354beb` | 1 | 11 files, +958/-12 | Controlled local Restore |
| #11 | `codex/recovery-history-read-model` | #10 (`a354beb`) | `9f8d4a3` | 1 | 13 files, +789/-83 | Recovery-history read model |
| #12 | `codex/flutter-auth0-live-history` | #11 (`9f8d4a3`) | `b42ef9b` | 2 | 121 files, +4,651/-185 | Flutter/native scaffolding, Auth0, live history |
| #13 | `codex/system-inventory-plugin` | #12 (`b42ef9b`) | `36dc6c5` | 1 | 9 files, +219/-8 | Agent system inventory |
| #14 | `codex/network-device-discovery` | #13 (`36dc6c5`) | `3615d63` | 1 | 11 files, +465/-8 | Allowlisted network-device discovery |
| #15 | `codex/resolume-portable-bundle` | #14 (`3615d63`) | `667767b` | 3 | 11 files, +457/-10 | Resolume portable bundle and initial catalog direction |
| #16 | `codex/resolume-user-data` | #15 (`667767b`) | `652df64` | 1 | 7 files, +133/-15 | Resolume user-data protection |
| #17 | `codex/grandma-show-backups` | #16 (`652df64`) | `bf0d543` | 1 | 9 files, +372/-5 | grandMA show exports |
| #18 | `codex/yamaha-console-exports` | #17 (`bf0d543`) | `92dc57a` | 1 | 10 files, +267/-8 | Yamaha console exports |
| #19 | `codex/yamaha-clql-tf-exports` | #18 (`92dc57a`) | `893057a` | 1 | 10 files, +173/-23 | Yamaha CL/QL/TF exports |
| #20 | `codex/yamaha-dm3-exports` | #19 (`893057a`) | `15f0b4f` | 1 | 9 files, +99/-8 | Yamaha DM3 exports |
| #21 | `codex/yamaha-dsp-projects` | #20 (`15f0b4f`) | `e5a1a08` | 2 | 10 files, +224/-6 | Yamaha DSP projects and handoff |
| #22 | `codex/yamaha-pc-amplifiers` | #21 (`e5a1a08`) | `b25a6c5` | 1 | 7 files, +87/-4 | Yamaha PC amplifier projects |
| #23 | `codex/yamaha-provisionaire-control` | #22 (`b25a6c5`) | `725d9f0` | 2 | 9 files, +164/-5 | Yamaha Provisionaire/control projects and handoff |
| #24 | `codex/yamaha-dme5-dme3` | #23 (`725d9f0`) | `254cbbf` | 2 | 9 files, +93/-9 | Yamaha DME5/DME3 projects and stack handoff |

Per-row file counts are not additive. Each diff is measured from its required
local predecessor, while the 237-file total is measured from local
`origin/main` to the PR #24 head.

## Review waves

The ancestry remains strictly sequential, but review preparation can be grouped:

### Wave A — platform and recovery foundation (#3–#12)

Review tenancy, authorization, Agent identity/queues, discovery, immutable
packages, independent verification, Restore, history, and the large Flutter
native scaffold. PR #12 is the largest row and requires explicit native-project,
dependency, Auth0, and generated-file review.

### Wave B — bounded inventory and representative recovery (#13–#17)

Review the distinction between system inventory, allowlisted network discovery,
and exact product recovery sources. Current product direction later removes
Agent setup from customer onboarding; these rows must remain infrastructure and
compatibility foundations rather than customer-flow authority.

### Wave C — Yamaha recovery catalog (#18–#24)

Review each exact export/project boundary and tests without treating catalog
quantity as recovery readiness. Sequential catalog expansion is currently
paused; merging this documented foundation does not authorize new catalog work
or venue testing.

## Authorized execution procedure

Only after explicit authorization to inspect current remote state:

1. Fetch/revalidate `main`, each PR base/head, open state, checks, approvals, and
   mergeability.
2. Compare live SHAs with this ledger. Any mismatch stops use of the static row
   until its diff and descendants are recomputed.
3. Review #3 against current `main` and integrate it only through the approved
   repository workflow.
4. Rebase/retarget or otherwise refresh #4 against the newly integrated `main`
   only after #3 completes, then inspect its resulting diff and CI.
5. Repeat through #24. Never assume an old green check applies to a changed base.
6. Record the final `main` SHA. That exact SHA becomes milestone 1's required
   integration base.

Do not merge PR #25 as a shortcut. Do not mark PRs ready, retarget, merge, or
push merely because local ancestry is valid.

## Row-level review record

For each authorized live review, append evidence outside this static snapshot:

```text
PR:
Expected local branch/head:
Observed remote base/head:
Resulting diff count:
Required checks and status:
Authorization/tenant/privacy review:
Migration/model result:
Compatibility and current-product impact:
Explicit limitations:
Ready/merge approval received:
Merged main SHA:
```

Do not record credentials, tokens, personal paths, customer data, host identity,
or venue topology.

## Local reproduction

The topology can be checked without network access by walking the 22 existing
`origin/codex/*` refs in table order and requiring each previous ref to be an
ancestor of the next. The final checks are:

```bash
git merge-base --is-ancestor origin/main origin/codex/auth-tenancy-foundation
git merge-base --is-ancestor \
  origin/codex/yamaha-provisionaire-control \
  origin/codex/yamaha-dme5-dme3
test "$(git rev-list --count origin/main..origin/codex/yamaha-dme5-dme3)" = 32
test "$(git diff --name-only origin/main..origin/codex/yamaha-dme5-dme3 | sort -u | wc -l | tr -d ' ')" = 237
```

The integration preflight verifies the later six-milestone topology separately.
Together, the ledger and preflight cover the planned foundation and local-first
reconstruction boundaries without authorizing either external sequence.
