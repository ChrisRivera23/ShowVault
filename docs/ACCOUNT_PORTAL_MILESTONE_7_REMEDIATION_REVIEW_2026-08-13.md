# Account portal milestone 7 remediation review — 2026-08-13

## Review checkpoint and verdict

- Reviewed head: `2caf703b6a108c244a77ea09b9be1f5a8167c595`
- Prior review commit: `faa2e18`
- Remediation commits: `1541d5d`, `2caf703`
- Branch: `codex/account-portal-m7-repair`
- Worktree: `/private/tmp/showvault-account-portal-m7-repair`

Verdict: **approved for the next local integration gate; no actionable findings.**

This approval is limited to the local synthetic milestone-7 implementation and
its evidence. It is not approval to deploy, enable production, configure Auth0,
create or mutate Stripe resources, use real-person data, perform external Git
operations, or claim real browser/provider/native proof.

This review changes documentation only. Product source, migrations,
dependencies, providers, accounts, deployment, and external state are unchanged.

## Finding closure

### Malformed invitation ordering

`AcceptInvitationAsync` now computes bounded candidate digests and rejects an
invalid shape before `HasCompleteKeyRingAsync`. The regression warms the test
host, resets its EF reader-command interceptor, submits a malformed code, and
asserts a uniform bad request with zero reader commands. Structurally valid
codes still receive the database-aware pending-key preflight.

### Temporary secret buffers

`InvitationTokenService` clears generated raw-code bytes, decoded candidate
bytes, discarded invalid secrets, and transient decoded key arrays using
`CryptographicOperations.ZeroMemory`. Success and failure paths use `finally`
or a common rejecting cleanup path. Returned codes and HMAC digests remain
independent values, so cleanup does not corrupt issued or candidate results.

### Personal-beta hosted-sync boundary

The synthetic personal-beta subject retains direct scan submission and recovery
candidate listing. An ordinary authentication for the same subject creates an
existing hosted-sync session; personal-beta authentication is then denied at
begin, file-state retrieval, chunk append, commit, and receipt. The source guard
runs before the affected session/object-store/commercial operations.

### Browser authorization redirects

Both ordinary and step-up tests now exercise actual TestServer challenges. They
parse emitted authorization URLs and assert the exact API audience, callback,
ordinary or elevated scopes, absence of `offline_access`, authorization-code
response, PKCE S256 challenge, state, and nonce. Step-up also asserts MFA
`acr_values` and `max_age=0`. A hostile forwarded host is ignored while the
canonical configured origin remains in `redirect_uri`.

### Eight-consumer authorization matrix

The endpoint test executes all five roles against all eight modules (40 active
cases), plus suspended/revoked denial for every module (16), wrong-tenant or
wrong-venue denial for every module (8), and role-preserving restoration (1).
Expected manager capability is independently frozen by the Platform role-policy
theory for Viewer, Technician, Manager, Administrator, and Owner.

## Verification

Decisive checks rerun during this review:

- API: 154 passed, 0 failed, 0 skipped;
- account portal: 15 passed, 0 failed, 0 skipped;
- EF: no pending model changes;
- remediation diff: `git diff --check` clean; and
- live-key/private-key/browser-storage/`offline_access` source scan: clean.

The remediation evidence additionally records the unchanged full gate:
Platform 30, local-engine 67, contracts 22, Agent 291, and Flutter 32 tests;
clean Flutter analysis; five zero-warning Release builds; focused formatting;
and no committed build artifacts. The counts and scenario descriptions match
the reviewed code.

## Residual gates

The local milestone is ready for separately authorized integration planning or
local integration. Auth0 operational configuration and real MFA/browser proof,
durable production portal sessions and Data Protection keys, deployment/domain/
TLS, real-person onboarding/privacy policy, Stripe sandbox operations,
production hosted storage, native proof, production enablement, and external
Git publication remain independent gates.
