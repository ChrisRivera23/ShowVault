# Milestone 8 Support repair-publication preflight — 2026-08-14

## Verdict

The exact independently reviewed Support repair is ready for a separately
authorized fast-forward publication to the existing draft source branch,
paired with the pinned replacement pull-request body. Stop before either
external mutation.

## Exact clean input candidate

- Worktree: `/private/tmp/showvault-milestone-8-support-admin-plan`.
- Branch: `codex/milestone-8-support-admin-plan`.
- Exact clean input head:
  `178562733cc7cb9f0e38853af66ee07b493ab068`.
- Input tree: `557240a47c713b8dfd2bfd69b66864fd03712a2b`.
- Input parent:
  `aab47ba349891aeffce81ef1785da3a69b724fb6`.
- Reviewed product base:
  `2dfb4cd82b6ca3cf1ef3928f73c8fe00e194b0a5`.
- Complete range: 20 linear commits, 57 paths, `+7208/-14`, with no merge
  commits or binary paths.
- Complete sorted path-list SHA-256:
  `aa023a14280813295012fde88e43409e143248688d42c1e9c1d9891e9f2df2e6`.
- Complete binary full-index diff SHA-256:
  `c42f143244c6e64c662d82de548473aebbcfd20b6ddcd2edceb62c417aa5333a`.

The exact repair and independent-review pins, evidence hashes, focused 5/5
proof, complete Support 17/17, account portal 15/15, platform 40/40, API
170/170, three warning-free Release builds, EF, formatting, and inventory
results all reproduced from the authoritative repair/review chain. No product
source changed during this preflight.

## Exact remote-to-local fast-forward delta

The existing remote source head
`cc27f9ef5fa5c8028ee9d0332fe03d40744b0a81` is an ancestor of the exact local
input head. The remote-to-local range is four linear commits:

1. `f4636f4ba2875f9cf8d374576d96f568b2b3f256` — draft publication/CI evidence;
2. `9fe24bb6afdbed9fb5db88195e30d26f22dfef84` — complete source review and
   origin-isolation finding;
3. `aab47ba349891aeffce81ef1785da3a69b724fb6` — bounded origin-isolation
   source/test/evidence repair; and
4. `178562733cc7cb9f0e38853af66ee07b493ab068` — independent repair approval.

That range is exactly six paths and `+628/-1`, with sorted path-list SHA-256
`fa848d9fb9e58e17ac50a99123a07ae91de5b15177fed66935a8f442d6ad7e62`
and binary full-index diff SHA-256
`97469d60b5a6961fb64ddbc89b10dade426f535a973b7357530feddac8024b40`.
It rewrites no history. This preflight commit will add only this evidence and
the body proposal; its exact head and expanded fast-forward pins must be
verified again immediately before publication.

The pinned expected-old publication form is:

```text
git push --force-with-lease=refs/heads/codex/milestone-8-support-admin-plan:cc27f9ef5fa5c8028ee9d0332fe03d40744b0a81 origin refs/heads/codex/milestone-8-support-admin-plan:refs/heads/codex/milestone-8-support-admin-plan
```

The lease supplies an exact stale-state rejection. Because the expected old
commit is a verified ancestor, the planned update is fast-forward and does not
exercise a non-fast-forward rewrite. Publication must stop if either local ref
or remote expected-old ref differs at the immediate gate.

## Fresh connector and raw X4 state

Connector, raw GitHub API, generated-merge commit, and `git ls-remote`
readbacks agree:

- repository: `ChrisRivera23/ShowVault`;
- pull request: https://github.com/ChrisRivera23/ShowVault/pull/40;
- title: `Add isolated Support administration`;
- state: open, draft, unmerged, mergeable, and clean;
- exact base and remote `main`:
  `577bbba00206f9e60a2e3c70d759a34af591106a`;
- exact remote source and PR head:
  `cc27f9ef5fa5c8028ee9d0332fe03d40744b0a81`;
- comparison: diverged, 16 ahead/one behind, 16 commits, 53 paths,
  `+6581/-14`;
- generated merge:
  `a530a455b9c3536b42b781c7f83d774c502f8599`, with ordered parents exact base
  then exact source and tree
  `3dc68f7dae304b5ec5bead5e2c70ff15224b7f97`;
- issue comments, reviews, and review threads: empty;
- permission: admin;
- auto-merge: disabled; normal, squash, and rebase merge methods: enabled;
- repository rulesets: none; `main` branch protection endpoint: unprotected;
- CI workflow: active;
- automatic push run `31767741253` and pull-request run `31767770175`: both
  completed successfully at exact source head in API and Flutter, without
  dispatch or rerun; and
- controlled Windows workflow-dispatch runs at exact source head: zero.

No remote state changed during these reads.

## Exact current and proposed pull-request body

The current PR body remains byte-equal to
`docs/SUPPORT_ADMIN_MILESTONE_8_POST_CI_REPAIR_PR_BODY_PROPOSAL_2026-08-13.md`:
3,553 bytes/74 newline-terminated lines, SHA-256
`dfd3ff8eac67fbdc8434bd5a369c8d9718fb1c33971f21f307d93532315a6d45`.
It still contains the historically stale pre-publication CI paragraph and the
pre-repair Support count of 13.

The exact replacement is
`docs/SUPPORT_ADMIN_MILESTONE_8_REPAIR_PUBLICATION_PR_BODY_PROPOSAL_2026-08-14.md`:
4,046 bytes/82 newline-terminated lines, SHA-256
`303cd7fdfa50bc8feee10096152797ba7a31568df1164a9bf741f1348c6eac69`.
Relative to the current body it is `+18/-10` and changes only the information
needed to:

- describe normalized portal/API effective-origin isolation;
- record focused normalized-origin 5/5 and complete Support 17/17 results;
- describe formatting across all eight relevant projects;
- record the successful automatic push and PR run IDs and API/Flutter jobs at
  exact published head;
- require fresh automatic dual CI after repair publication; and
- replace the stale instruction to open the already-open PR with a draft-
  preservation instruction.

The title remains unchanged. The proposed body does not claim that the local
repair has already passed hosted CI.

## Immediate publication and stop gates

A separately authorized publication task must, immediately before its first
write:

1. require the scoped worktree clean and branch ref exact to the pinned final
   preflight commit;
2. reproduce the final candidate and remote-to-local path/stat/hash pins;
3. require remote `main`, remote source, PR base/head/title/body/state,
   feedback, policy, generated merge, and historical CI pins unchanged;
4. require the remote source remains the pinned expected-old lease and an
   ancestor of the local source;
5. apply only the byte-exact proposed body and exact fast-forward source
   update; and
6. wait without dispatch or rerun for automatic push and pull-request CI at
   the new exact head, requiring API and Flutter success before stopping.

Publication must not mark ready, merge, clean branches/worktrees, force a
non-fast-forward update, deploy, release, touch provider/identity/production,
Keychain or native state, or use real data.

No push/fetch, PR/ref/workflow mutation, ready transition, merge, cleanup,
deployment, release, provider/identity/production, Keychain, native, or real-
data operation occurred in this preflight.
