# Local-first milestone 7 PR merge preflight — 2026-08-13

## Verdict

Verdict: **approved for a separately authorized exact merge-commit operation on
PR #37, followed by strict commit/ref readback and automatic push-CI wait.**

This preflight does not authorize or perform the merge.

## Exact merge candidate

- Repository: `ChrisRivera23/ShowVault`
- Pull request: [#37](https://github.com/ChrisRivera23/ShowVault/pull/37)
- State: open, unmerged, ready for review, mergeable, and `clean`
- Exact base: `main` at
  `32c21cfbd51ea5f16bb5fe84c56f4efb125b1df4`
- Exact head: `codex/local-first-m7-mainline-candidate` at
  `0e00171f16ae4feca682de916cb29c862fe840ec`
- Merge base: exact `32c21cfbd51ea5f16bb5fe84c56f4efb125b1df4`
- Comparison: `ahead`, 57 commits ahead, zero behind, 194 files,
  +29,288/-296
- Candidate tree: `8e68dc94b9793fdf494fa14f6950fc1f6370956f`
- Title SHA-256:
  `67e5d3e84d87346f606156828756e5b8bcf3e8629bc9a7e4b538f32637eeabb5`
- Body: 5,807 bytes, 121 lines, SHA-256
  `4b6d5e439e8de604e7c8160d7eff924ae8c6c97e69c7cb5d08f4b7b9d01fad8c`

Labels, assignees, requested reviewers, requested teams, milestone,
conversation comments, inline comments, submitted reviews, and review threads
are empty. The absence of a submitted review is not a policy blocker because
the repository has neither branch protection nor a ruleset requiring one.

## Current CI and policy

At exact head `0e00171`, exactly two workflow runs exist:

- push run `31716020102`: completed/success;
- pull-request run `31716026186`: completed/success.

Exactly four check runs exist and all are completed/successful:

- API `94500821235`;
- Flutter `94500821182`;
- API `94500840928`;
- Flutter `94500841026`.

No newer workflow, check, ref update, PR content change, metadata, or feedback
appeared after the ready transition.

The repository is active and public. The connected identity has admin and push
permission. Auto-merge is disabled. Merge commits, squash merges, and rebase
merges are enabled; update-branch is disabled and merged branches are not
automatically deleted. The `main` protection endpoint returns 404 and the
repository has zero rulesets.

## Selected merge method

Use a normal merge commit. This repository's recent mainline convention is a
two-parent `Merge pull request #...` commit. A merge commit preserves all 57
reviewed commit identities and keeps exact candidate `0e00171` as its second
parent. Squash would collapse that audit trail, while rebase would rewrite every
candidate commit.

The local three-way merge calculation is conflict-free and produces exact tree
`8e68dc94b9793fdf494fa14f6950fc1f6370956f`, identical to the reviewed
candidate tree. Because current `main` is the candidate's direct merge base, no
content transformation is expected.

The exact proposed connector call is:

- operation: `merge_pull_request`;
- repository: `ChrisRivera23/ShowVault`;
- PR number: `37`;
- expected head SHA: `0e00171f16ae4feca682de916cb29c862fe840ec`;
- merge method: `merge`;
- commit title:
  `Merge pull request #37 from ChrisRivera23/codex/local-first-m7-mainline-candidate`;
- commit message: `Advance ShowVault through local-first milestone 7`.

The expected-head field prevents a merge if the PR source moves. GitHub's merge
operation does not expose an equivalent expected-base field, so the future step
must minimize the interval between exact base readback and the connector call,
then verify the resulting first parent immediately.

## Exact future merge sequence

After new explicit authorization:

1. Re-read both remote refs, the full PR state/base/head/title/body/counts and
   metadata, all feedback, comparison, repository policy, mergeability, and all
   current-head runs/checks. Stop on any difference from this preflight.
2. Invoke the GitHub connector exactly once with the operation and arguments
   above. Do not enable auto-merge.
3. Require a successful response containing `merged: true` and a merge commit
   SHA. Stop and report if GitHub refuses the merge.
4. Immediately require PR #37 to be closed/merged/non-draft, `main` to equal the
   returned merge SHA, and the candidate ref to remain exact `0e00171`.
5. Fetch the merge commit and require, in order, first parent exact
   `32c21cfbd51ea5f16bb5fe84c56f4efb125b1df4`, second parent exact
   `0e00171f16ae4feca682de916cb29c862fe840ec`, tree exact
   `8e68dc94b9793fdf494fa14f6950fc1f6370956f`, and the explicit title and
   message above. Report any mismatch; do not attempt an automatic repair.
6. Observe the automatically triggered push workflow at the new merge SHA and
   require both API and Flutter jobs to complete successfully. Record any other
   automatically triggered run. Do not dispatch or rerun a workflow.
7. Record the outcome locally and stop. Do not delete the candidate branch,
   create a release/tag, deploy, or perform provider/production/native actions.

## Stop conditions and boundary

Stop before mutation if `main`, the candidate ref, PR state/base/head/content,
comparison, metadata, feedback, policy, mergeability, workflow set, or any check
differs from this preflight. Any new pending or failing check, review feedback,
ref movement, or mergeability change requires a new decision.

After a successful merge, a failing automatically triggered mainline check is
an incident to report and diagnose under separate authorization; it does not
authorize revert, rerun, force-push, or another mutation.

This preflight changed local evidence only. PR #37 remains open and unmerged.
