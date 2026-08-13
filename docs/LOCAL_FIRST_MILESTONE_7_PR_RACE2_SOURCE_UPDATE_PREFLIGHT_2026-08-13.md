# Local-first milestone 7 second-repair source-update preflight — 2026-08-13

## Verdict and proposed mutation

Verdict: **approved for a separately authorized, lease-protected fast-forward of
draft PR #37's source from exact `3f2496a` to exact second repair `0e00171`,
coupled with the pinned truthful body refresh below.**

- Repository: `ChrisRivera23/ShowVault`
- Pull request: [#37](https://github.com/ChrisRivera23/ShowVault/pull/37)
- Base branch: `main`
- Exact base SHA: `32c21cfbd51ea5f16bb5fe84c56f4efb125b1df4`
- Source branch: `codex/local-first-m7-mainline-candidate`
- Required current source SHA:
  `3f2496a41c7f5ec359971b5dc206e6a42159e798`
- Exact proposed source SHA:
  `0e00171f16ae4feca682de916cb29c862fe840ec`
- Proposed source tree: `8e68dc94b9793fdf494fa14f6950fc1f6370956f`
- Draft state: remain `true`
- Title: remain `Advance ShowVault through local-first milestone 7`
- Exact replacement body:
  `docs/LOCAL_FIRST_MILESTONE_7_PR_RACE2_SOURCE_UPDATE_BODY_PROPOSAL_2026-08-13.md`
- Replacement body: 5,807 bytes, 121 lines, SHA-256
  `4b6d5e439e8de604e7c8160d7eff924ae8c6c97e69c7cb5d08f4b7b9d01fad8c`

The future push target is exact product commit `0e00171`, not local branch head
`b227f32`; the latter also contains local repair-review evidence. Explicitly
addressing the product commit keeps local disposition documentation out of the
published PR source.

## Fresh GitHub state

Connector and raw GitHub readback agree:

- PR #37 is open, unmerged, mergeable, and draft;
- base remains exact `main` `32c21cf`;
- head remains exact candidate `3f2496a`;
- comparison remains 56 commits, 194 files, +29,195/-296;
- title and current 5,537-byte body retain their exact pinned hashes;
- there are no labels, assignees, requested reviewers, requested teams,
  milestone, conversation comments, inline comments, submitted reviews, or
  review threads;
- pull-request run `31707204442` passed API and Flutter;
- simultaneous push run `31707198609` passed Flutter but failed API on the
  concurrent same-subject invitation acceptance test;
- exact current head therefore has four check runs: three successful and one
  failed, and GitHub reports mergeable state `unstable`;
- remote `main` and the remote candidate match the PR's exact SHAs;
- repository access still grants admin and push permission, auto-merge remains
  disabled, all three ordinary merge modes remain enabled, and update-branch is
  disabled; and
- `main` returns no branch-protection document and the repository has zero
  rulesets.

No new feedback or external state invalidates the second local repair or its
fresh no-findings review.

## Exact topology and prospective comparison

Exact product `0e00171` has parent `3f2496a`; the proposed update is one commit
and a strict fast-forward. The focused change is +103/-10 across exactly:

- `services/api/src/ShowVault.Api/Account/AccountAdministrationService.cs`;
- `services/api/tests/ShowVault.Api.Tests/AccountAdversarialTests.cs`; and
- `services/api/tests/ShowVault.Api.Tests/TenantApiFactory.cs`.

Its changed-path SHA-256 is
`c2d17850eb97c05592958a636ca4b1006e8592b48e0c5299bac3a320bd018260`,
and its binary-diff SHA-256 is
`c6eed69fd1614e17c58423e159c1e466ca1b57afb74ea3206e7f59911575ca79`.

Against exact base `32c21cf`, the proposed source is 57 commits ahead and zero
behind, with 194 files, 29,288 insertions, and 296 deletions. The prospective
changed-path SHA-256 is
`8cffcf6cc7a96a7574661306c8ad1c88448b8254bd9adde1ce73b2ea7c0d9a09`,
and the prospective binary-diff SHA-256 is
`2c8e6df18e616c880c6e505bb2ba877fbc17f8e38fe052e130a0ce0734999e5f`.

The complete second-repair gate and review are recorded in
`docs/LOCAL_FIRST_MILESTONE_7_PR_CI_RACE2_REPAIR_REVIEW_2026-08-13.md`. They
cover 583 .NET tests, 32 Flutter tests, the deterministic database-boundary
regression, 80/80 focused concurrency repetitions across four concurrent test
processes, clean Flutter analysis and EF consistency, five sequential
zero-warning Release builds, formatting, diff checks, and focused credential
scans.

## Exact future sequence

After new explicit authorization:

1. Re-read remote `main`, the candidate ref, PR #37, feedback, and all current
   exact-head checks; stop on any drift from the pinned facts above.
2. Recompute the proposed commit, parent, tree, comparison counts, diff hashes,
   and replacement-body bytes/hash; stop on mismatch.
3. Fast-forward only the remote candidate by pushing exact `0e00171` with an
   explicit lease requiring its current value to remain exact `3f2496a`:

   `git push --force-with-lease=refs/heads/codex/local-first-m7-mainline-candidate:3f2496a41c7f5ec359971b5dc206e6a42159e798 origin 0e00171f16ae4feca682de916cb29c862fe840ec:refs/heads/codex/local-first-m7-mainline-candidate`

4. Require remote and PR head readback at exact `0e00171`, with unchanged base,
   title, draft state, and empty metadata.
5. Replace only the PR body with the exact pinned 5,807-byte proposal, then
   require byte/hash readback and the expected 57-commit/194-file comparison.
6. Discover both automatically triggered workflows at exact `0e00171`: one
   `push` and one `pull_request`. Await both without manual dispatch or rerun.
   Require API and Flutter to pass in both workflows before calling the updated
   exact-head gate green.
7. Record both run and all four job IDs/conclusions plus final exact PR/ref
   readback. Keep the PR draft regardless of outcome.

The source push and body refresh are coupled because the current body pins the
first repair, old source, counts, hashes, validation totals, and incomplete CI
result. Updating source without the exact refresh would knowingly leave the
draft description stale.

## Stop conditions and boundaries

Stop without mutation if `main`, the remote candidate, PR base/head/state/title,
feedback, current exact-head checks, local product object, parent/tree,
comparison, hashes, or replacement body differs from this preflight. A lease
rejection is a stop condition, never permission to overwrite another update.

This preflight changed local documentation only. It did not push, edit PR #37,
rerun or dispatch CI, mark the PR ready, merge, move `main`, deploy, configure
providers or production, use real-person data, or perform native/external proof.
Ready transition and merge remain separate gates even if all updated checks
pass.
