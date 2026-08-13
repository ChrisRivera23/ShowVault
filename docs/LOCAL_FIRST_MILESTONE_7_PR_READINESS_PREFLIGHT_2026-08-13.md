# Local-first milestone 7 PR readiness preflight — 2026-08-13

## Verdict and proposed transition

Verdict: **approved for a separately authorized transition of exact draft PR
#37 to ready for review. Do not merge under that authorization.**

- Repository: `ChrisRivera23/ShowVault`
- Pull request: [#37](https://github.com/ChrisRivera23/ShowVault/pull/37)
- Required state: open, unmerged draft
- Exact base: `main` at
  `32c21cfbd51ea5f16bb5fe84c56f4efb125b1df4`
- Exact head: `codex/local-first-m7-mainline-candidate` at
  `0e00171f16ae4feca682de916cb29c862fe840ec`
- Required mergeability: mergeable, state `clean`
- Title: `Advance ShowVault through local-first milestone 7`
- Title SHA-256:
  `67e5d3e84d87346f606156828756e5b8bcf3e8629bc9a7e4b538f32637eeabb5`
- Body: 5,807 bytes, 121 lines, SHA-256
  `4b6d5e439e8de604e7c8160d7eff924ae8c6c97e69c7cb5d08f4b7b9d01fad8c`
- Comparison: 57 commits ahead, zero behind, 194 files, +29,288/-296

The proposed mutation is only the GitHub draft-to-ready transition through the
GitHub connector. It does not include title/body/base edits, metadata changes,
review requests, another push, workflow dispatch/rerun, or merge.

## Fresh GitHub evidence

Connector and raw GitHub reads agree:

- PR #37 is open, unmerged, draft, mergeable, and `clean`;
- live `main` and the candidate ref match the exact PR base/head SHAs;
- the comparison is linear and `ahead`, with merge base exact `32c21cf`;
- title, body, counts, and empty metadata retain their pinned values;
- there are no conversation comments, inline comments, reviews, review threads,
  labels, assignees, requested reviewers, requested teams, or milestone;
- exactly two current-head workflow runs exist, one `push` and one
  `pull_request`, both completed successfully;
- exactly four current-head checks exist: API and Flutter from each workflow,
  all completed successfully;
- only inherited non-failing Node.js 20 deprecation annotations remain;
- there are no newer runs or source/base updates after the green runs; and
- the repository is active and public, grants admin/push permission, disables
  auto-merge, enables merge commit/squash/rebase, disables update-branch, has no
  returned `main` protection document, and has zero rulesets.

The absence of submitted GitHub reviews is not a repository-policy blocker:
there is no protection/ruleset requiring one. Marking ready is the appropriate
way to expose the completed candidate for any desired public review; it is not
equivalent to approval to merge.

## Exact-head CI

At exact `0e00171`:

- push run `31716020102`: success;
  - API job `94500821235`: success;
  - Flutter job `94500821182`: success;
- pull-request run `31716026186`: success;
  - API job `94500840928`: success;
  - Flutter job `94500841026`: success.

The checked-in CI workflow listens to `push` and `pull_request` without a
`ready_for_review` type. GitHub's default pull-request activity set does not
normally trigger this workflow on draft-to-ready alone. The future transition
should nevertheless inspect the PR and current-head runs afterward and record
any unexpected automatically triggered run without dispatching or rerunning it.

## Local validation and review trail

The final product source tree is
`8e68dc94b9793fdf494fa14f6950fc1f6370956f`. Against exact base `32c21cf`, the
changed-path SHA-256 remains
`8cffcf6cc7a96a7574661306c8ad1c88448b8254bd9adde1ce73b2ea7c0d9a09`,
and the binary-diff SHA-256 remains
`2c8e6df18e616c880c6e505bb2ba877fbc17f8e38fe052e130a0ce0734999e5f`.

The historical adversarial and repair reviews initially required changes. Those
findings were addressed and superseded by the milestone-7 remediation review,
which approved the local integration gate with no actionable findings, and the
mainline promotion review, which approved the local candidate with no
actionable findings. The two later CI races were independently diagnosed,
repaired, validated, and freshly reviewed.

The final second-repair review records no actionable findings and a complete
local gate: 583 .NET tests, 32 Flutter tests, deterministic database-boundary
race reproduction, 80/80 focused concurrency repetitions across four concurrent
test processes, clean Flutter analysis and EF model consistency, five
sequential zero-warning Release builds, formatter verification, diff checks,
and focused credential scans. The exact publication evidence records all four
current-head checks green and mergeability `clean`.

Local documentation head `57bbecb` is intentionally not part of published
product `0e00171`; this preserves evidence without altering the reviewed PR
source. The user's primary worktree remains untouched except for its pre-existing
untracked `NEXT_CONVERSATION.md`.

## Exact future sequence

After new explicit authorization:

1. Re-read both remote refs, PR state/base/head/title/body/counts/metadata,
   feedback, repository policy, mergeability, and all current-head runs/checks.
   Stop on any drift from this preflight.
2. Mark only PR #37 ready for review using the GitHub connector's
   `mark_pull_request_ready_for_review` operation.
3. Immediately require readback of open, unmerged, non-draft state with the
   exact same base/head/title/body/comparison and empty metadata/feedback.
4. Re-read exact-head checks and workflow runs. Record any automatically
   triggered run, but do not manually dispatch or rerun anything.
5. Stop. Do not merge, enable auto-merge, request reviewers, edit metadata,
   push, deploy, or perform another external action.

## Stop conditions and boundaries

Stop without mutation if either remote ref, PR state/base/head/title/body,
comparison, metadata, feedback, mergeability, repository policy, workflow set,
or any check differs from this preflight. Any new failure or in-progress check
must be resolved under a separate authorization before readiness.

This preflight changed local documentation only. It did not mark the PR ready,
merge, push, edit PR metadata, dispatch/rerun workflows, move `main`, deploy,
configure providers or production, use real-person data, or perform native
proof. Merge remains a separately authorized gate after readiness and any
subsequent review period.
