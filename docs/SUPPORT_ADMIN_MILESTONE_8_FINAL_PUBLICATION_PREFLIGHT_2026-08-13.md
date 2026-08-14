# Support Admin milestone 8 final publication preflight — 2026-08-13

## Verdict

Verdict: **the reviewed Milestone 8 candidate is ready for a separately
authorized exact new-branch publication as a draft pull request. Stop before
publication.**

The complete local candidate, corrected historical hashes, Support CI repair,
draft title/body, remote base, repository permissions/policies, intended
source-ref and pull-request absence, open-PR inventory, active workflow, and
current-main checks all pass the final no-drift gate. No unresolved local or
remote publication blocker remains.

This preflight adds documentation only. It does not publish or mutate any
branch, pull request, ref, workflow, provider, identity, production system,
deployment, release, native system, Keychain value, or real data.

## Exact local input

- Worktree: `/private/tmp/showvault-milestone-8-support-admin-plan`.
- Branch: `codex/milestone-8-support-admin-plan`.
- Exact reviewed input head:
  `d3df2713a057d4d1bd70fa8d58ca25e51fade191`.
- Input tree:
  `a9a8c1c30dc421b2c2455efae161af60d3632d8d`.
- Input parent, exact reviewed CI repair:
  `e53d04bade47bd1744d1b5c1b6a69494978ea274`.
- Reviewed product base:
  `2dfb4cd82b6ca3cf1ef3928f73c8fe00e194b0a5`.
- Product-base tree:
  `fea87b4dc7492a5187dcd60cc618ddff77b067db`.

The scoped worktree was clean, had no staged paths, and both `HEAD` and the
branch ref matched the exact input before this document was added.

## Complete candidate reproduction

The reviewed product base through the input head is exactly 15 linear,
single-parent commits. The range contains 52 unique paths, `+6337/-14`, and no
binary paths.

- Sorted path-list SHA-256:
  `26cce41c0074bedb0f6766d80d74f0aba8d8ba7179a91709daed361e4178917e`.
- Binary full-index diff SHA-256:
  `5f6c18cbbaeeedac88a79b097539752c5fef06431138f4d89c2bf022495961ba`.
- Committed-range `git diff --check`: passed.

The range separates into:

- 34 product/migration/test/configuration paths, `+4041/-7`, sorted path hash
  `97fd3fae118f80a32be818c46e12fa9554620bbdeeb523aefef655ebaadf3959`,
  binary full-index diff hash
  `47359e2d6c65afd37416ac6a0a906e211618e8378791f7c1681df7c199706d1d`;
- 17 documentation paths, `+2293/-7`, sorted path hash
  `9dabbafade8d284c894439b9115aa1272cbcc7a5f6a9053748f88f7ddcdbd481`,
  binary full-index diff hash
  `317f5b12e7464d34cec3665078925199fe731f44c7b6d168c6b3c0e24e59cb38`;
  and
- one workflow path, `.github/workflows/ci.yml`, `+3/-0`, sorted path hash
  `b1e4fcd28055c712644fe84f6a1e30a41018cf387dd808cec348f2e505e33a2a`,
  binary full-index diff hash
  `adb314d950f6abcdde1860606de5e1bb34018f2db1fb26eab6dc400fd46d4307`.

The two corrected planning binary-diff hashes reproduce again:

- plan commit `7237f0f`:
  `0d61fa05600864d87c6434aadd8fdbf31fcc205e18b5c7d4b567a081a1a86362`;
- plan-review commit `095fc81`:
  `20b25f8b2cf408fb2e4fa4b4b08cb20ba48620e6e6f1c93f580de60462d32644`.

No other historical pin differs.

## Reviewed workflow and validation authority

The candidate's CI workflow is exact blob
`a71a56547af4afa68b43a9c28681d6d89ef325f2`, 2,172 bytes/42 lines. YAML parsing
passes. It retains exact `push` and `pull_request` triggers and exact `api` and
`flutter` jobs. The reviewed three-line Support sequence restores and runs the
13 Support BFF Release tests and builds the application in Release mode after
the account-portal steps. Every preexisting workflow line is unchanged.

The exact complete local workflow-equivalent validation remains authoritative:

- EF pending model: no changes;
- API 170/170;
- contracts 22/22;
- platform 40/40;
- Agent 291/291;
- account portal Release 15/15;
- Support BFF Release 13/13;
- Flutter 32/32 and analysis clean; and
- Agent, account portal, and Support BFF Release builds with zero warnings and
  errors.

The CI repair review independently reran the new Support commands and approved
the repair without finding. No source, dependency, configuration, migration,
test, or lockfile drift followed validation.

## Fresh GitHub readback

Repository: `ChrisRivera23/ShowVault`.

- Authenticated user: `ChrisRivera23`.
- Repository permission: `admin`; admin, maintain, push, pull, and triage are
  all true.
- Visibility: public; repository is not archived.
- Default branch: `main`.
- Current `main`:
  `577bbba00206f9e60a2e3c70d759a34af591106a`.
- Ordered current-main parents: prior main
  `ffbb3902717fa02c56e7a66b5635f3e7d63981bb`, then reviewed PR #39 head and
  local product base `2dfb4cd82b6ca3cf1ef3928f73c8fe00e194b0a5`.
- Current-main tree:
  `fea87b4dc7492a5187dcd60cc618ddff77b067db`, exact product-base tree.
- Intended source ref `codex/milestone-8-support-admin-plan`: absent by
  connector, raw reference API, and `git ls-remote`.
