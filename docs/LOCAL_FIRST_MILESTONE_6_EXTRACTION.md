# Local-first milestone 6 extraction manifest

## Exact outcome

Milestone 6 reconstructs one bounded provider-backed purchase and billing
management outcome:

**Sign in as an organization Owner → choose one server-approved offering in
Plan and storage → continue to Stripe-hosted Checkout → return with access
still pending → accept and reconcile signed provider events into ShowVault's
license/subscription projection → refresh the normalized plan → open a
short-lived Stripe Billing Portal session**

ShowVault never collects card details. A browser redirect, client claim, email,
or Checkout completion page never grants entitlement. Only a verified,
durably received event followed by current-provider-state reconciliation may
update the milestone-5 projection. Existing hosted reservations and receipts
remain recoverable after later cancellation, delinquency, refund, or dispute.

This is an extraction and architecture artifact only. It authorizes no source
implementation, dependency/plugin installation, Stripe account or API action,
product/price/customer creation, webhook registration, secret or credential,
payment/customer data, checkout, deployment, cloud mutation, native action,
external Git action, or destructive cleanup.

## Historical source accounting

Extract behavior from exactly two historical commits, in this order:

| Commit | Historical concern | Disposition |
| --- | --- | --- |
| `eea1d45` | Account/billing/admin target architecture embedded in a mixed navigation and personal-beta implementation | Retain provider-hosted payment surfaces, separate one-time license and recurring subscription, stable internal plan codes, signed webhook authority, password/payment-data boundaries, and non-deletion lifecycle rules. Replace the broad multi-surface implementation order with one Owner purchase/portal funnel against milestone 5. |
| `ce5be25` | Handoff that preserved the recommended Stripe one-time-plus-recurring model and account-surface boundary | Retain its explicit separation of desktop, customer billing portal, private staff Admin, and personal-beta exclusion; discard its superseded direct-to-cloud and old verification context. |

The commits are historical provenance, not a replay series. Their selected
source has 13 unique paths, all of which overlap the current tree and require
reconciliation. Summed per-commit statistics are 515 insertions and 49
deletions. Their concatenated binary patches have SHA-256
`71f162c534697dd9c0fdb9cb0028afd51cf63593e7af999fab440e80947b8746`;
their sorted unique path list has SHA-256
`b589c5bace5be9a121fcbedba340d167d05867f8dc14bf7f69faf3ba3782de54`.

Reproduce the accounting:

```bash
for commit in \
  eea1d45543aaafd014ceeac32a38675bdf742188 \
  ce5be252fee9564b91872b9a6286d52f3f4d9e10; do
  git show --format= --binary "$commit"
done | shasum -a 256

for commit in \
  eea1d45543aaafd014ceeac32a38675bdf742188 \
  ce5be252fee9564b91872b9a6286d52f3f4d9e10; do
  git show --format= --name-only "$commit"
done | sed '/^$/d' | sort -u | shasum -a 256
```

## Current authority and surface boundary

Milestone 5 remains authorization authority. Stripe facts are inputs to the
server-owned `CommercialLicense` and `ServiceSubscription` projections; the
desktop never evaluates a provider status. Keep provider IDs, event state,
price mappings, and reconciliation cursors in a separate server-only billing
layer. Do not add them to the Owner plan response or local SQLite.

Extend the current Flutter **Plan and storage** section rather than creating a
second desktop application or a ShowVault staff console. An Owner without a
provider binding may choose one internally coded, server-allowlisted offering
and select **Continue to secure checkout**. An Owner with a binding may select
**Manage billing**. Both actions request a new server-created hosted session
and open only the returned HTTPS URL. The URL is ephemeral and never persisted
or logged.

This slice's “customer portal” is Stripe's hosted Billing Portal entered from
ShowVault Settings. A full ShowVault-owned account website, signup,
membership/role administration, installations, support Admin, and staff
financial actions remain later work.

## Checkout contract

Add Owner-only authenticated POST operations beneath the exact organization
for Checkout and Billing Portal sessions. Missing subject returns 401;
nonmember, non-Owner, or wrong-tenant returns 403 before provider state is
disclosed. The personal-beta identity cannot create a billing session.

The Checkout request contains only a stable internal offering code from a
closed server catalog. The server—not the client—maps it to exactly one
recurring Stripe Price and one one-time license Price, quantity one, in a
subscription-mode hosted Checkout Session. Exact products, Price IDs,
amounts, currency, taxes, payment methods, and production limits are separately
configured Product Owner/operations decisions and never client input.

Create a durable purchase attempt before calling Stripe. Its server-generated
attempt ID supplies the provider idempotency key and opaque organization/
offering correlation. Concurrent/repeated requests return the same unexpired
open attempt instead of creating multiple Customers or subscriptions. Persist
the provider session ID and status, not its URL. If provider creation fails,
retain a bounded retryable attempt without changing entitlement.

