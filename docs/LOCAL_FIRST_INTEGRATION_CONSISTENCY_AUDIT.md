# Local-first integration manifest consistency audit

## Result

The six extraction manifests form one complete, dependency-ordered source plan
for the current local-first product direction. Their boundaries have no commit
gap, duplicate selected commit, or accidental inclusion of the four unrelated
integration-planning commits interleaved in the Windows history.

This is a local read-only history audit. It does not authorize branch creation,
pushes, PR operations, merges, workflow dispatch, Docker execution, cloud
resources, external equipment, personal data, or venue use.

## Commit coverage

Milestones 1–5 are a continuous first-parent chain:

| Milestone | Boundary | Commits | Net files |
| --- | --- | ---: | ---: |
| 1 — direct Scan | `310190c..ce5be25` | 9 | 41 |
| 2 — local vault and offline Save | `ce5be25..c172e49` | 6 | 36 |
| 3 — synchronization and Restore | `c172e49..fff4434` | 10 | 31 |
| 4 — deployable object storage | `fff4434..69b83ab` | 2 | 28 |
| 5 — resilience, diagnostics, upgrade | `69b83ab..3a5e715` | 7 | 19 |

Together those five milestones cover exactly 34 commits. Their combined net
diff `310190c..3a5e715` changes 112 files. Per-milestone file counts are not
additive because later milestones deliberately extend earlier files.

Milestone 6 selects 18 Windows commits from the 22-commit contiguous span
`3a5e715..2e107a8`. It excludes exactly:

- `626e88d` — local-first product integration plan;
- `0c174ba` — that plan's handoff;
- `a1c3c83` — local-first overlap audit; and
- `65c50be` — that audit's handoff.

The 18 selected Windows commits touch a 35-path union. Eleven of those paths
also occur in the milestones 1–5 net diff. Therefore the complete 52-commit
source selection touches a 136-path union:

```text
112 milestone-1-to-5 paths
+35 selected Windows paths
-11 shared paths
=136 selected paths
```

The first 40 product-directed commits through published head `ddfcaa6` are fully
accounted for: 34 commits in milestones 1–5 plus the first six selected Windows
commits. The remaining 12 selected Windows commits add artifact verification,
workflow provenance, run attestation, and deterministic bridge preparation and
verification.

## Legacy separation

The selected 136-path union overlaps the paused `254cbbf..310190c` legacy
catalog/Agent slice in exactly 29 files. This matches
`LOCAL_FIRST_INTEGRATION_AUDIT.md`.

No manifest treats those 29 paths as safe wholesale cherry-picks. Each is
assigned carry, split, regenerate, or compatibility handling. Customer-facing
Agent installation, enrollment, service setup, broad inventory, and personal
Keychain behavior remain excluded. Retained Agent code is compatibility
infrastructure only.

The published `ddfcaa6` correction is consistently assigned to
`services/agent/src/ShowVault.Agent/Recovery/RecoveryPackageWriter.cs`: explicit
legacy `PackageDirectory` mode must not construct `LocalVaultLayout` or resolve
an unavailable default Documents vault. Default local-vault mode remains
fail-closed.

## Dependency and safety consistency

The manifests preserve these cross-milestone dependencies:

1. Direct Scan establishes opaque allowlisted candidate keys and tenant-scoped
   detections before Save consumes a `UserDataRoot` key.
2. Offline Save establishes immutable verified packages and queue intent before
   synchronization consumes them.
3. Restore and hosted synchronization independently reverify local packages and
   keep cloud state separate from local verification.
4. Production hosted storage remains fail-closed until the S3-compatible
   provider milestone is integrated.
5. Resilience and upgrade harnesses call the integrated customer services but
   remain compile-time-gated synthetic command modes.
6. Windows packaging consumes the completed local-first behavior and retains all
   vault, path, diagnostic, provenance, and cleanup boundaries.

Every manifest requires focused tests, the relevant full Flutter/.NET suites,
EF pending-model checks, privacy/authorization review, and `git diff --check`.
Native, installed, Docker, cloud, remote, destructive cleanup, and venue actions
remain separately authorized even when a manifest describes their acceptance
criteria.

## Documentation topology

All six manifests are referenced by:

- `LOCAL_FIRST_INTEGRATION_AUDIT.md`;
- `WINDOWS_EVIDENCE_INTEGRATION_PLAN.md`;
- `CHAT_CONTINUATION_README.md`; and
- the intentionally untracked `NEXT_CONVERSATION.md`.

The first five use contiguous source ranges. Milestone 6 intentionally uses a
selected-commit union because unrelated planning commits are interleaved before
the final bridge-verification commits. Shared handoff and runbook files must be
reconciled from final behavior rather than replayed verbatim.

## Reproduction

Run from the repository root with Bash:

```bash
test "$(git rev-list --count 310190c..3a5e715)" = 34
test "$(git diff --name-only 310190c..3a5e715 | sort -u | wc -l | tr -d ' ')" = 112
test "$(git rev-list --count 310190c..2e107a8)" = 56

selected=(
  58ad46a 5c7ade7 e503ca1 6fdccca 70fe056 ddfcaa6
  d5e441e 1dd2d23 a1a69eb a66f744 0644cb1 b231d4c
  7592fbe a375e40 a927c20 7b6093d 1ce2efc 2e107a8
)
windows_files="$(mktemp)"
for commit in "${selected[@]}"; do
  git diff-tree --no-commit-id --name-only -r "$commit"
done | sort -u > "$windows_files"
test "${#selected[@]}" = 18
test "$(wc -l < "$windows_files" | tr -d ' ')" = 35

product_files="$(mktemp)"
all_files="$(mktemp)"
legacy_files="$(mktemp)"
git diff --name-only 310190c..3a5e715 | sort -u > "$product_files"
sort -u "$product_files" "$windows_files" > "$all_files"
git diff --name-only 254cbbf..310190c | sort -u > "$legacy_files"
test "$(comm -12 "$product_files" "$windows_files" | wc -l | tr -d ' ')" = 11
test "$(wc -l < "$all_files" | tr -d ' ')" = 136
test "$(comm -12 "$legacy_files" "$all_files" | wc -l | tr -d ' ')" = 29
```

The 56-commit contiguous span minus the four named exclusions equals the 52
selected commits. Temporary accounting files may be discarded through normal
temporary-file cleanup; do not broaden cleanup beyond those exact files.

The cross-platform read-only verifier performs the same checks directly against
local Git objects and emits only bounded counts/status:

```bash
cd apps/showvault_app
dart run tool/verify_local_first_integration_preflight.dart
```

It runs no network command, writes no repository file, and reports no local path
or commit-file inventory. Any boundary, count, overlap, selected source, or
excluded-commit mismatch fails closed.

## Conclusion

The manifest set is internally consistent and ready to serve as an integration
planning baseline. It is not approval to execute the integration or any external
stage. If the source branch changes before authorized reconstruction begins,
rerun this audit and review every changed count, boundary, and overlap before
using the checklist.

`PR_3_24_LOCAL_DEPENDENCY_LEDGER.md` supplies the preceding 22-branch local
foundation chain. Its PR-number mapping and remote status remain a static local
snapshot until explicitly authorized live revalidation.
