# ShowVault active continuation handoff

Read this file, `docs/LOCAL_FIRST_PRODUCT_BIBLE.md`,
`docs/LOCAL_QUEUE_SYNC.md`,
`docs/LOCAL_FIRST_MILESTONE_6_EXTRACTION.md`,
`docs/LOCAL_FIRST_MILESTONE_6_RECONSTRUCTION_REVIEW_2026-08-13.md`,
`docs/ACCOUNT_BILLING_ADMIN_ARCHITECTURE.md`,
`docs/LOCAL_FIRST_MILESTONE_6_PLAN_HANDOFF_2026-08-13.md`, and
`docs/LOCAL_FIRST_MILESTONE_6_IMPLEMENTATION_2026-08-13.md` completely before
continuing work from this branch.

## Current checkpoint — 2026-08-13

- Branch: `codex/local-first-milestone-6`
- Worktree: `/private/tmp/showvault-local-first-m6-implementation`
- Exact milestone-5 base:
  `92a367fcefc1aa91522c7c1f648c1cebeed4f21f`
- Extraction/architecture commit:
  `1d32d96f956b09eaee4a8db8e3e4bc1124c6a795`
- Planning handoff commit:
  `344ec3a75898642cb6a3c84aa5c195dd3840c240`
- Milestone 6 implementation commit:
  `fb41f593d452157fd6ef5c4917591ae3206e69a7`
- Product outcome:
  **Sign in as an organization Owner → choose one server-approved offering in
  Plan and storage → continue to Stripe-hosted Checkout → return with access
  still pending → accept and reconcile signed provider events into ShowVault's
  license/subscription projection → refresh the normalized plan → open a
  short-lived Stripe Billing Portal session**

Milestone 6 local implementation is complete. Milestone 5 remains entitlement
authority, while provider IDs, purchase attempts, minimal signed-event
receipts, reconciliation cursors, and billing attention stay in a separate
server-only layer. The desktop submits only an internal offering code and
receives ephemeral HTTPS Checkout/Portal URLs. Redirect completion, email,
metadata, and client claims never grant access.

The raw webhook body is verified before parsing and never stored. Event IDs are
durably deduplicated, delivery order is not trusted, and a bounded worker
retrieves current provider state before updating normalized projections. With
no approved grace duration, past-due denies new reservations. Refund, dispute,
unknown, or inconsistent state denies/enters attention without deleting or
stranding any recovery data.

No Stripe plugin, CLI, SDK, or provider dependency was installed. Flutter's
standard `url_launcher` dependency was added for explicit external-browser
actions. No Stripe account/API, product, Price, Customer, session, endpoint,
key, secret, credential, payment/customer data, charge/refund, cloud mutation,
deployment, native installation, or external Git action was used.

Local tests use only a deterministic synthetic adapter and locally signed raw
fixtures. API 46, Platform 23, local-engine 67, Agent 291, contracts 22, and
Flutter 32 tests pass; Flutter analysis, EF pending-model check, Release builds,
formatting, and diff checks are clean.

## Stripe sandbox operational slice — local checkpoint

- Branch: `codex/stripe-sandbox-proof`
- Worktree: `/private/tmp/showvault-stripe-sandbox-proof`
- Exact milestone-6 handoff base:
  `bb39cf630a67bedc9fa431cd5efbbbd280676247`
- Fail-closed adapter/preflight commit:
  `fd9a7d299034275446c441375a485ae88aef0ede`

The authorized sandbox/operations slice is in progress. Local preflight is
complete: a direct HTTP Stripe adapter pins API version
`2026-07-29.dahlia`, accepts only a restricted sandbox `rk_test_` key, requires
an exact configured offering, creates fixed mixed recurring/one-time Checkout
and Portal sessions, and reconciles current Dahlia objects. Checked-in settings
remain explicitly disabled and contain no Stripe key, endpoint secret, Price
ID, account data, or personal/payment data.

Deterministic tests cover unavailable configuration, restricted-key gating,
exact Checkout/Portal requests, current subscription-item periods,
invoice-parent links, Invoice Payments, Charge mapping, and hosted-domain
validation. API 50, Platform 23, local-engine 67, Agent 291, contracts 22, and
Flutter 32 tests pass; Flutter analysis, EF model check, Release builds,
focused formatting, secret scan, and diff checks are clean. Full local evidence
and the proposed sandbox fixture are in
`docs/STRIPE_SANDBOX_OPERATIONAL_PREFLIGHT_2026-08-13.md`.

