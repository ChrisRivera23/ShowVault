# Support Admin milestone 8 stage 3 adversarial review — 2026-08-13

## Verdict

Verdict: **approve after one bounded logging repair; stop before any later
integration or operational gate.**

The exact stage-3 implementation was reviewed against the repaired milestone
plan, completed stage-2 authority, full source/test/configuration delta, and
implementation evidence. Disabled and enabled routing, origin, OIDC, cookies,
sessions, antiforgery, typed API access, rendering, privacy, and regression
boundaries were reproduced. One actionable generic-failure logging defect was
repaired with focused coverage. No other stage-3 blocker was found.

## Exact review input

- Stage-2 final-review parent:
  `9a54607f5e006cdf68d16a907c312abef30300fa`.
- Stage-3 implementation commit:
  `be23a1f6e3d797a88953dcb08652e2bbc5c8b6f0`.
- Stage-3 implementation tree:
  `fc33ef44fd9d9d62b85c3fb46a54f59c3c7335ac`.
- Stage-3 delta: 17 files, `+1053/-0`.
- Sorted path-list SHA-256:
  `1171715424a2ee795ee554bde84757591ecf537d1a40b217ea20957ac29474ed`.
- Binary full-index diff SHA-256:
  `ce32c2fd07c23d8d3b8907094e71c9722dc1a6557a996bfc39061c37412f9dcf`.
- Branch: `codex/milestone-8-support-admin-plan`.
- Worktree: `/private/tmp/showvault-milestone-8-support-admin-plan`.

The worktree was clean and the input commit, sole parent, tree, branch ref,
path hash, and binary-diff hash all matched before review.

## Finding and repair

### Handled exceptions emitted exception diagnostics — repaired

The portal returned generic `500` problem details, but it configured
`UseExceptionHandler` with the delegate overload. Under .NET 10, a handled
exception in that form still emits `ExceptionHandlerMiddleware` diagnostics by
default unless diagnostics are explicitly suppressed or an `IExceptionHandler`
service handles it. Exception text could therefore enter logs despite the
frozen rule that Support application logs contain only a generated correlation
ID and bounded outcome code.

A focused synthetic upstream `ApplicationException` reproduced the defect: the
browser response contained only the generic title, while a capturing logger
received the synthetic sensitive exception text.

The portal now uses explicit `ExceptionHandlerOptions` and sets
`SuppressDiagnosticsCallback` to return true for exceptions successfully
handled by the generic handler. The handler records only fixed outcome
`unexpected_failure` and the generated request correlation ID, then emits the
same generic response. Unhandled exceptions or failures after a response has
started retain framework fail-safe behavior.

Focused coverage proves:

- the synthetic exception text is absent from the response and all captured
  log messages;
- trace/correlation details are absent from the browser response;
- one bounded `unexpected_failure` outcome is logged; and
- invalid IDs, exact organization IDs, access tokens, and provider fixtures
  remain absent from captured logs.

The repaired source/test delta before this evidence file is two files,
`+100/-5`.

## Complete boundary revalidation

- Checked-in disabled mode registers no authentication/Razor/client services
  and returns the same security-header-protected generic `503` for `/`, unknown
  paths, and the OIDC callback.
- Enabled incomplete or non-Development configuration fails startup. Portal,
  API, and OIDC roots remain exact HTTPS settings; portal and API roots are
  distinct.
- The exact origin is enforced from the direct request scheme/host. Spoofed
  forwarding headers are ignored and no forwarded-header runtime support was
  added.
- The distinct Support OIDC scheme uses Authorization Code + PKCE, exact
  Support audience and scope, nonce/state, fresh MFA, and no offline access.
  Effective challenge cookies prove the expected distinct `__Host-` prefixes,
  root path, Secure, HttpOnly, and SameSite=None attributes.
- The authentication and antiforgery cookies retain distinct secure HttpOnly
  host-only names, root paths, Lax/Strict SameSite values, and no Domain.
- Server-side ticket handles remain opaque random 256-bit values under a
  one-gate 4,096 cap with fixed five-minute expiry and explicit removal.
  Enabled non-Development use remains impossible.
- The sole page remains authenticated and antiforgery-protected. It accepts
  only an exact non-empty D-format GUID, sends one fixed-path server-side POST,
  renders the minimized response without redirect, clears the input, and
  persists no result across refresh.
- The strict typed client retains its 256-KiB bound, exact status/media/no-store
  checks, unknown-field denial, closed-shape/cardinality/state/usage/time
  validation, server-side bearer token, fixed path, and removed HTTP loggers.
- Browser responses and HTML retain no subject, access token, provider detail,
  correlation ID, raw JSON, path/content secret, or unexpected field. There is
  no search, list, autocomplete, export, impersonation, write action, customer
  route, or account-portal namespace.
- The completed Support API, customer account portal, platform, and data model
  were not changed by the repair.

## Repeated validation

- Support BFF Release tests: **13 passed, 0 failed, 0 skipped**.
- Account portal Release tests: **15 passed, 0 failed, 0 skipped**.
- Platform suite: **40 passed, 0 failed, 0 skipped**.
- API suite: **170 passed, 0 failed, 0 skipped**.
- Support BFF Release build: **0 warnings, 0 errors**.
- Account portal Release build: **0 warnings, 0 errors**.
- API Release build: **0 warnings, 0 errors**.
- Support BFF source/test formatting verification: **passed**.
- EF pending-model gate: **no pending model changes**.
- Exact input pins, diff whitespace, route/page/API-path, scheme/cookie/scope,
  forwarded-header, HTTP-logging, response, banned-feature, and secret
  inventories: **passed**.

All fixtures are synthetic. No GitHub, fetch/push, workflow, identity provider,
production, deployment, release, native, cleanup, or real-data mutation
occurred.

## Stop boundary and next gate

Stage 3 is approved after the bounded logging repair. Stop here. A fresh
authorization should perform one final read-only review of the exact
repair/review commit and this evidence before any integration, publication, or
operational planning is considered. Repair only a newly proven stage-3 defect
with focused coverage.

No authorization here extends to GitHub, staff provisioning, customer routes,
organization search/list/export, provider access, identity configuration,
deployment, production, release, native operations, or cleanup.
