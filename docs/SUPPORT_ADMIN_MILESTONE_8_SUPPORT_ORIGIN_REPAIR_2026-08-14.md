# Milestone 8 Support origin-isolation repair — 2026-08-14

## Verdict

The bounded local repair is complete and passes the prescribed focused,
regression, build, model, formatting, diff, privacy/secret, and read-only X4
gates. Stop after the local commit for an independent repair review before any
push or pull-request mutation.

## Authorized scope and exact input

- Worktree: `/private/tmp/showvault-milestone-8-support-admin-plan`.
- Branch: `codex/milestone-8-support-admin-plan`.
- Exact clean input head:
  `9fe24bb6afdbed9fb5db88195e30d26f22dfef84`.
- Input tree: `d7f01fc3b86553d999750b974023f0b7fbfef7b6`.
- Input parent: `f4636f4ba2875f9cf8d374576d96f568b2b3f256`.
- Authorized product paths:
  `apps/support_admin/src/ShowVault.SupportAdmin/Configuration/SupportAdminPortalOptions.cs`
  and
  `apps/support_admin/tests/ShowVault.SupportAdmin.Tests/SupportAdminSecurityTests.cs`.
- This document is the one authorized repair-evidence path.

The input worktree was clean and exactly pinned before the repair. No
unrelated path was changed.

## Finding and repair

`SupportAdminPortalOptions.IsComplete` already required the portal origin and
API base URI to be exact HTTPS roots, but it compared their original strings
for distinctness. An implicit default port and its explicit spelling, for
example `https://support.showvault.test/` and
`https://support.showvault.test:443/`, therefore passed as different strings
despite naming the same effective origin.

The repair parses the already validated roots and compares their normalized
HTTPS scheme, IDN host, and effective port. It rejects default-port aliases and
hostname case aliases while retaining genuinely distinct hosts or ports.
Validation remains fail-closed before parsing, so the helper receives only
non-null absolute exact HTTPS roots.

Focused regression coverage now proves:

- implicit and explicit HTTPS port 443 are the same origin and are rejected;
- hostname case differences are the same origin and are rejected;
- a distinct API hostname is accepted; and
- a distinct effective API port is accepted.

The repair does not change enablement, the Development-only gate, HTTPS-root
requirements, OIDC authority/audience/client bounds, cookie or session
configuration, API behavior, routes, authorization scopes, response shape,
logging, persistence, migrations, or checked-in disabled configuration.

## Local validation

- Focused configuration regression: **5 passed, 0 failed, 0 skipped**.
- Complete Support BFF Release suite: **17 passed, 0 failed, 0 skipped**.
- Complete account portal Release suite: **15 passed, 0 failed, 0 skipped**.
- Complete platform Release suite: **40 passed, 0 failed, 0 skipped**.
- Complete API Release suite: **170 passed, 0 failed, 0 skipped**. The API
  suite was repeated sequentially with `--no-build` after the parallel matrix
  and passed cleanly.
- Support BFF Release build: **0 warnings, 0 errors**.
- Account portal Release build: **0 warnings, 0 errors**.
- API Release build: **0 warnings, 0 errors**.
- EF `migrations has-pending-model-changes`: **no pending model changes**.
- `dotnet format --verify-no-changes --no-restore`: **passed** for all eight
  Support, account portal, platform, and API source/test projects.
- `git diff --check`: **passed**.

Before adding this evidence document, the product/test repair was exactly two
paths and `+34/-1`. Its sorted path-list SHA-256 was
`0982042e70f458ac24777b1f45b30a7aaa944ce10b04d58f239a5ee04d3483f9` and
its binary full-index diff SHA-256 was
`087c3f3040222522afe3b3a8c815da36e76dc932a0b620bdeab56ea73054a3c2`.
The repaired source blobs were:

- options:
  `c700c9152e0dfbf554885f68df7b01990fe40469`;
- tests:
  `e089f3429eab03b2a332b10b56f228138a726e80`.

Added-line credential/private-key, privacy-field, and route/scheme/scope
inventories returned no matches. The only new literal data is synthetic
`.test` URL data. No token, credential, personal/customer/venue/provider,
payment, filesystem, backup, manifest, or production value was introduced.

## Fresh read-only X4 no-drift result

Connector, raw GitHub API, generated-merge commit, and `git ls-remote`
readbacks agree:

- pull request: https://github.com/ChrisRivera23/ShowVault/pull/40;
- state: open, draft, unmerged, mergeable, and clean;
- exact base and remote `main`:
  `577bbba00206f9e60a2e3c70d759a34af591106a`;
- exact remote source and PR head:
  `cc27f9ef5fa5c8028ee9d0332fe03d40744b0a81`;
- comparison: diverged, 16 ahead/one behind, 16 commits, 53 paths,
  `+6581/-14`;
- generated merge:
  `a530a455b9c3536b42b781c7f83d774c502f8599`, with ordered parents exact base
  then exact remote source and tree
  `3dc68f7dae304b5ec5bead5e2c70ff15224b7f97`;
- title and body remain unchanged;
- issue comments, reviews, and review threads remain empty;
- repository permission remains admin; auto-merge remains disabled; normal,
  squash, and rebase merges remain enabled; and
- automatic push run `31767741253` and pull-request run `31767770175` remain
  completed successfully at the exact remote source head.

The local repair intentionally remains beyond the remote source. No fetch,
push, ref or PR mutation, ready transition, merge, workflow dispatch/rerun,
cleanup, deployment, release, identity/provider/production, Keychain, native,
or real-data operation occurred.

## Stop boundary and next gate

Stop after one local repair/evidence commit. The next task requires fresh
explicit authorization for an independent read-only review of that exact
commit, including its parent/tree/ref, three-path delta and hashes, normalized
origin semantics, focused coverage, complete validation, and remote no-drift
pins. Repair only a newly proven defect and stop again before any push or pull-
request mutation.
