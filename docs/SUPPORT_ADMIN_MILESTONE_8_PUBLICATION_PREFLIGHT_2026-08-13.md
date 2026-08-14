# Support Admin milestone 8 publication preflight — 2026-08-13

## Verdict

Verdict: **changes required before publication. Do not publish exact candidate
`682c4a381f5101ac8c192269bc6aa11824d9c6ef`.**

The local candidate, remote base, branch/PR absence, repository permissions,
merge policy, protection/rules, feedback surfaces, and current-main CI all
match the intended no-drift state. One integration defect blocks publication:
the active CI workflow does not restore, test, or build the new Support BFF.
Publishing this candidate would create push and pull-request runs whose API and
Flutter jobs can pass without compiling or testing the new application.

This preflight records documentation only. It does not modify the workflow or
product, publish a branch, create a pull request, fetch, dispatch or rerun CI,
or mutate any external state.

## Exact local input

- Worktree: `/private/tmp/showvault-milestone-8-support-admin-plan`.
- Branch: `codex/milestone-8-support-admin-plan`.
- Candidate:
  `682c4a381f5101ac8c192269bc6aa11824d9c6ef`.
- Candidate parent:
  `e1e6db12860dc7140e8738c9787a3fb8c8d5704b`.
- Candidate tree:
  `b85dc241b97c8616cb7e3ca6dd649faa5caf8d0f`.
- Candidate closeout delta: two documentation paths, `+270/-3`.
- Closeout sorted path-list SHA-256:
  `f5f4e163509bb78653a78f987d13f6bd0980f8016579e2b83d44923c7e918a56`.
- Closeout binary full-index diff SHA-256:
  `0a1dcf4ca61d67a2ca8a735ca319e1166fa887c6140470bf80c4501c1426a5e9`.
- Reviewed product base:
  `2dfb4cd82b6ca3cf1ef3928f73c8fe00e194b0a5`.
- Base-through-candidate range: 12 exact linear commits, 47 unique paths,
  `+5792/-14`, and no binary paths.
- Full-range sorted path-list SHA-256:
  `462f96e85bcff08e45bf1beeb3e33b1a7c45d125686fd99e30df89a6718a08f7`.
- Full-range binary full-index diff SHA-256:
  `eddc022279f9431fadb7aaf098b40aacf38d35db296c5d64bdfbf0e40e92b8e6`.

The scoped worktree was clean, had no staged paths, and its branch ref matched
the exact candidate before these two preflight documents were added.
Committed-range `git diff --check` passed.

The corrected planning binary-diff hashes reproduce again:

- `7237f0f`:
  `0d61fa05600864d87c6434aadd8fdbf31fcc205e18b5c7d4b567a081a1a86362`;
- `095fc81`:
  `20b25f8b2cf408fb2e4fa4b4b08cb20ba48620e6e6f1c93f580de60462d32644`.

No local drift or new hash discrepancy was found.

## Current GitHub readback

Repository: `ChrisRivera23/ShowVault`.

- Authenticated user: `ChrisRivera23`.
- Repository permission: `admin` (admin, maintain, push, pull, and triage are
  all true).
- Visibility: public; repository is not archived.
- Default branch: `main`.
- Current `main`:
  `577bbba00206f9e60a2e3c70d759a34af591106a`.
- Ordered parents: prior main
  `ffbb3902717fa02c56e7a66b5635f3e7d63981bb`, then exact reviewed PR #39 head
  `2dfb4cd82b6ca3cf1ef3928f73c8fe00e194b0a5`.
- Current-main tree:
  `fea87b4dc7492a5187dcd60cc618ddff77b067db`, byte-identical to the candidate's
  reviewed product-base tree.
- Intended source ref `codex/milestone-8-support-admin-plan`: absent.
- Pull request for that source or Milestone 8 Support scope: absent.
- Current open pull requests: only unrelated draft PR #14, targeting
  `codex/system-inventory-plugin` rather than `main`.
- Candidate feedback, labels, assignees, review requests, reviews, inline
  threads, reactions, and comments: none because neither source ref nor pull
  request exists.

Repository policy remains unchanged:

- auto-merge disabled;
- normal merge, squash, and rebase modes enabled;
- update-branch disabled;
- `main` reports `protected: false` and no protection document;
- repository rulesets: empty;
- effective rules for `main`: empty; and
- no review or status check is policy-required.

The active workflows are `CI` at `.github/workflows/ci.yml` and manual
`Controlled Windows evidence`. Current-main automatic push run `31757836927`
is completed/successful at exact `577bbba`; API job `94637455749` and Flutter
job `94637455796` both passed. No workflow was dispatched or rerun.

## Publication blocker: Support BFF is absent from CI

Remote-main, product-base, and candidate `.github/workflows/ci.yml` all have
exact blob `e5f40987be3ea78e00e42ffc4818f648a44f7c08` (1,776 bytes), and the Milestone
8 range changes no workflow path.