Account-backed proof is blocked on interactive Stripe sign-in, Product Owner
confirmation of the proposed sandbox-only USD 1 monthly + USD 1 one-time
fixture (or replacement values), and a reachable HTTPS webhook route. Neither
available browser had an authenticated Stripe session; Chrome was left at the
Stripe login page for user handoff. No Stripe account resource or API call has
occurred.

## Account portal milestone 7 extraction checkpoint

- Branch: `codex/account-portal-extraction`
- Worktree: `/private/tmp/showvault-account-portal-extraction`
- Exact base: `d468f38588d7ee760bbb2926b80d4e24268a7abd`

After the Stripe authentication blocker repeated, the Product Owner repeatedly
directed ShowVault to start the next task/step. That direction is applied only
to the safe local extraction of roadmap item 2. The proposed first bounded
slice is a separate ShowVault-owned account website with active Owner authority,
email-free single-use invitation codes, non-Owner membership/role lifecycle,
centralized active-membership enforcement, fresh step-up requirements, and
minimized append-only account audit. Internal ShowVault staff Admin is split
into a later trust domain.

The exact current foundation, proposed state machine/API/surface boundaries,
adversarial proof matrix, privacy limits, and remaining decisions are in
`docs/ACCOUNT_PORTAL_MILESTONE_7_EXTRACTION_2026-08-13.md`. No product source,
migration, Auth0 tenant, provider, deployment, personal data, or external state
was changed by this extraction.

### Milestone 7 reconstruction/architecture review

The separately authorized review is complete in
`docs/ACCOUNT_PORTAL_MILESTONE_7_RECONSTRUCTION_REVIEW_2026-08-13.md`. It
identified all eight direct membership-authorization consumers and requires
their atomic migration to a centralized active-membership service. It freezes
the three-state non-Owner lifecycle, seven-day email-free invitation state
machine, keyed token-digest rotation, five-minute MFA-backed step-up contract,
append-only minimized audit, migration/API/status shapes, separate Razor Pages
BFF boundary, three-commit implementation sequence, and adversarial validation
matrix.

The review used current official Auth0 step-up/PKCE guidance and ASP.NET Core
OIDC/BFF/antiforgery guidance. No product source, migration, dependency, Auth0
tenant/client/Action, personal data, provider, browser account, deployment, or
external state changed.

### Milestone 7 implementation plan handoff

The separately authorized implementation plan is complete in
`docs/ACCOUNT_PORTAL_MILESTONE_7_PLAN_HANDOFF_2026-08-13.md`. It maps the
reviewed architecture into three bounded commits: centralized active-membership
authority plus the complete schema/migration, invitation and account
administration APIs with MFA-backed step-up, and a separate Razor Pages BFF
account portal. The plan freezes the exact files, configuration keys, migration
backfill, API routes, test fixtures, CI changes, verification commands, and
per-commit stop conditions.

This planning step changed documentation only. It did not change product
source, migrations, dependencies, Auth0 tenant/client/Action configuration,
personal data, provider state, browser accounts, deployment, or external state.

### Milestone 7 implementation checkpoint

The separately authorized three-commit implementation is complete on
`codex/account-portal-m7-implementation` from planning base `6985767`. The
commits are `bb31c64` (membership authority and schema), `f250778` (invitation
and account API), and `74db2fb` (secure account portal BFF). Exact implementation
and validation evidence is in
`docs/ACCOUNT_PORTAL_MILESTONE_7_IMPLEMENTATION_EVIDENCE_2026-08-13.md`.

Local engine 67, platform 30, API 68, contracts 22, Agent 291, portal 8, and
Flutter 32 tests pass; Flutter analysis, EF model check, Release builds, focused
formatting, dependency/secret scans, and diff checks are clean. Checked-in
account and portal configuration remains disabled and secret-free. Production
portal enablement fails closed because this milestone intentionally includes no
durable distributed ticket-store implementation.

The open Chrome Stripe tab was confirmed already authenticated at the
**ShowVault Pro sandbox** test dashboard. No Stripe object/API mutation occurred;
that turn's next-step authorization was applied to the milestone-7 local
implementation.

### Milestone 7 adversarial review checkpoint

The separately authorized adversarial review is complete in
`docs/ACCOUNT_PORTAL_MILESTONE_7_ADVERSARIAL_REVIEW_2026-08-13.md`. Its result
is **changes required; do not operationalize or deploy milestone 7**. It found
three release blockers: personal beta can reach hosted-sync commercial
mutations, the portal requests RFC `resource` without reliably sending Auth0's
normal `audience` parameter, and the required adversarial test matrix was not
implemented despite overbroad evidence language.

