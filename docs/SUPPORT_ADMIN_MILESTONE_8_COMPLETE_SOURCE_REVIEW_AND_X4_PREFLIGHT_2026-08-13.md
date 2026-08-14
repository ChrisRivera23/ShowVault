# Milestone 8 complete source review and X4 preflight — 2026-08-13

## Verdict

Verdict: **changes required; do not repair the pull-request body, mark ready,
or merge PR #40.**

The complete 53-path published candidate, live pull request, generated merge,
hosted checks, staged evidence, and frozen Support boundaries were reviewed.
One actionable configuration-isolation defect remains in the Support BFF.
Every repeated test, build, migration, formatting, inventory, and remote X4
check otherwise passed.

This review changes documentation only. It does not alter published source,
the remote branch, PR metadata, workflows, providers, identity, production,
deployments, releases, native state, real data, or worktree topology.

## Exact reviewed input

- repository: `ChrisRivera23/ShowVault`;
- pull request: <https://github.com/ChrisRivera23/ShowVault/pull/40>;
- exact base/current remote `main`:
  `577bbba00206f9e60a2e3c70d759a34af591106a`;
- exact remote source/PR head:
  `cc27f9ef5fa5c8028ee9d0332fe03d40744b0a81`;
- published tree:
  `3dc68f7dae304b5ec5bead5e2c70ff15224b7f97`;
- local publication-evidence parent:
  `f4636f4ba2875f9cf8d374576d96f568b2b3f256`;
- reviewed product base/merge base:
  `2dfb4cd82b6ca3cf1ef3928f73c8fe00e194b0a5`;
- published range: 16 linear single-parent commits, 53 paths,
  `+6581/-14`, no binary paths;
- sorted path-list SHA-256:
  `46491e213fb79efa14fc1dcd89d2c04286aec9c732d2a3a126373865b1747f5a`;
- binary full-index diff SHA-256:
  `45190948ecaf3dd65a64423818cc257506a2166c771209fdad9ddb08484e98da`;
- generated merge:
  `a530a455b9c3536b42b781c7f83d774c502f8599`; and
- generated-merge tree: exact published tree.

The reviewed path set contains 34 product, migration, test, configuration, and
workflow paths plus 19 documentation paths. Each path and its aggregate stat
was reproduced from immutable local objects and the GitHub comparison.

## X4 readback

Connector-first and independent raw API/remote-ref reads reproduced:

- PR state open, draft, unmerged, mergeable, and clean;
- exact base/head refs and SHAs;
- comparison diverged, 16 ahead and one behind, 16 candidate commits,
  53 files, `+6581/-14`;
- exact title `Add isolated Support administration`;
- exact 3,553-byte/74-line body at SHA-256
  `dfd3ff8eac67fbdc8434bd5a369c8d9718fb1c33971f21f307d93532315a6d45`;
- generated-merge ordered parents exact base then source and exact source tree;
- zero issue comments, inline review comments, reviews, review threads,
  labels, assignees, requested reviewers/teams, and milestone;
- admin permission, auto-merge disabled, ordinary merge modes enabled;
- no `main` protection document, repository ruleset, or effective rule; and
- four exact-head successful checks named `api`, `api`, `flutter`, `flutter`.

Automatic push run `31767741253` and pull-request run `31767770175` remain
completed successfully at exact source head. Their API jobs are `94666977290`
and `94667063631`; Flutter jobs are `94666977345` and `94667065283`. Both API
jobs include the exact Support restore, Release test, and Release build steps.
The manual `Controlled Windows evidence` workflow has zero runs at this head.

## Actionable finding

### Support portal and API same-origin aliases pass the distinct-origin gate

`SupportAdminPortalOptions.IsComplete` validates each configured root through
`Uri.TryCreate`, but then compares the original `Origin` and `ApiBaseUri`
strings with `string.Equals`. URI-equivalent origins can have different text.
For example:

```text
Origin     = https://support.showvault.test/
ApiBaseUri = https://support.showvault.test:443/
```

Both values are valid HTTPS roots. Port 443 is the default HTTPS port, so they
identify the same effective origin, but their raw strings are unequal and the
current method reports the configuration complete.

