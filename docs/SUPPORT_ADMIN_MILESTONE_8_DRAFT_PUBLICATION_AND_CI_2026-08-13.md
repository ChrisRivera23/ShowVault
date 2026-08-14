# Milestone 8 draft publication and CI evidence — 2026-08-13

## Outcome

Under explicit Product Owner authorization, the exact reviewed Milestone 8
candidate was published as one new remote branch and one draft pull request.
Both automatically triggered CI runs passed at the exact published head. The
pull request remains open, draft, clean, and unmerged.

No ready transition, merge, workflow dispatch or rerun, fetch, provider or
identity operation, production change, deployment, release, native operation,
real-data use, or branch/worktree cleanup was performed.

## Exact publication inputs

- repository: `ChrisRivera23/ShowVault`;
- required and retained remote `main`:
  `577bbba00206f9e60a2e3c70d759a34af591106a`;
- source branch: `codex/milestone-8-support-admin-plan`;
- exact published head:
  `cc27f9ef5fa5c8028ee9d0332fe03d40744b0a81`;
- published-head tree:
  `3dc68f7dae304b5ec5bead5e2c70ff15224b7f97`;
- reviewed product base and merge base:
  `2dfb4cd82b6ca3cf1ef3928f73c8fe00e194b0a5`;
- source range: 16 linear single-parent commits, 53 paths,
  `+6581/-14`, with no binary paths;
- sorted path-list SHA-256:
  `46491e213fb79efa14fc1dcd89d2c04286aec9c732d2a3a126373865b1747f5a`;
- binary full-index diff SHA-256:
  `45190948ecaf3dd65a64423818cc257506a2166c771209fdad9ddb08484e98da`;
- repaired workflow blob:
  `a71a56547af4afa68b43a9c28681d6d89ef325f2`; and
- active pre-publication `main` workflow blob:
  `e5f40987be3ea78e00e42ffc4818f648a44f7c08`.

The exact title was `Add isolated Support administration`, with SHA-256
`e281d6ed946aa7078a4c69a0d475c7dbce265a48546a3703e93f8b03dc7784d8`.
The exact body was
`docs/SUPPORT_ADMIN_MILESTONE_8_POST_CI_REPAIR_PR_BODY_PROPOSAL_2026-08-13.md`,
3,553 bytes and 74 newline-terminated lines, with SHA-256
`dfd3ff8eac67fbdc8434bd5a369c8d9718fb1c33971f21f307d93532315a6d45`.

## Immediate no-drift gate

Immediately before the first remote write, the complete fail-closed gate
reproduced the clean local branch, exact head/tree/parent, 16-commit linear
range, 53-path inventory, statistics, no-binary result, both full-range hashes,
workflow blob, title hash, and byte-exact body.

Connector, raw GitHub API, and `git ls-remote` readback required and found:

- remote `main` remained exact;
- its ordered parents remained prior main
  `ffbb3902717fa02c56e7a66b5635f3e7d63981bb` then product base
  `2dfb4cd82b6ca3cf1ef3928f73c8fe00e194b0a5`;
- its tree remained
  `fea87b4dc7492a5187dcd60cc618ddff77b067db`;
- the intended source ref was absent;
- no pull request existed for that source;
- repository permission remained admin;
- auto-merge remained disabled and ordinary merge modes enabled;
- `main` remained unprotected with no protection document, ruleset, or
  effective rule; and
- current-main CI run `31757836927` remained successful in both API and
  Flutter jobs.

Two preliminary shell attempts stopped before the push because zsh rejected an
unquoted API query-string character. The complete corrected gate was repeated
in the same fail-closed command as the push. An orchestration attempt before PR
creation also stopped before calling the connector because an unavailable
local byte-count helper was referenced; the exact body was revalidated through
Git before the successful connector call. These stopped attempts caused no
remote mutation.

## Guarded branch publication

Only the pinned empty-expected-value absence lease was executed:

```text
git push --force-with-lease=refs/heads/codex/milestone-8-support-admin-plan: origin cc27f9ef5fa5c8028ee9d0332fe03d40744b0a81:refs/heads/codex/milestone-8-support-admin-plan
```

Git reported a new branch. Immediate raw API and `git ls-remote` readback both
returned exact head
`cc27f9ef5fa5c8028ee9d0332fe03d40744b0a81`; `main` remained unchanged.
No existing ref was force-updated.

## Draft pull request

- pull request: <https://github.com/ChrisRivera23/ShowVault/pull/40>;
- number: `40`;
- state: open, draft, unmerged, mergeable, and clean;
- base: `main` at exact
  `577bbba00206f9e60a2e3c70d759a34af591106a`;
- head: `codex/milestone-8-support-admin-plan` at exact
  `cc27f9ef5fa5c8028ee9d0332fe03d40744b0a81`;
- comparison: diverged, 16 ahead and one behind, 16 candidate commits,
  53 files, `+6581/-14`;
- generated merge:
  `a530a455b9c3536b42b781c7f83d774c502f8599`;
- generated-merge ordered parents: exact base then exact head;
- generated-merge tree: exact candidate tree
  `3dc68f7dae304b5ec5bead5e2c70ff15224b7f97`; and
- issue comments, inline review comments, reviews, review threads, labels,
  assignees, requested reviewers/teams, and milestone: empty.

Connector and raw API readback reproduced the exact title, body bytes and
terminal newline, title/body hashes, refs, counts, statistics, generated merge
parents/tree, and empty feedback surfaces. GitHub temporarily reported
`mergeable_state=unstable` while required checks were running and reported
`clean` after they succeeded.

## Automatic CI

No workflow was dispatched or rerun.

### Push run

- run: <https://github.com/ChrisRivera23/ShowVault/actions/runs/31767741253>;
- event: `push`;
- exact head:
  `cc27f9ef5fa5c8028ee9d0332fe03d40744b0a81`;
- result: completed successfully;
- API job `94666977290`: completed successfully; and
- Flutter job `94666977345`: completed successfully.

### Pull-request run

- run: <https://github.com/ChrisRivera23/ShowVault/actions/runs/31767770175>;
- event: `pull_request`;
- exact head:
  `cc27f9ef5fa5c8028ee9d0332fe03d40744b0a81`;
- result: completed successfully;
- API job `94667063631`: completed successfully; and
- Flutter job `94667065283`: completed successfully.

Both API jobs included and passed, in order:

```text
dotnet restore apps/support_admin/tests/ShowVault.SupportAdmin.Tests/ShowVault.SupportAdmin.Tests.csproj
dotnet test apps/support_admin/tests/ShowVault.SupportAdmin.Tests/ShowVault.SupportAdmin.Tests.csproj --configuration Release
dotnet build apps/support_admin/src/ShowVault.SupportAdmin/ShowVault.SupportAdmin.csproj --configuration Release --no-restore
```

The manual `Controlled Windows evidence` workflow had zero runs at the exact
head.

## Final state and stop

Final readback retained exact `main`, source ref, PR base/head, title/body,
comparison, generated merge tree, empty feedback, and four successful hosted
jobs. Checked-in Support behavior remains disabled by default.

Stop before complete source review, any body correction, ready transition, or
merge. A later gate requires fresh explicit authorization for a complete
source/diff/security review and exact-head readiness preflight. Identity and
staff provisioning, durable non-Development sessions, migration application,
deployment, production enablement, monitoring/retention/revocation, provider
operations, real data, release, native work, and cleanup remain separately
unauthorized.