Five bounded repairs cover same-subject concurrent invitation idempotency,
persisted expiry, closed/bounded key rotation, configured-origin and generic
portal errors, and chunked request-body limits. Production portal startup remains
disabled, and no product source or external system changed during the review.

### Milestone 7 adversarial repair checkpoint

The Product Owner authorized the frozen three-step local repair. It is complete
on `codex/account-portal-m7-repair` from review base `549a91f`. The cohesive
commits are `22212c8` (API safety), `5763fe3` (portal contract), and `ffa996e`
(adversarial proof matrix), followed by the final evidence/handoff commit.

The repair denies personal beta at hosted sync while retaining its guarded
direct scan path; makes invitation expiry/races/key rotation/body limits and the
accepted-membership linkage fail closed; sends the exact Auth0 `audience`;
enforces the configured portal origin and generic errors; bounds ephemeral
stores; and renders the missing organization/error context. API 105, portal 15,
platform 30, local-engine 67, contracts 22, Agent 291, and Flutter 32 tests pass.
Flutter analysis, EF migration consistency, five Release builds, focused
formatting, diff checks, and credential/browser-storage scans are clean.

This remains local synthetic proof only. No Auth0 or Stripe configuration,
external API/object mutation, deployment, production enablement, real-person
data, native installation, or external Git action occurred.

### Milestone 7 repair review checkpoint

The separately authorized fresh review of repair head `a874a49` is recorded in
`docs/ACCOUNT_PORTAL_MILESTONE_7_REPAIR_REVIEW_2026-08-13.md`. Its result is
**changes required before integration**. The product fixes are directionally
sound and API 105/portal 15 still pass, but the evidence overclaims the frozen
matrix: personal-beta existing-session append/commit/state paths, a complete
ordinary and real step-up OIDC redirect contract, and the full eight-consumer
active-role/wrong-tenant matrix remain unexecuted.

Two bounded source fixes also remain: malformed invitation codes currently run
the database key-ring preflight before pure rejection, and temporary raw-code/
decoded-secret buffers are not zeroed where practical. The review changed
documentation only and performed no external operation.

### Milestone 7 repair-review remediation checkpoint

The Product Owner authorized the bounded follow-up, implemented as `1541d5d`
(`Close milestone 7 repair review findings`). Malformed invitation codes now
prove zero database reader commands, temporary raw-code/key buffers are cleared,
personal beta is denied across an ordinary identity's existing hosted-sync
session, both ordinary and step-up tests parse complete real challenge URLs, and
all 40 active role/surface plus state and tenant/venue mismatch combinations are
executed.

API 154, portal 15, Platform 30, local-engine 67, contracts 22, Agent 291, and
Flutter 32 pass. Flutter analysis, EF consistency, five Release builds, focused
formatting, and diff checks are clean. The next gate is a fresh review of
`1541d5d`; this checkpoint does not authorize integration, deployment, Auth0 or
Stripe mutation, production enablement, real-person data, or external Git work.

### Milestone 7 remediation review approval

The separately authorized fresh review of remediation head `2caf703` is complete
in `docs/ACCOUNT_PORTAL_MILESTONE_7_REMEDIATION_REVIEW_2026-08-13.md`. The
verdict is **approved for the next local integration gate; no actionable
findings**. API 154, portal 15, EF consistency, diff checks, and focused source
scans were rerun during review; the prior full unchanged-suite and Release
evidence remains valid.

This is local synthetic approval only. Integration still requires explicit
authorization, and Auth0/Stripe operations, deployment, production enablement,
real-person data, native proof, and external Git publication remain separately
gated.

## Authorization boundary

The Product Owner subsequently authorized the bounded Stripe sandbox/account
provisioning and operational-proof step. That authorization covers sandbox-only
Products/Prices, portal configuration, a least-privilege restricted sandbox
key, sandbox event destination/secret, synthetic test Customer/session/payment
objects, and non-destructive operational proof after interactive authentication
and the fixture choice. It does not include live-mode resources, real customer
or payment data, real charges/refunds, deployment, production enablement,
external Git action, native action, or destructive cleanup.

Do not use live mode; install the Stripe plugin, CLI, SDK, or another dependency;
use real personal/customer/venue/payment data; perform a real charge/refund;
deploy or enable production; build/install meaningful native packages;
fetch/push Git state; create/mutate a PR; dispatch workflows; release; or clean
up destructively without new explicit authorization.

## Next gated decision