The active CI triggers on both `push` and `pull_request` and has two jobs:

- `api`: EF pending-model, API, contracts, platform, Agent, Agent Release,
  account-portal Release tests, and account-portal Release build; and
- `flutter`: dependency resolution, analysis, and tests.

There is no `support_admin` reference. Therefore the automatically triggered
jobs do not compile `ShowVault.SupportAdmin`, run its 13 security tests, or
prove the Support BFF on GitHub's Ubuntu runner. This is material because
Milestone 8 adds a new deployable application and because the account portal's
parallel BFF already has explicit CI coverage.

The strong exact local Support test/build evidence remains valid, but it does
not substitute for a maintained integration regression gate. The publication
preflight is rejected until this omission is repaired and reviewed locally.

## Exact bounded repair required before publication

Under fresh authorization, change only `.github/workflows/ci.yml` plus focused
documentation evidence. In the existing `api` job, after the account-portal
steps, add the established parallel sequence:

```yaml
      - run: dotnet restore apps/support_admin/tests/ShowVault.SupportAdmin.Tests/ShowVault.SupportAdmin.Tests.csproj
      - run: dotnet test apps/support_admin/tests/ShowVault.SupportAdmin.Tests/ShowVault.SupportAdmin.Tests.csproj --configuration Release
      - run: dotnet build apps/support_admin/src/ShowVault.SupportAdmin/ShowVault.SupportAdmin.csproj --configuration Release --no-restore
```

The repair must reproduce 13/13 Support BFF Release tests and a zero-warning
Support BFF Release build, retain the existing API/Flutter workflow contract,
pass workflow syntax/whitespace review, and receive an independent exact-diff
review. Stop again before publication. Do not change Support product source to
make CI pass unless a separately reviewed source defect is proven.

## Contingent publication proposal after repair approval

The current exact candidate must not be pushed. After the workflow repair and
its local review produce a new exact clean head, rerun this entire no-drift
gate and pin the new commit/tree/range/hashes.

If all pins still hold:

- remote base branch: `main`;
- required remote base commit:
  `577bbba00206f9e60a2e3c70d759a34af591106a`;
- source branch: `codex/milestone-8-support-admin-plan`;
- source ref must still be absent;
- PR mode: draft;
- proposed exact title: `Add isolated Support administration`;
- title SHA-256:
  `e281d6ed946aa7078a4c69a0d475c7dbce265a48546a3703e93f8b03dc7784d8`;
- proposed body:
  `docs/SUPPORT_ADMIN_MILESTONE_8_POST_CI_REPAIR_PR_BODY_PROPOSAL_2026-08-13.md`;
- body: 3,553 bytes, 74 newline-terminated lines; and
- body SHA-256:
  `dfd3ff8eac67fbdc8434bd5a369c8d9718fb1c33971f21f307d93532315a6d45`.

The future new-branch push must use an explicit absence lease and the exact
reviewed post-repair head:

```text
git push --force-with-lease=refs/heads/codex/milestone-8-support-admin-plan: origin <exact-reviewed-post-repair-head>:refs/heads/codex/milestone-8-support-admin-plan
```

The empty expected value after the colon means the remote ref must not exist.
This proposal does not authorize running the command. Stop if `main` moves,
the source ref appears, the local head/tree/range changes, the corrected hashes
fail, the PR title/body differs, permissions/policy change, unrelated paths
appear, or any external read is ambiguous.

## Expected publication CI and later gates

An authorized exact new-branch push should automatically trigger one `push`
CI run. Opening the draft PR against `main` should automatically trigger one
`pull_request` CI run. Each run must complete with successful `api` and
`flutter` jobs at the exact post-repair source; the `api` job must include the
new Support BFF test and build steps. Do not dispatch or rerun either workflow.
The manual Windows workflow must not run.

Publication alone must stop after exact source/ref/PR title/body/draft readback
and both automatic runs. Then require separate gates:

1. complete source, migration, tests, workflow, documentation, metadata,
   feedback, generated-merge, and CI review;
2. read-only readiness preflight, followed by separately authorized
   draft-to-ready transition;
3. read-only merge preflight with exact current main, expected head, generated
   merge parents/tree, title/message, policies, feedback, and green CI;
4. separately authorized expected-head normal merge; and
5. automatic mainline API/Flutter CI wait and exact post-merge closeout.

Ready transition, merge, workflow rerun, release, deployment, provider or
identity configuration, production enablement, durable sessions, staff/grant
operations, native work, and cleanup are not authorized by any publication
step unless separately stated.

## Stop boundary

Stop after one documentation-only preflight commit. Do not edit CI in this
gate; push or publish; create or mutate a PR/ref/workflow; fetch; force-push;
rerun CI; clean branches/worktrees; deploy; release; mutate identity, Stripe or
other provider/production state; expose or delete Keychain values; use real
data; or perform native operations.
