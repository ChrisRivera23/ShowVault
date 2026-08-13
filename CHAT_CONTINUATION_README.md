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

## Authorization boundary

Milestone 6 implementation authorization has been consumed. This checkpoint
authorizes no provider/account operation, credential or payment/customer data,
cloud action, native action, external Git action, deployment, or destructive
cleanup.

Do not install the Stripe plugin, CLI, SDK, or another dependency; create or
access provider resources, register webhooks, retrieve artifacts, use
credentials or personal/customer/
venue/payment data, mutate cloud state, build/install meaningful native
packages, fetch/push Git state, create/mutate a PR, dispatch workflows,
charge/refund, release, deploy, or clean up destructively without new explicit
authorization.

## Next gated decision

Stop for Product Owner direction. The next bounded action is Stripe
sandbox/account provisioning and operational proof against the disabled
provider seam. It requires separate explicit authorization. A ShowVault-owned
account website, membership/role
administration, internal staff Admin, production hosted-object storage, and
native proof remain independently gated.

The existing `NEXT_CONVERSATION.md` in the user's primary worktree is outside
this branch and was not added or changed.