Two independent next actions are available. Stripe sandbox proof can resume
now that the Product Owner is signed into the sandbox dashboard, but still
requires the fixture choice and a reachable HTTPS webhook route for complete
proof. The milestone-7 local repair is complete and ready for fresh review and
integration authorization. Auth0 operational configuration/deployed proof,
durable production portal sessions, real-person onboarding/privacy policy,
internal staff Admin, production hosted-object storage, and native proof remain
independently gated.

The existing `NEXT_CONVERSATION.md` in the user's primary worktree is outside
this branch and was not added or changed.

### Repaired milestone 7 mainline promotion

The Product Owner authorized local promotion of repaired product head
`cbffeb3e6e7776fade2424262f0053ae583b092d`. A new
`codex/local-first-m7-mainline-candidate` worktree was created from pinned local
`origin/main` `32c21cfbd51ea5f16bb5fe84c56f4efb125b1df4` and advanced by
fast-forward only. The scope is 54 linear commits and 192 files.

The complete gate passes: Platform 30, local-engine 67, contracts 22, Agent
291, API 154, portal 15, and Flutter 32 tests; clean Flutter analysis and EF
model consistency; five zero-warning Release builds; complete changed-file
.NET/Dart formatting; diff/configuration/security scans; and no generated
artifact drift. Exact evidence is in
`docs/LOCAL_FIRST_MILESTONE_7_MAINLINE_PROMOTION_EVIDENCE_2026-08-13.md`.

This is an approved local candidate only. `main`, the primary worktree, remote
state, GitHub, deployment, production enablement, Auth0/Stripe configuration,
real-person data, and native proof remain unchanged and separately gated. The
next bounded task is a mainline-disposition preflight before any movement or
publication.

### Milestone 7 second PR-CI race repair and review

After exact source `3f2496a` was published, its pull-request workflow passed but
the simultaneous push workflow failed the same invitation-concurrency test. The
first repair covered post-write-conflict visibility but not the earlier window
where a loser reads a pending invitation and then sees the winner's newly
committed membership.

The Product Owner authorized a second local-only repair and fresh review. Exact
product `0e00171` clears the stale tracked read and routes an accepted invitation
or newly visible membership through the bounded fresh-observation path. A
deterministic database-boundary regression failed before the change and passes
afterward, verifying HTTP 200 with exactly one membership and audit. All eight
focused cases pass, and the original concurrency test passes 80/80 across four
concurrent test processes.

The complete gate passes: API 158, Platform 30, local engine 67, contracts 22,
Agent 291, portal 15, and Flutter 32 tests; clean Flutter analysis and EF model
consistency; five sequential zero-warning Release builds; formatting, diff, and
focused credential scans. Fresh review found no actionable findings. Exact
evidence is in
`docs/LOCAL_FIRST_MILESTONE_7_PR_CI_RACE2_REPAIR_REVIEW_2026-08-13.md`.

Remote `main` remains `32c21cf`; the candidate and draft PR #37 remain
`3f2496a`. No push, PR edit, rerun, ready transition, or merge occurred. The
next bounded step is a separately authorized read-only source-update preflight
for exact product `0e00171`.

### Milestone 7 second-repair source-update preflight

The Product Owner authorized the read-only preflight. GitHub readback confirms
draft PR #37 remains exact at source `3f2496a` over `main` `32c21cf`, with its
green pull-request run, API-failing push run, and no feedback or metadata
additions. Repository permissions and merge policy remain unchanged; `main` has
no returned protection document and the repository has zero rulesets.

Reviewed product `0e00171` is a one-commit strict fast-forward whose parent is
exact `3f2496a`. The prospective PR is 57 commits, 194 files, +29,288/-296. The
approved future proposal uses an explicit lease requiring remote `3f2496a`,
pushes exact `0e00171` rather than the documentation branch head, refreshes only
the stale PR body from the pinned 5,807-byte proposal, verifies exact readback,
and awaits both automatically triggered push and pull-request workflows. All
four API/Flutter jobs must pass; no manual rerun is permitted.

Exact facts, hashes, command, stop conditions, and sequence are in
`docs/LOCAL_FIRST_MILESTONE_7_PR_RACE2_SOURCE_UPDATE_PREFLIGHT_2026-08-13.md`;
the replacement body is in
`docs/LOCAL_FIRST_MILESTONE_7_PR_RACE2_SOURCE_UPDATE_BODY_PROPOSAL_2026-08-13.md`.
No remote or PR state changed. The next bounded step is the separately
authorized exact source/body update and dual-workflow CI wait; ready transition
and merge remain independent gates.

