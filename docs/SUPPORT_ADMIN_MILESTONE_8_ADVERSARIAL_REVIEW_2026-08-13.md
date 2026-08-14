# Support Admin milestone 8 adversarial plan review — 2026-08-13

## Verdict

Verdict: **approve after documentation repair; do not implement yet.**

The roadmap reconciliation is correct and the proposed scope remains the
smallest defensible Support slice. Six material precision defects in plan
commit `7237f0f157703494357394cfef528b03d8cd6d97` were repaired in the reviewed
plan. This review changes documentation only and leaves all product source,
migrations, tests, applications, GitHub state, workflows, providers, and
worktrees untouched.

## Exact review input

- Product-tree base: `2dfb4cd82b6ca3cf1ef3928f73c8fe00e194b0a5`.
- Product tree/current-main tree:
  `fea87b4dc7492a5187dcd60cc618ddff77b067db`.
- Plan commit: `7237f0f157703494357394cfef528b03d8cd6d97`.
- Plan tree: `06bd6034df28aaf99c7bdfad1609272cc5057923`.
- Input diff: `docs/ROADMAP.md` plus
  `docs/SUPPORT_ADMIN_MILESTONE_8_EXTRACTION_AND_PLAN_2026-08-13.md` only.
- Input sorted path-list SHA-256:
  `b92093c649236dca3268eb18f2bd0499b8767802e0c111ac2071ed380403a7b5`.
- Input binary-diff SHA-256:
  `d6a9f71f751dceeb1566e87ae1d522782f40ff8d8a0887201ee68ae67ce5a4df`.

The worktree was clean at the exact plan commit before review edits.

## Findings and repairs

### 1. Organization ID contradicted the logging boundary — repaired

The plan put the organization ID in a GET route while also requiring that the
ID never enter logs. Default browser history, reverse-proxy access logs, and
request-path telemetry can retain route values even when application logging is
careful.

The contract is now one strict, non-cacheable POST query with the organization
ID as the only bounded JSON field. The BFF posts server-side and renders the
response directly. Unknown/duplicate fields, wrong content type, empty IDs, and
oversized bodies deny. Body logging is disabled and no redirect/result store is
introduced.

### 2. Sequential target and grant lookup created an oracle — repaired

Resolving organization existence before resolving the staff grant could expose
existence through query shape, timing, or divergent errors despite equal final
status codes.

The plan now requires one joined active-grant/organization lookup, one uniform
`support_target_unavailable` outcome for unknown, ungranted, and revoked-grant
targets, and explicit equality across status, body, headers, durable reason,
query count, and bounded timing class. Arbitrary requested organization IDs are
not written to durable audit.

### 3. Revocation race lacked an atomic decision — repaired

The earlier plan could authorize, project, then observe a concurrent assignment
or grant revocation while still returning data.

The reviewed decision now resolves the active staff assignment inside the same
serializable transaction as the joined grant/organization lookup, projection,
mandatory audit, and commit, with bounded conflict retry. Conflict/retry
exhaustion returns no overview. A response is released only after the evidence
commits.

### 4. Staff subject was not issuer-bound — repaired

A subject alone is not a complete durable identity key across issuer changes or
multiple identity planes. The staff assignment now binds normalized immutable
issuer plus subject, the Support bearer scheme validates an exact HTTPS issuer
and distinct audience, and uniqueness/tests cover issuer-subject collisions.

### 5. Hosted-sync grouping was underspecified — repaired

`HostedSyncSession.Status` is currently a string with actual transitions only
between `uploading` and `completed`; it is not a closed enum or database check.
The response plan now maps only those two exact values and fails the entire
projection on any unknown value. Latest activity is explicitly `UpdatedAt`.

### 6. Disabled behavior and response cardinality were not frozen — repaired

“Bounded” without exact values leaves privacy and denial behavior to
implementation judgment, while a partially configured staff scheme can expose
an unintended route surface.

The plan now freezes a 15-cell member matrix, at most eight distinct sorted
billing-attention reasons, exactly two hosted-sync buckets, checked 64-bit
counts, and whole-response failure on overflow/excess. Checked-in configuration
is disabled with no authority/audience, Support API routes are absent while
disabled, the BFF returns only a generic disabled `503`, and any enabled but
incomplete or non-HTTPS/non-distinct identity configuration fails startup.

## Boundaries that passed review

- Customer authentication, customer membership, customer Owner role, route
  values, JWT roles, email domains, and portal cookies never grant Support
  authority.
- Support uses a distinct scheme/audience, exact issuer, exact read scope,
  fresh MFA, active server-owned `SupportReader`, and an explicit active
  organization grant.
- The only response is a closed aggregate projection. It contains no identity
  subjects, member/target IDs, invitations, provider IDs/payloads, payment
  details, credentials, correlation IDs, paths, filenames, manifests, backup
  contents, restore contents, or signed URLs.
- Audit is mandatory, append-only, minimized, and committed before disclosure.
  Pre-trust authentication failures remain bounded security telemetry rather
  than attacker-created durable audit rows.
- The Support BFF remains disabled by default and separate from the customer
  portal's OIDC client, cookies, ticket namespace, pages, and trust domain.
- Assignments/grants are synthetic-fixture-only; there is no staff provisioning
  API, search, impersonation, export, customer mutation, financial action,
  provider action, deletion, or production enablement.
- The staged commits preserve migration-first authority, API isolation, BFF
  isolation, and complete adversarial/regression evidence before handoff.

## Remaining implementation gate

No unresolved plan-level trust, authorization, privacy, audit, or scope blocker
remains after these repairs. Runtime implementation still requires fresh
explicit authorization and must follow the repaired plan commit exactly.

Before implementation, re-read the repaired plan head and confirm the worktree
is clean. Then execute only staged commit 1 (staff authority, grants, immutable
evidence) first, validate its domain/migration boundaries, record a local
commit, and stop for review. Do not combine all four implementation stages into
one authorization.

No authorization here extends to GitHub mutation, fetch/push, workflow rerun,
cleanup, deployment, release, identity/provider/production operations,
Keychain-value access, real-person/customer/venue data, or native operations.
