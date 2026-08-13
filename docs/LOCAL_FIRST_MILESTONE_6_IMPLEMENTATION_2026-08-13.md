# Local-first milestone 6 implementation — 2026-08-13

## Result

Milestone 6 implements the authorized local provider-billing contract:

**Sign in as an organization Owner → choose one server-approved offering in
Plan and storage → continue to provider-hosted Checkout → return with access
still pending → durably accept a locally signed provider event → reconcile
current provider state into ShowVault's license/subscription projection →
refresh normalized plan → open a short-lived Billing Portal session**

The production provider and catalog remain disabled. Local proof uses only a
deterministic synthetic provider and locally generated HMAC-SHA256 webhook
fixtures. No Stripe SDK, plugin, CLI, account, API, product, Price, Customer,
session, endpoint registration, key, secret, payment/customer data, charge,
refund, deployment, cloud mutation, native installation, or external Git
action was used.

## Server-only billing boundary

Migrations `20260813071436_AddProviderBilling` and
`20260813072248_EnforceBillingInvoiceBinding` add restrictive server-only tables
for organization/environment Customer bindings, durable purchase attempts,
minimal event receipts, and attended reconciliation state. Unique indexes
prevent an active organization attempt or provider Customer, session,
subscription, or invoice from being bound twice. Revisions are concurrency
tokens. Provider IDs and financial state do not enter the public plan
projection or desktop persistence.

Checkout creation accepts only the closed internal offering code. The server
selects the recurring and one-time license Price IDs, fixed HTTPS return URLs,
provider environment, and API-version pin. It persists a UUIDv7 attempt before
calling the adapter and uses that attempt as the idempotency key. Concurrent
or repeated requests converge on one open attempt and provider session. Only
the provider session ID is stored; the HTTPS URL is validated, returned, and
discarded.

Portal creation requires the exact organization's environment-matched Customer
binding and a server-fixed return URL. Checkout and Portal operations are exact
tenant Owner-only and explicitly reject the personal-beta authentication type.
Their minimized audit events contain no hosted URL or provider payload.

## Signed event inbox and reconciliation

The webhook route bounds content type, body size, and signature-header size.
It verifies Stripe's `t.payload` HMAC over the exact raw bytes against one or
two rotation-overlap fixture secrets and a bounded timestamp before parsing.
Wrong, stale, oversized, malformed, or environment-mismatched input creates no
receipt.

Valid input stores only event/type/object identifiers, provider/API times,
SHA-256 payload digest, processing state, bounded outcome, and later
organization correlation. Raw JSON and financial/customer fields are never
stored. The environment-plus-event unique key makes sequential and concurrent
duplicates 2xx no-ops. Unknown signed events are recorded as ignored.

The HTTP route returns after durable receipt. A bounded background worker asks
the provider adapter for current state rather than trusting event order or
event fields. It verifies the preexisting Checkout attempt and both exact
allowlisted line items, enforces environment/customer/object uniqueness, and
uses a provider modified-time/revision cursor to make stale events no-ops.

## Projection lifecycle

The license remains `pending` until current state proves the initial invoice
paid with the exact license Price. Full refund maps to `refunded`; partial or
ambiguous refund and dispute map to attended denial. Subscription mapping now
preserves `incomplete` and `unpaid` rather than mislabelling them. `past_due`
has no grace by default, final cancellation denies, and active cancel-at-period
end remains active only while the provider's current state remains active.

Any open billing attention overrides otherwise-active commercial projections
and denies a new hosted reservation. A later clean, newer reconciliation can
resolve attention. These changes do not delete backups, release reservations,
or affect append/commit/receipt recovery for a session already reserved.

## Desktop surface

The existing Flutter **Plan and storage** card loads one server-approved
offering. Without a Customer binding it displays **Continue to secure
checkout**; with a binding it displays **Manage billing**. It sends no Price or
Customer ID, opens only a server-returned HTTPS URL through the platform's
external browser, never stores that URL, and presents Checkout return as
**Payment processing—refresh plan status after checkout.**

`url_launcher` 6.3.2 is the only added client dependency. Its generated macOS
and Windows registrants were updated by Flutter package resolution; no native
application was built or installed.

## Verification evidence

The final local gate passed:

- API tests: 46, including concurrent Checkout convergence, Owner/personal-beta
  gates, locally signed exact-byte verification, invalid and unknown events,
  duplicates, out-of-order no-op, paid projection, dispute attention, and
  ephemeral Portal URL handling;
- Platform tests: 23;
- local-engine tests: 67;
- Agent tests: 291;
- contract tests: 22;
- Flutter tests: 32 with clean analysis;
- EF reports no pending model changes;
- zero-warning Release builds for API, Agent, local host, and sync host;
- .NET and Dart formatting checks and `git diff --check`; and
- a changed-tree scan found fixture-labelled secrets only and no provider key,
  credential, bearer token, or personal path added by this milestone.

## Honest limits and next gate

This is local contract proof, not provider operational proof. The checked-in
application has no Stripe network adapter and no enabled production offering.
Exact products, prices, currency, taxes, payment methods, portal configuration,
restricted API key, endpoint secret, webhook registration, sandbox data,
deployment, and live lifecycle behavior remain unset and disabled.

The next bounded action is a separately authorized Stripe sandbox/operations
slice. A ShowVault customer website, membership and role administration,
internal staff Admin, provider financial actions, production hosted-object
durability, native proof, and deployment remain separate future work.