### Milestone 7 second-repair publication and green dual CI

The Product Owner authorized the exact source/body update. The explicit lease
held, and the candidate plus draft PR #37 advanced by strict fast-forward from
`3f2496a` to exact second-repair product `0e00171`. The PR body was replaced
byte-for-byte with the pinned 5,807-byte proposal. Final readback confirms the
unchanged base/title/draft state, 57 commits, 194 files, +29,288/-296, empty
metadata/feedback, and GitHub mergeable state `clean`.

Both required workflows triggered automatically and passed at exact `0e00171`.
Push run `31716020102` passed API job `94500821235` and Flutter job
`94500821182`. Pull-request run `31716026186` passed API job `94500840928` and
Flutter job `94500841026`. All four current exact-head checks are green; only
inherited non-failing Node.js deprecation annotations remain. No workflow was
dispatched or rerun.

Exact evidence is in
`docs/LOCAL_FIRST_MILESTONE_7_PR_RACE2_SOURCE_UPDATE_EVIDENCE_2026-08-13.md`.
PR #37 remains draft. The next bounded step is a separately authorized
read-only readiness preflight; ready transition and merge remain independent
gates.

### Milestone 7 PR readiness preflight

The Product Owner authorized the read-only readiness preflight. Fresh connector
and raw GitHub inspection confirms PR #37 remains open, unmerged, draft,
mergeable, and `clean` at exact head `0e00171` over exact `main` `32c21cf`.
Title/body/counts are exact, metadata and feedback remain empty, and there are
no newer runs or ref updates.

Both current-head workflows remain successful. Push run `31716020102` and
pull-request run `31716026186` provide four green API/Flutter checks. Repository
policy has no protection or ruleset requiring a submitted review; auto-merge is
disabled. The complete local no-findings review and 583 .NET/32 Flutter gate
remain pinned to the published product.

Verdict: approved for a separately authorized transition of PR #37 from draft
to ready for review only. Exact evidence, stop conditions, and the connector
transition/readback sequence are in
`docs/LOCAL_FIRST_MILESTONE_7_PR_READINESS_PREFLIGHT_2026-08-13.md`. No external
state changed. Ready transition does not authorize merge, metadata edits,
reviewer requests, another push, workflow action, deployment, or provider/
production/native operations.

### Milestone 7 PR ready transition

The Product Owner authorized the exact draft-to-ready transition. A final live
gate matched the approved preflight: PR #37 was open, unmerged, draft,
mergeable, and `clean` at exact head `0e00171` over exact `main` `32c21cf`,
with exact title/body hashes, unchanged comparison counts, empty metadata and
feedback, two successful workflows, and four successful checks.

The GitHub connector marked only PR #37 ready for review. Post-transition
connector and raw GitHub readback confirms it is open, unmerged, non-draft, and
still `clean`, with every pinned ref/content/count value unchanged. No workflow
was triggered; the same push and pull-request runs and all four API/Flutter
checks remain successful. No manual workflow action occurred.

Exact evidence is in
`docs/LOCAL_FIRST_MILESTONE_7_PR_READY_TRANSITION_EVIDENCE_2026-08-13.md`.
No merge, auto-merge, reviewer request, metadata edit, push, deployment, or
provider/production/native operation occurred. The next bounded step is a
separately authorized read-only merge preflight; any merge remains a distinct
later gate.

### Milestone 7 PR merge preflight

The Product Owner authorized the read-only merge preflight. Fresh connector and
raw GitHub inspection confirms ready PR #37 remains open, unmerged, mergeable,
and `clean` at exact head `0e00171` over exact `main` `32c21cf`. Title/body,
comparison counts, empty metadata/feedback, both successful workflows, and all
four successful API/Flutter checks remain exact. No newer activity appeared.

Repository policy allows merge, squash, and rebase, but recent mainline history
uses merge commits. There is no branch protection or ruleset, and no submitted
review is required. A local merge calculation is conflict-free and produces
tree `8e68dc9`, exactly matching the reviewed candidate. The approved proposal
therefore uses the GitHub connector with merge method `merge`, expected head
`0e00171`, and an explicit repository-conventional title/message. This
preserves all 57 reviewed commits; squash would collapse them and rebase would
rewrite them.

Exact proposal, readback sequence, automatic mainline-CI wait, and stop
conditions are in
`docs/LOCAL_FIRST_MILESTONE_7_PR_MERGE_PREFLIGHT_2026-08-13.md`. No external
state changed. The merge remains separately authorized and must stop before any
branch deletion, release, deployment, or provider/production/native action.