This contradicts the repaired plan's frozen requirement that the Support
portal and Support API roots be distinct. A same-origin deployment can send
the portal's host-only authentication, antiforgery, nonce, and correlation
cookies to API paths selected by the fronting host/router, collapsing the
intended browser/API namespace boundary even though those cookies do not grant
API authorization. The checked-in portal remains disabled and enabled
non-Development startup remains prohibited, so this is not evidence of a
current production exposure; it is nevertheless a readiness blocker for the
reviewed isolation contract.

The existing configuration test rejects only byte-identical portal/API values
and therefore does not cover effective-origin equivalence.

## Required bounded repair

A separately authorized local repair should change only:

- `apps/support_admin/src/ShowVault.SupportAdmin/Configuration/SupportAdminPortalOptions.cs`;
- `apps/support_admin/tests/ShowVault.SupportAdmin.Tests/SupportAdminSecurityTests.cs`; and
- one local repair-evidence document.

Parse the already validated roots and compare their normalized effective
scheme, host, and port rather than their source strings. Reject equality after
default-port normalization. Add focused cases proving implicit and explicit
HTTPS port 443 are the same origin, while genuinely distinct hosts or ports
remain accepted. Preserve every other option, disabled/default behavior,
Development-only gate, and exact-root restriction.

Run the focused Support configuration tests, complete Support BFF Release
suite, account-portal/platform/API regression suites, warning-free Release
builds, formatting, EF model drift, exact diff, privacy/secret inventories,
and local X4 readback. Commit locally and stop for an independent repair review
before any remote source update.

## Boundaries that passed source review

- The only API surface is one strict non-cacheable Support-scheme POST with a
  4-KiB exact one-field body; checked-in disabled mode maps no Support route.
- Customer authentication, personal beta, membership, JWT roles, email
  domains, and portal cookies cannot satisfy Support authorization.
- Exact issuer, distinct audience, exact read scope, fresh MFA/`iat`, stable
  subject, active `SupportReader`, explicit active organization grant, and
  direct-peer bounded rate limiting remain intact.
- Active assignment, joined grant/organization lookup, minimized projection,
  audit append, and response decision remain in one serializable transaction.
- Missing and ungranted targets retain the same result, reason, query path,
  and null-organization audit behavior.
- Projection fields, cardinality, enum/state checks, checked arithmetic, UTC
  timestamps, and banned identity/provider/payment/path/content exclusions
  remain frozen.
- Support audit rows are bounded and append-only; relationships are
  restrictive and identity/grant indexes unique.
- The isolated BFF retains exact-origin request enforcement, Code + PKCE,
  Support audience/scope, fresh MFA, distinct secure host-only cookies,
  antiforgery, bounded server-side tokens, no HTTP loggers, strict typed API
  validation, in-place rendering, and generic bounded failures.
- There is no organization directory/search, impersonation, write action,
  export/download, provider lookup, staff provisioning surface, customer route
  change, real-data fixture, or enabled checked-in Support configuration.
- The workflow delta remains exactly the three reviewed Support restore/test/
  build commands and introduces no secret, permission, action, or trigger
  change.

## Repeated local validation

- Support BFF Release tests: **13 passed, 0 failed, 0 skipped**.
- Platform suite: **40 passed, 0 failed, 0 skipped**.
- API suite: **170 passed, 0 failed, 0 skipped**.
- Account portal Release tests: **15 passed, 0 failed, 0 skipped**.
- Support BFF Release build: **0 warnings, 0 errors**.
- Account portal Release build: **0 warnings, 0 errors**.
- API Release build: **0 warnings, 0 errors**.
- EF pending-model gate: **no pending model changes**.
- Support/API/platform source and test formatting: **passed**.
- Exact committed-range whitespace and clean-worktree checks: **passed**.
- Route, scheme, forwarded-header, HTTP-logger, disabled-configuration,
  workflow, lockfile, banned-feature, and secret-value inventories: **passed**.

The passing suite confirms no broad regression but does not negate the missing
effective-origin invariant or replace its focused regression case.

## Body and stop decision

The current PR body contains one stale pre-publication CI paragraph. Because
the published source is not clean, the conditional post-publication body-only
proposal was deliberately not created. Repairing metadata first would
incorrectly advance X4 while a source blocker remains.

Stop before source repair, push, PR-body mutation, ready transition, or merge.
Identity/provider/production configuration, staff provisioning, migration
application, deployment, release, native operations, real data, workflow
dispatch/rerun, fetch, and branch/worktree cleanup remain unauthorized.