Stripe may create the Customer during Checkout. Organization UUID and offering
code may be sent as bounded non-PII correlation metadata, but metadata alone is
never authority: reconciliation must match the signed event's provider session
to the preexisting attempt and exact allowlisted line items. The success and
cancel return URLs are fixed HTTPS server configuration; they are never
accepted from the client. A success return shows **Payment processing—refresh
plan status** and grants nothing.

Stripe documents that subscription-mode Checkout supports recurring and
one-time Prices, with one-time items appearing only on the initial invoice. It
also directs integrations to perform fulfillment from webhooks and check
payment status, not from the return page:

- <https://docs.stripe.com/api/checkout/sessions/object>
- <https://docs.stripe.com/checkout/fulfillment>

## Webhook inbox and reconciliation

Expose one unauthenticated provider webhook route outside normal JSON model
binding. Bound the content type, raw body size, signature-header size, and
processing time. Verify the `Stripe-Signature` over the exact raw bytes with a
separate environment-specific endpoint secret and bounded timestamp tolerance
before JSON parsing or any durable change. Invalid, missing, stale, oversized,
or wrong-environment signatures return 400 and create no commercial state.

After verification, atomically insert a minimal inbox receipt keyed by provider
environment plus event ID. Record only event ID, allowlisted type, primary
object ID, provider-created time, API version, payload digest, received/
processed timestamps, bounded outcome/reason, and correlation. Never persist
the raw event, Checkout/Portal URL, email, address, tax ID, card/payment-method
data, invoice document, token, secret, or provider request/response body.
Duplicates, including concurrent delivery, return 2xx after locating the same
receipt and never apply a projection twice.

Webhook delivery order is not guaranteed. The handler must durably accept the
receipt quickly, then a bounded reconciliation worker uses the server-side
provider adapter to retrieve current Checkout Session, line items, Customer,
Subscription, initial Invoice/payment, and refund/dispute state as needed. It
maps that current state transactionally to the existing ShowVault projections
with an object revision/cursor, making stale/out-of-order events no-ops. It
never constructs authority from event arrival order or raw event fields alone.

Listen only for the exact event families required to wake reconciliation:

- Checkout completion and asynchronous payment success/failure;
- subscription created/updated/deleted/paused/resumed;
- invoice paid and payment failed; and
- refund and dispute changes affecting the initial license payment.

Unknown but validly signed events receive a bounded ignored receipt and 2xx.
Stripe explicitly requires the raw body for signature verification, warns that
event order is not guaranteed, and recommends deduplicating event IDs:
<https://docs.stripe.com/webhooks>.

## Projection and financial lifecycle

The provider/customer/session/subscription/invoice binding is organization
unique and environment-bound. A provider object may never bind to two
organizations. Sandbox and live IDs/events/keys/databases cannot cross.

Checkout completion initially leaves the license `pending`. Activate the
one-time license only after reconciliation proves the exact allowlisted initial
invoice was paid and contains the one-time license Price. Map the provider's
current subscription state explicitly; expand the normalized subscription enum
if needed rather than collapsing `incomplete` or `unpaid` into a misleading
active/paused value. Entitlement remains the milestone-5 evaluator over the
normalized records.

For the first slice, `past_due` has no implicit grace: `GraceEndsAt` is null and
new hosted reservations deny closed. A nonzero grace policy requires a later
explicit Product Owner decision. Cancel-at-period-end remains active only while
the reconciled provider subscription remains active and exposes its current
period end; final cancellation denies new reservations.

A full refund of the license payment maps the license to `refunded`. A partial
or ambiguous refund, dispute, object mismatch, unsupported provider status, or
reconciliation conflict maps to bounded billing attention and denies new
reservations until attended reconciliation. A dispute must not leave new
service authorized merely because a prior event said paid. No commercial
change deletes local/hosted backups, releases quota, or blocks continuation and
receipt access for an already reserved session.

Stripe recommends subscription lifecycle handling through verified webhook
events and identifies `invoice.paid`, payment failure, and subscription changes
as the relevant asynchronous state:
<https://docs.stripe.com/billing/subscriptions/webhooks>.

## Billing Portal and secret boundary

The Owner-only Portal endpoint requires the exact organization's existing
environment-matched Customer binding. The server supplies the Customer ID and
a fixed HTTPS return URL; neither is client-selected. Each action creates a new
session and returns its short-lived URL without persisting or logging it. No
ShowVault surface retrieves or renders cards, bank data, invoices, tax IDs, or
provider credentials. Stripe documents that portal sessions are created on
demand from the Customer ID and return a short-lived URL:
<https://docs.stripe.com/customer-management/integrate-customer-portal>.

