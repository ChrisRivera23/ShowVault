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

Ask the Product Owner to sign in through the handed-off Chrome Stripe tab and
confirm or replace the proposed sandbox fixture. Then resume the already
authorized sandbox provisioning. A public webhook proof also requires either a
separately authorized deployment or an explicitly approved Stripe CLI install
and forwarding session. A ShowVault-owned account website, membership/role
administration, internal staff Admin, production hosted-object storage, and
native proof remain independently gated.

The existing `NEXT_CONVERSATION.md` in the user's primary worktree is outside
this branch and was not added or changed.
