# Local-first milestone 7 PR merge evidence — 2026-08-13

## Result

[PR #37](https://github.com/ChrisRivera23/ShowVault/pull/37) merged
successfully through the authorized normal merge-commit path. Remote `main` is
exact merge commit `210b050c720eabf62564181be95ce628a694dada`, and its
automatically triggered API and Flutter CI jobs both passed.

No workflow was dispatched or rerun. The candidate branch was not deleted. No
release, tag, deployment, provider/production mutation, or native operation was
performed.

## Final pre-merge gate

Immediately before mutation, connector and raw GitHub reads matched the merge
preflight in full:

- PR #37 was open, unmerged, non-draft, mergeable, and `clean`;
- exact base `main` was
  `32c21cfbd51ea5f16bb5fe84c56f4efb125b1df4`;
- exact candidate was
  `0e00171f16ae4feca682de916cb29c862fe840ec`;
- the candidate was 57 commits ahead and zero behind, with merge base exact
  `32c21cfbd51ea5f16bb5fe84c56f4efb125b1df4`;
- title SHA-256 was
  `67e5d3e84d87346f606156828756e5b8bcf3e8629bc9a7e4b538f32637eeabb5`;
- body SHA-256 was
  `4b6d5e439e8de604e7c8160d7eff924ae8c6c97e69c7cb5d08f4b7b9d01fad8c`;
- comparison counts remained 57 commits, 194 files, +29,288/-296;
- metadata and all feedback remained empty;
- push run `31716020102` and pull-request run `31716026186` remained
  completed/successful;
- all four candidate API/Flutter checks remained completed/successful; and
- policy, permissions, mergeability, refs, and repository rules had not moved.

No stop condition was present.

## Authorized merge

The GitHub connector's `merge_pull_request` operation was invoked exactly once
with:

- repository `ChrisRivera23/ShowVault`;
- PR number `37`;
- expected head SHA
  `0e00171f16ae4feca682de916cb29c862fe840ec`;
- merge method `merge`;
- title
  `Merge pull request #37 from ChrisRivera23/codex/local-first-m7-mainline-candidate`;
- message `Advance ShowVault through local-first milestone 7`.

GitHub returned `merged: true` and merge SHA
`210b050c720eabf62564181be95ce628a694dada`.

## Merge topology and PR readback

Independent connector and raw GitHub readback confirmed:

- PR #37 is closed, merged, and non-draft;
- `merged_at` is `2026-08-13T15:43:24Z`;
- remote `main` is exact `210b050c720eabf62564181be95ce628a694dada`;
- the retained candidate ref remains exact
  `0e00171f16ae4feca682de916cb29c862fe840ec`;
- merge first parent is exact
  `32c21cfbd51ea5f16bb5fe84c56f4efb125b1df4`;
- merge second parent is exact
  `0e00171f16ae4feca682de916cb29c862fe840ec`;
- merge tree is exact
  `8e68dc94b9793fdf494fa14f6950fc1f6370956f`, identical to the reviewed
  candidate tree; and
- merge title and message match the explicit proposal byte-for-byte.

The PR's source/base identity, title/body hashes, comparison counts, and empty
metadata remain unchanged in its merged record.

## Automatic mainline CI

The merge created exactly one automatic workflow at the merge SHA:

- push run `31716939349`: completed/success;
- API job/check `94503923017`: completed/success;
- Flutter job/check `94503922732`: completed/success.

The API job passed EF model consistency, the API/contracts/platform/agent test
suites, the agent Release build, account-portal tests, and the account-portal
Release build. The Flutter job passed dependency restoration, analysis, and
tests. The only annotations are the inherited non-failing Node.js 20
deprecation warnings for existing GitHub Actions versions.

## Boundary and next gate

Milestone 7 is now integrated into `main` with green mainline CI. This operation
did not delete the remote candidate, rewrite local worktrees, create a release
or tag, deploy, enable providers or production configuration, use real data, or
perform native signing/install/protocol proof.

Any branch deletion is destructive and remains separately authorized. The next
bounded action, if desired, is a read-only post-merge closeout preflight that
inventories remote/local branch and worktree state and proposes only necessary
cleanup or handoff actions. Release, deployment, provider/production, and native
operations remain independent gates.