- Pull request from the intended source: absent by exact-head raw query and
  connector searches for the branch and proposed title.
- Relevant source/PR feedback, labels, assignees, review requests, reviews,
  inline threads, reactions, and comments: absent because the ref and PR do not
  exist.
- Current open pull requests: only unrelated draft PR #14, whose base and head
  are non-`main` inventory branches. It is outside this publication target and
  remains untouched.

Repository policy is unchanged:

- auto-merge disabled;
- normal merge, squash, and rebase enabled;
- update-branch disabled;
- `main` reports `protected: false` and has no protection document;
- repository rulesets: empty;
- effective rules for `main`: empty; and
- no review or status check is policy-required.

The active workflows remain `CI` at `.github/workflows/ci.yml` and manual
`Controlled Windows evidence`. Remote-main CI is still blob
`e5f40987be3ea78e00e42ffc4818f648a44f7c08`, 1,776 bytes; the candidate's
reviewed three-line diff upgrades it to the repaired blob above.

Current-main automatic push run `31757836927` remains completed/successful at
exact `577bbba`: API job `94637455749` and Flutter job `94637455796` both
passed. The legacy combined-status surface has no status contexts; the Actions
check-run surface is the authoritative two-job result. No workflow was
dispatched or rerun.

## Exact comparison against remote main

The local candidate descends from exact reviewed PR #39 head `2dfb4cd`, which
is the second parent of current-main merge `577bbba`. It does not contain that
merge commit, but current-main and the candidate's product base have the exact
same `fea87b4d` tree. Therefore the content comparison from current remote
`main` to the unpublished candidate is byte-identical to the reproduced
product-base-to-candidate range above: 15 commits' candidate content, 52 paths,
`+6337/-14`, with the same path and binary-diff hashes.

This ancestry shape is acceptable for a normal pull request. Publication must
still stop if GitHub's exact post-publication comparison does not report the
same changed paths and statistics or if its generated merge is not clean.

This preflight document will add one documentation-only commit and one unique
path. The exact final publication head, tree, 16-commit/53-path comparison,
hashes, and executable absence-lease command must be pinned in the authoritative
handoff after commit and rechecked immediately before any authorized write.

## Exact draft metadata

- Base branch: `main`.
- Required base commit:
  `577bbba00206f9e60a2e3c70d759a34af591106a`.
- Source branch: `codex/milestone-8-support-admin-plan`.
- Source-ref precondition: absent.
- Pull-request mode: draft.
- Exact title: `Add isolated Support administration`.
- Title SHA-256:
  `e281d6ed946aa7078a4c69a0d475c7dbce265a48546a3703e93f8b03dc7784d8`.
- Exact body file:
  `docs/SUPPORT_ADMIN_MILESTONE_8_POST_CI_REPAIR_PR_BODY_PROPOSAL_2026-08-13.md`.
- Body: 3,553 bytes/74 newline-terminated lines.
- Body SHA-256:
  `dfd3ff8eac67fbdc8434bd5a369c8d9718fb1c33971f21f307d93532315a6d45`.

The title/body accurately describe the local synthetic scope, CI prerequisites,
validation, and independently gated production/identity/session/operational
boundaries. No stale blocker or overclaim remains.

## Proposed exact publication sequence

Publication requires fresh explicit authorization. Immediately before the
first write, repeat these no-drift checks:

1. require the local branch is clean at the exact final preflight head and
   tree pinned in the authoritative handoff;
2. require remote `main` is still exact `577bbba` with exact parents/tree;
3. require the source ref and exact-head PR query remain empty;
4. require permissions/policies, open-PR inventory, title/body hashes,
   candidate range, workflow blob, and corrected hashes remain exact; and
5. stop on any mismatch or ambiguity.

Only after those checks, use the exact reviewed final-preflight head in the
new-branch absence lease:

```text
git push --force-with-lease=refs/heads/codex/milestone-8-support-admin-plan: origin <exact-final-preflight-head>:refs/heads/codex/milestone-8-support-admin-plan
```

The empty expected value after the source ref means the remote branch must not
exist. This command is proposed, not authorized in this gate.

After exact ref readback, create one draft pull request with the pinned base,
head, title, and byte-exact body. Verify exact PR metadata and comparison, then
wait for the automatically triggered `push` and `pull_request` CI runs. Each
run must target the exact published head and pass both `api` and `flutter`; the
API job must execute the Support restore/test/build steps. Do not dispatch or
rerun either workflow. The manual Windows workflow must not run.

Stop publication after exact source/PR readback and both automatic runs. Do not
mark ready or merge.

## Later independent gates

After publication, require separately authorized gates for:

1. complete source, migration, tests, workflow, documentation, metadata,
   feedback, generated-merge, and dual-CI review;
2. read-only readiness preflight and a separate draft-to-ready transition;
3. read-only merge preflight with exact current main, expected head, generated
   merge parents/tree, title/message, policies, feedback, and green CI;
4. exact expected-head normal merge; and
5. automatic mainline API/Flutter CI plus post-merge closeout.

Publication never authorizes workflow reruns, durable production sessions,
Support identity configuration, staff/grant operations, database migration,
deployment/DNS/TLS, monitoring/retention/revocation, provider/production
mutation, release, native operations, real data, or cleanup.

## Stop boundary

Stop after one documentation-only local preflight commit. Do not push or
publish; create or mutate a PR/ref/workflow; fetch; dispatch or rerun hosted CI;
clean branches/worktrees; force-push; revert; deploy; release; mutate identity,
provider, or production state; access Keychain values; use real data; or
perform native operations.
