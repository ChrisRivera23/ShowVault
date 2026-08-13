# Local-first milestone 7 post-merge closeout preflight — 2026-08-13

## Verdict

Verdict: **do not delete or broadly clean up yet. Preserve and publish the
local-only milestone-7 closeout evidence first through a separately reviewed
docs-only branch and pull request.**

This preflight is read-only with respect to GitHub, remote refs, and existing
worktrees. It does not push, open a PR, delete a branch/worktree, rewrite a
worktree, release, or deploy.

## Final GitHub state

- [PR #37](https://github.com/ChrisRivera23/ShowVault/pull/37) is closed and
  merged, non-draft, with merge time `2026-08-13T15:43:24Z`.
- Remote `main` is exact merge commit
  `210b050c720eabf62564181be95ce628a694dada`.
- The remote candidate branch remains exact
  `0e00171f16ae4feca682de916cb29c862fe840ec`.
- Automatic mainline push run `31716939349` is completed/successful.
- Merge-SHA API check `94503923017` and Flutter check `94503922732` are both
  completed/successful.
- No later merge-SHA workflow or check exists.

The merged commit's parents, tree, and message were already verified in the
merge evidence. No GitHub closeout mutation is required for correctness.

## Local milestone-7 inventory

Six local branches carry the `codex/local-first-m7*` prefix:

- `codex/local-first-m7-mainline-candidate` at `d9982f0`;
- `codex/local-first-m7-mainline-disposition` at `550b664`;
- `codex/local-first-m7-mainline-format-repair` at `2db28fd`;
- `codex/local-first-m7-mainline-integration` at `85bc6dc`;
- `codex/local-first-m7-pr-ci-repair` at `4fe9a42`; and
- active evidence branch `codex/local-first-m7-pr-ci-race2-repair` at
  pre-preflight head `5f9a164`.

Five of those branches have milestone-7 worktrees. Four additional
account-portal milestone-7 worktrees remain. All nine scoped worktrees are
clean.

The local branch named `codex/local-first-m7-mainline-candidate` is not the
remote candidate tip: local is historical documentation commit `d9982f0`,
while the remote branch is published product `0e00171`. Any future branch
operation must use a fully qualified local or remote ref and re-read exact SHAs;
name-only commands are unsafe here.

The user's primary worktree remains on unrelated branch
`codex/windows-packaging` and has its pre-existing untracked
`NEXT_CONVERSATION.md`. It must not be switched, reset, cleaned, or otherwise
included in milestone-7 cleanup.

Repository-wide cleanup is out of scope. Fourteen registered worktrees contain
uncommitted files, including the primary worktree and multiple historical
review worktrees. A broad `git clean`, worktree prune/removal sweep, or branch
deletion sweep would risk user work.

## Local-only closeout evidence

Before this preflight commit, active evidence head `5f9a164` was seven commits
ahead of exact published product `0e00171` and zero behind it:

1. `b227f32` — second PR-CI race repair review;
2. `41631b1` — second-repair source-update preflight;
3. `57bbecb` — green second-repair source update;
4. `ef81215` — PR readiness preflight;
5. `bd38ffb` — PR ready transition;
6. `983e43f` — PR merge preflight;
7. `5f9a164` — PR merge evidence.

That range changes only nine documentation paths: `CHAT_CONTINUATION_README.md`
and eight files under `docs/`. Its sorted changed-path SHA-256 is
`bd8ed77e4c9f544273c191388b6b56cf31ed43117fe0d1e46d1e8620aecf8a76`,
and its binary-diff SHA-256 is
`c551016e6b7799dcb29b582b6551f8d8c27a6a536357703eb3162ed9029e33c1`.

The merge commit's tree equals the product candidate tree, so these commits
remain a docs-only delta against current `main`; none of the merged product
implementation is being withheld locally. This preflight and its README entry
extend that local-only documentation set further.

## Recommended closeout order

1. Under separate authorization, perform a read-only publication preflight on
   the finalized active evidence head. Pin its commit/tree, complete docs-only
   diff, hashes, an absent new remote branch name, and exact draft-PR title/body.
2. Under a later separate authorization, push that exact evidence head to a new
   remote branch and open a docs-only draft PR against exact `main`. Do not move
   the already-merged candidate branch.
3. Require the follow-up PR comparison to contain documentation only and await
   all automatically triggered CI without reruns.
4. Review and merge that evidence PR only under its own gates.
5. Only after evidence is durable on `main`, separately decide whether to
   delete the remote candidate and remove the nine clean milestone-7 worktrees
   and their local branches. Re-read every worktree status immediately before
   any destructive action.

## Stop conditions and boundaries

Stop if `main`, candidate, PR #37, mainline CI, the active evidence branch, or
any scoped worktree differs from this inventory. Stop if the proposed evidence
publication includes a non-documentation path or if its target remote branch
already exists.

Never include the primary worktree or any dirty historical worktree in a cleanup
proposal. Never delete the remote candidate until the local-only evidence is
durably published and separately reviewed. No authorization in this closeout
preflight extends to releases, deployments, provider/production configuration,
real data, or native signing/install/protocol proof.
