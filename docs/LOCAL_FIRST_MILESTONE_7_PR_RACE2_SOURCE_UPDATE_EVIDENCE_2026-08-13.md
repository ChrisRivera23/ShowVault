# Local-first milestone 7 second-repair source-update evidence — 2026-08-13

## Verdict

Verdict: **exact source/body update complete and exact-head dual-workflow CI
green; draft PR #37 is eligible for a separately authorized readiness
preflight.**

The PR remains a draft. This result does not itself authorize marking it ready,
merging, moving `main`, deploying, or performing any provider, production,
real-person, or native operation.

## Exact mutation and readback

The remote `codex/local-first-m7-mainline-candidate` ref was fast-forwarded from
required old SHA `3f2496a41c7f5ec359971b5dc206e6a42159e798` to exact second-repair product
SHA `0e00171f16ae4feca682de916cb29c862fe840ec`. The explicit preflight lease held
and the push was a one-commit fast-forward. Local documentation head `41631b1`
was not pushed.

Final PR #37 readback confirms:

- state: open draft, unmerged;
- base: `main` at exact
  `32c21cfbd51ea5f16bb5fe84c56f4efb125b1df4`;
- head: `codex/local-first-m7-mainline-candidate` at exact `0e00171`;
- title: unchanged, SHA-256
  `67e5d3e84d87346f606156828756e5b8bcf3e8629bc9a7e4b538f32637eeabb5`;
- body: exact pinned 5,807 bytes and 121 lines, SHA-256
  `4b6d5e439e8de604e7c8160d7eff924ae8c6c97e69c7cb5d08f4b7b9d01fad8c`;
- comparison: 57 commits, 194 files, +29,288/-296;
- labels, assignees, requested reviewers, requested teams, milestone,
  conversation comments, inline comments, and reviews: none; and
- GitHub mergeability: mergeable, state `clean`.

Remote `main` remains exact `32c21cf`. No source or PR drift occurred while the
two workflows ran.

## Automatically triggered exact-head CI

Both workflows triggered automatically at exact head `0e00171`. No workflow was
manually dispatched or rerun.

Push run
[`31716020102`](https://github.com/ChrisRivera23/ShowVault/actions/runs/31716020102)
passed:

- API job `94500821235`: success in 1m21s; and
- Flutter job `94500821182`: success in 1m35s.

Pull-request run
[`31716026186`](https://github.com/ChrisRivera23/ShowVault/actions/runs/31716026186)
passed:

- API job `94500840928`: success in 1m27s; and
- Flutter job `94500841026`: success in 1m31s.

All four current exact-head check runs are completed successfully. Only
inherited non-failing Node.js 20 deprecation annotations remain for existing
GitHub Action versions.

## Local proof retained

The published product had already passed the full local gate and fresh review:
583 .NET tests, 32 Flutter tests, deterministic reproduction of the second
interleaving, 80/80 focused concurrency repetitions across four concurrent test
processes, clean Flutter analysis and EF model consistency, five sequential
zero-warning Release builds, formatter verification, diff checks, and focused
credential scans. The no-findings review is recorded in
`docs/LOCAL_FIRST_MILESTONE_7_PR_CI_RACE2_REPAIR_REVIEW_2026-08-13.md`.

## Boundary and next gate

PR #37 was not marked ready or merged. No workflow was manually dispatched or
rerun, no further commit was pushed, and `main`, deployment,
production/provider configuration, real-person data, and native state were not
changed.

The next bounded task is a separately authorized read-only readiness preflight.
It should revalidate exact refs, PR metadata/body, all four current checks,
feedback, repository policy, mergeability, and the complete local review trail;
then propose whether to mark PR #37 ready. The preflight would not itself
authorize the ready transition or merge.
