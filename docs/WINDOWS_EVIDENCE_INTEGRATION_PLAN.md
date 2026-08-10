# Windows evidence integration plan

## Decision

Separate native Windows evidence from the accumulated product-integration review.

The recommended path is the one-file evidence-bridge PR [#26](https://github.com/ChrisRivera23/ShowVault/pull/26), based on `main`. The bridge places a manual-only workflow on the default branch and checks out one explicitly approved immutable source commit. It does not merge the accumulated product stack or claim that stack is ready to ship.

PR #26's current published revision pins `ddfcaa6af7ccd03a1e7ae8d6de29f0865a81e97b`, which predates the checksummed workflow-provenance contract and matching independent verifiers. It is no longer merge-ready. Local candidate `a927c20` contains those protections, including post-run GitHub metadata/workflow-revision attestation and deterministic bridge preparation, but must be pushed and pass remote CI before it can become the approved source pin.

Creating, merging, or dispatching this bridge requires explicit authorization. This document grants none of those permissions.

## Audited repository state

As of 2026-08-10:

- `main` is `81007283c21a63ef4b712b926afcfaa4a1530063`.
- Draft PRs #3 through #24 form a published stack. Together they place 32 commits, 237 changed files, 16,079 additions, and 106 deletions ahead of `main`.
- All four API/Flutter push and pull-request checks shown for each published PR pass. GitHub reports PRs #3–#20 and #22–#25 mergeable; PR #21's mergeability is currently unevaluated even though its checks pass.
- Draft PR #25 targets `codex/yamaha-dme5-dme3` and adds another 287 commits, 293 changed files, 46,491 additions, and 392 deletions.
- The published PR #25 head is `ddfcaa6af7ccd03a1e7ae8d6de29f0865a81e97b`. Its four current API/Flutter checks pass, but it lacks the newer provenance contract.
- The local handoff-only commit `d5e441e` is not required for Windows execution and is not part of the published head.
- `.github/workflows/windows-evidence.yml` exists on the published feature head, but not on `main`. It has never been dispatched.

GitHub requires a `workflow_dispatch` workflow file to exist on the default branch before it can receive a manual dispatch. The checkout action accepts a branch, tag, or commit SHA through its `ref` input. These two behaviors allow an immutable evidence source to be tested without first merging the complete product stack.

## Evidence-bridge shape

Create a new `codex/` branch directly from the current `origin/main`. From `apps/showvault_app`, prepare the absent workflow path in that worktree with:

```bash
dart run tool/prepare_windows_evidence_bridge.dart \
  <explicitly-approved-published-green-source-sha> \
  ../../.github/workflows/windows-evidence.yml \
  <bridge-worktree>/.github/workflows/windows-evidence.yml
```

The command accepts only a lowercase full commit SHA, a regular source workflow, an existing regular output parent, and an absent file named `windows-evidence.yml`. It validates the manual/read-only policy and the exact three approved commit-pinned actions, injects exactly one immutable checkout ref with `persist-credentials: false`, rereads the created file, and emits a bounded SHA-256 result. It refuses to overwrite an existing file.

The resulting one-file change contains the checkout step fixed to the exact source SHA:

```yaml
- name: Check out exact audited source
  uses: actions/checkout@11d5960a326750d5838078e36cf38b85af677262 # v4
  with:
    ref: <explicitly-approved-published-green-source-sha>
    persist-credentials: false
```

Retain the existing controls:

- `workflow_dispatch` only;
- `contents: read` and no other repository permission;
- pinned checkout, Flutter, and artifact-upload actions;
- pinned Flutter 3.44.8 x64;
- `windows-2025`, a 90-minute job limit, and no secrets;
- native analysis, tests, package creation, installed replacement proof, checksum verification, callback/fixture cleanup, and 14-day synthetic artifact retention.

The bridge PR should contain one changed file. Its description must identify the immutable product source SHA, state that it is an evidence bridge rather than product integration, and repeat that attended picker/Auth0, clean-customer-machine, signing, hardware, reboot, personal-data, and venue evidence remain outside the run.

## Authorized execution sequence

Only after explicit authorization for each external stage:

1. Create the evidence-bridge branch from the current `origin/main` and use the deterministic preparation command to create the one pinned workflow file.
2. Confirm the reported workflow digest, validate the YAML, and inspect the one-file diff locally.
3. Push the bridge branch and open a draft PR to `main`.
4. Review the one-file diff and confirm that the checkout source is the exact explicitly approved, published, green commit containing checksummed workflow provenance and the matching independent verifier.
5. Obtain separate approval to mark ready and merge the bridge PR.
6. Obtain separate approval to dispatch the workflow exactly once.
7. Wait for completion, then run `dart run tool/verify_windows_run.dart <workflow-run-id> <absent-output-directory>` from `apps/showvault_app`. The command must attest the successful manual GitHub run, the exact workflow revision and immutable checkout pin, the named download, both checksum sets, artifact provenance, path/privacy boundaries, package metadata, report-core checksum, and recorded Authenticode states. Independently review callback removal and owned-fixture cleanup in the bounded evidence. Signer trust remains a separate Windows signing-policy check.
8. Record runner OS/build, architecture, workflow/run/job identities, source SHA, artifact identity, exact hashes, results, and limitations.
9. Replace or remove the bridge through a later reviewed PR when the product stack reaches `main`; do not silently retarget it to mutable source.

Any failure stops the readiness claim. Preserve failed logs and bounded synthetic evidence long enough to diagnose the defect, patch the product branch, select a new immutable source SHA, and review a new bridge revision before another dispatch.

## Product-integration path remains separate

Do not retarget or merge PR #25 merely to make the workflow visible on `main`.

The published stack can be integrated in two later phases:

1. Review and merge PRs #3–#24 in dependency order, retargeting each next PR to `main` only after its predecessor is merged and rechecking its resulting diff and CI.
2. Decompose the 287 post-PR #24 commits into reviewable, dependency-ordered product milestones, or authorize a deliberately audited integration roll-up. The existing PR #25 remains a useful accumulated integration view, but it is not a substitute for that review decision.

Native Windows evidence can inform the second phase, but a passing run does not approve the accumulated product integration.