API and webhook secrets are separate, server-only, environment-specific secret
manager values. Prefer a restricted API key with only the calls the provider
adapter requires, where the provider supports the necessary restriction.
Support webhook-secret rotation with an explicitly bounded overlap. No key or
secret appears in source, checked-in configuration, desktop code, database,
logs, errors, tests, or audit. Stripe's key boundary is documented at
<https://docs.stripe.com/keys>.

## Audit and privacy

Append minimized ShowVault audit events for Owner session requests, provider
session creation outcome, verified event receipt/outcome, projection transition,
and reconciliation attention. Store organization, actor subject only for the
Owner action, bounded action/outcome/reason, internal offering/policy version,
provider environment, opaque provider object suffix or digest rather than full
financial payload, correlation, and timestamps. Webhook events have no human
actor. Never copy raw provider objects into `CommercialAuditEvent`.

## Provider seam and fixtures

Use a narrow `IBillingProvider` adapter for Checkout/Portal creation and
current-state retrieval, plus a separate signature verifier. Unit and API tests
use deterministic synthetic adapters and locally generated signed raw payloads;
they perform no provider network call. The production adapter and route remain
disabled when any required catalog, key, endpoint secret, fixed return origin,
environment, or provider API-version pin is absent or inconsistent.

Do not install the Stripe plugin, Stripe CLI, SDK, create a Stripe account,
product, Price, Customer, Checkout Session, webhook endpoint, portal
configuration, sandbox/live key, or secret during planning. Those are
implementation/operations actions requiring separate authorization.

## Explicit non-goals

Milestone 6 does not implement or claim:

- a ShowVault-owned customer web portal, signup, invitations, role changes,
  installation management, internal staff Admin, refunds, dispute operations,
  credits, coupons, custom invoices, or support impersonation;
- final prices, currencies, taxes, trials, discounts, payment-method policy,
  grace duration, retention/export/deletion, or accounting recognition;
- direct card collection, embedded Elements, mobile wallet setup, or storage of
  payment details;
- production provider-account provisioning, live charges, real Customer data,
  endpoint registration, deployment, reconciliation operations, or secret
  rotation proof;
- production object-storage durability, remote deletion/quota reclamation; or
- native installation, signing, equipment, or venue proof.

## Reconstruction sequence

1. Add provider-neutral binding, purchase-attempt, event-inbox, projection
   cursor, billing-attention, and minimized audit types with restrictive
   persistence and concurrency rules.
2. Add the disabled-by-default provider adapter/signature seams and exact
   environment/catalog/URL configuration validation.
3. Add Owner-only idempotent Checkout and Portal session endpoints with fixed
   return origins and ephemeral URLs.
4. Add the raw-body webhook endpoint, signature verification, minimal durable
   inbox, duplicate handling, and bounded asynchronous reconciliation.
5. Map reconciled provider state transactionally into milestone-5 projections
   without deleting or stranding recovery state.
6. Extend Flutter Plan and storage with external-checkout/portal consent,
   pending refresh, URL safety, non-Owner exclusion, and no financial detail.
7. Add synthetic signed lifecycle fixtures and reconcile documentation. A
   separately authorized sandbox operational proof comes only afterward.

## Verification gate

Implementation authorization, if separately granted, must prove at minimum:

- Owner-only exact-tenant Checkout/Portal actions; personal-beta, non-Owner,
  outsider, cross-tenant, route-ID, offering-code, and provider-ID attacks fail;
- durable provider idempotency across retry/restart/concurrent clicks; exact
  allowlisted prices/quantity/mode; fixed return URLs; ephemeral safe URLs;
- raw-body signature validity, invalid/stale/missing/wrong-secret/wrong-mode/
  oversized rejection, secret rotation, API-version pin, and no state before
  verification;
- duplicate/concurrent and out-of-order delivery, crash after inbox receipt,
  retry/dead-letter attention, current-state reconciliation, stale cursor, and
  provider outage recovery;
- initial paid invoice activation, async payment, incomplete/unpaid/past-due,
  cancel-at-period-end/final cancellation, full/partial refund, dispute, and
  unsupported-state fail-closed matrices;
- redirect completion alone never activates; provider metadata alone never
  binds; one provider object cannot cross organizations or environments;
- no raw payload, email/address/tax/payment/card/invoice document, Checkout or
  Portal URL, key, secret, token, path, filename, manifest, or backup content in
  database, logs, errors, audit, desktop, or local SQLite;
- milestone-5 quota and existing-session recovery plus signed-out/offline local
  Save/inspect/Restore remain unchanged; and
- complete .NET/Flutter suites, EF model gate, zero-warning Release builds,
  formatting, `git diff --check`, and repository secret/path audits.

Synthetic tests will not establish real payment correctness, provider sandbox
or live readiness, tax/accounting correctness, webhook reachability, production
operations, native correctness, or venue readiness.
