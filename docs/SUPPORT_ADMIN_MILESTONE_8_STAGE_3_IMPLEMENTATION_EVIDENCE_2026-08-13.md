# Support Admin milestone 8 stage 3 implementation evidence — 2026-08-13

## Verdict and exact input

Verdict: **repaired plan stage 3 is implemented locally and passes its focused,
regression, build, configuration, browser-security, and privacy gates. Stop
before stage 4.**

- Exact stage-2 final-review input:
  `9a54607f5e006cdf68d16a907c312abef30300fa`.
- Input tree: `67023545387bdbb9406ffea84f34efcff5950f2d`.
- Branch: `codex/milestone-8-support-admin-plan`.
- Worktree: `/private/tmp/showvault-milestone-8-support-admin-plan`.

The worktree was clean and the input commit, tree, parent, and branch ref
matched before implementation.

## Implemented boundary

- `apps/support_admin` is a separate server-rendered .NET application with its
  own project, namespace, configuration, OIDC scheme, host-only cookie,
  antiforgery cookie, nonce/correlation cookie prefixes, ticket store, typed
  API client, Razor page, and tests. It shares no account-portal OIDC client,
  cookie name, session namespace, page, or customer authority.
- Checked-in configuration is disabled with null identity/API values. Disabled
  mode registers no authentication handlers or Razor endpoints and returns the
  same protected generic `503` for every path, including the OIDC callback.
- Enabled configuration requires exact HTTPS root origins, distinct portal and
  API roots, bounded complete OIDC/API settings, a fixed five-minute session,
  and Development. Any enabled non-Development configuration fails startup
  until a reviewed durable encrypted session implementation exists.
- Authorization Code with PKCE requests only `openid` and exact
  `support:organizations:read`; it requests fresh MFA with `max_age=0`, sends
  the distinct Support audience, and never requests offline access.
- The browser boundary enforces the exact configured scheme/host, secure
  HttpOnly host-only cookies, Strict antiforgery, no sliding session,
  server-side token tickets, local session removal, and CSP/frame/referrer/
  cache/content-type/permissions protections.
- The bounded Development-only ticket store uses opaque random 256-bit handles,
  expires after five minutes, caps at 4,096 under one gate, evicts expired/
  oldest entries, and removes sessions explicitly. Access tokens never enter
  browser cookies or HTML.
- The single protected page accepts only an exact non-empty D-format
  organization GUID in a CSRF-protected POST. It sends one strict server-side
  POST to the existing Support API, renders the minimized typed response in the
  same response, and clears the input. There is no organization ID in a browser
  URL, redirect, result handle, result store, or later page refresh.
- The typed client removes all HTTP client loggers, sends the server-side bearer
  token, bounds the response to 256 KiB, requires exact `200` JSON with
  `no-store`, rejects unknown JSON fields, and revalidates the exact member
  matrix, closed commercial states, bounded sorted attention reasons, usage,
  exact hosted-sync buckets, non-negative counts, UTC timestamps, and requested
  organization identity before rendering.
- Invalid input, authentication/API/shape failure, and unexpected exceptions
  return generic browser output. Structured application logs contain only a
  generated correlation ID and bounded outcome code; no organization ID,
  token, subject, API body, provider detail, or exception is logged.

During focused testing, disabled mode initially allowed ASP.NET's automatic
authentication middleware to intercept the configured OIDC callback before
the fallback response. Authentication, ticket, Razor, and typed-client services
are now registered only when the portal is enabled. Coverage proves `/`, an
unknown path, and the callback all return the same generic disabled `503`.

## Focused security proof

The 12-test Support BFF suite covers:

- generic disabled behavior and security headers across all route classes;
- exact HTTPS-root validation, complete Development configuration, and
  fail-closed non-Development startup;
- opaque bounded server-side sessions, fixed expiry, and explicit removal;
- exact origin enforcement while ignoring a spoofed forwarding header;
- distinct audience, Code + PKCE, nonce/state, exact scope, no offline access,
  and fresh MFA challenge parameters;
- distinct secure host-only cookie/correlation/nonce namespaces and fixed
  non-sliding lifetime;
- authenticated pages retaining access tokens server-side;
- mandatory antiforgery, exact GUID POST, exact API path/body/bearer behavior,
  same-response rendering, and no persisted result after refresh; and
- generic invalid-input and upstream-failure output without echoing the input,
  provider fixture, or token.

All identities, organizations, endpoints, tokens, and responses are synthetic.

## Repeated validation

- Support BFF Release tests: **12 passed, 0 failed, 0 skipped**.
- Account portal Release tests: **15 passed, 0 failed, 0 skipped**.
- Platform suite: **40 passed, 0 failed, 0 skipped**.
- API suite: **170 passed, 0 failed, 0 skipped**.
- Support BFF Release build: **0 warnings, 0 errors**.
- Account portal Release build: **0 warnings, 0 errors**.
- API Release build: **0 warnings, 0 errors**.
- Support BFF source/test formatting verification: **passed**.
- EF pending-model gate: **no pending model changes**; stage 3 adds no migration.
- New-file whitespace, one-page/one-API-path inventory, distinct cookie/scheme
  inventory, no forwarded-header runtime use, no HTTP logging middleware, no
  offline scope, disabled configuration, raw-JSON rendering, and banned-feature
  inventories: **passed**.

The first account-portal no-restore build attempt stopped because that untouched
project's local assets file was absent. A normal local dependency restore was
performed; its tests and warning-free Release build then passed. No source or
external state changed as part of that restore.

No GitHub, fetch/push, workflow, identity provider, production, deployment,
release, native, branch/worktree cleanup, or real-data operation occurred.

## Stop boundary and next gate

Stop after this local stage-3 implementation/evidence commit. Stage 4 requires
fresh explicit authorization and must perform a complete adversarial review of
the exact stage-3 source, tests, configuration, browser/OIDC/session/API
boundaries, rendered output, and evidence before any later integration or
operational gate. Repair only proven stage-3 findings and stop again.

No authorization here extends to staff provisioning, customer routes,
organization search/list/export, provider access, deployment, production
identity configuration, or any operation excluded above.
