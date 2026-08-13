# Account, commercial-state, and administration architecture

## Product surfaces and current slice

ShowVault keeps three related product surfaces:

1. **ShowVault desktop** for local Scan, Save, Verify, Restore, hosted
   synchronization, and a compact read-only organization-plan summary.
2. **ShowVault customer account portal** for later signup, membership and role
   administration, billing, invoices, plan changes, licenses, and devices.
3. **ShowVault Admin** for later strongly authenticated staff support and
   operational inspection.

Milestone 5 implements only the provider-independent commercial boundary: an
organization Owner can read normalized license, subscription, eligibility, and
logical hosted-storage usage; the API uses the same server-derived projection
to authorize and reserve quota for a new hosted-sync session. It does not add a
payment provider, checkout, webhooks, provider portals, invitations, role
mutation, staff administration, or provider/customer identifiers to clients.

## Identity and password boundary

The authenticated identity-provider subject continues to select a current
organization membership. Route IDs and client claims are never authority.
ShowVault must never display, retrieve, export, transmit, or store plaintext
passwords. Password hashing, MFA/passkeys, recovery, and password reset remain
identity-provider responsibilities.

The guarded personal-beta identity is Development-only loopback scaffolding.
It must not create a commercial record, receive an implicit entitlement, or
bypass membership, role, entitlement, or quota checks.

## Separated commercial records

The one-time license and recurring service subscription are independent,
organization-scoped projections:

- `CommercialLicense`: internal type code, normalized
  `pending|active|refunded|revoked` state, effective timestamps, revision, and
  optional server-only provider linkage for a later milestone.
- `ServiceSubscription`: stable internal plan code, normalized
  `trialing|active|past_due|paused|canceled` state, current-period and grace
  timestamps, revision, and optional server-only provider linkage.
- `OrganizationStorageUsage`: logical committed bytes, reserved bytes, and a
  concurrency revision.
- `HostedSyncReservation`: the organization, hosted-sync session, exact
  manifest total, and `reserved|committed` state.
- `CommercialAuditEvent`: append-only bounded decision/change evidence.

Do not infer commercial access from email, redirect success, a desktop claim,
or raw provider state. A deterministic server evaluator combines current
license, subscription, plan policy, time, and storage usage into an effective
entitlement. Stable internal plan codes permit later provider price changes
without rewriting history. Exact prices and production limits remain Product
Owner/provider decisions.

## Eligibility policy

A new hosted-sync session requires an active license plus a subscription that
is `trialing`, `active`, or `past_due` before its explicit grace deadline.
`pending`, `refunded`, `revoked`, `paused`, `canceled`, expired grace, missing
records, unsupported policies, or inconsistent projections deny closed.

The Owner-only plan response may contain the internal plan code, normalized
states, relevant period/grace timestamps, effective logical byte limit,
committed and reserved logical bytes, an eligible boolean, and a bounded reason
code. It excludes provider IDs, prices, invoices, payment method/card data,
email, credentials, tokens, filesystem paths, filenames, backup contents, and
raw provider payloads.

Manager, Administrator, and Owner keep the existing hosted-sync write role.
They receive only a bounded `commercial_access_required` or `quota_exceeded`
decision from sync begin; detailed commercial state remains Owner-only.
Unauthenticated, outsider, wrong-tenant, Viewer, and Technician access remains
denied before commercial information is evaluated or disclosed.

## Logical quota and recovery

Quota is organization-wide across venues and measures the closed manifest's
logical content bytes, not physical provider overhead or deduplicated size.
Starting a previously unseen recovery point atomically checks:

`committed bytes + reserved bytes + requested bytes <= effective limit`

and reserves the full manifest total in the same durable transaction that
creates the hosted session. Concurrent begins must serialize or use a durable
compare-and-set so they cannot over-allocate. Repeating the same recovery point
and manifest returns its existing session and never reserves twice. Commit
atomically moves its exact bytes from reserved to committed.

Commercial ineligibility blocks only a new session. Append, commit, and receipt
recovery for an already reserved session remain available to an otherwise
authorized member, including after later suspension, so accepted bytes are not
stranded and receipt-last recovery still works. Cancellation and transient
failure retain the reservation and remote chunks. This milestone adds no
deletion, quota release, expiry, or automatic reconciliation; abandoned
reservations require a separately designed attended policy and fail closed in
the meantime.

Billing state must never silently delete, hide, or corrupt local or hosted
recovery evidence. Receipt reads remain available to authorized members after
a later commercial-state change.

## Provider boundary

Milestone 5 has no payment-provider integration. Tests and Development may seed
explicit synthetic projection records and a synthetic stable policy code.
Non-Development environments with absent or unsupported commercial state deny
new sessions. Production defaults, limits, provider secrets, customer IDs,
checkout, signed webhooks, refunds, disputes, invoice/payment-method access,
and customer portal sessions require a later separately authorized provider
slice.

When a provider is later selected, signed idempotent webhooks update ShowVault's
own projections and entitlement remains derived from those projections. Raw
provider events are not authorization responses or general audit records.

## Audit and privacy

Every synthetic projection change and every new-session allow/deny decision
produces append-only evidence with organization ID, server-known actor subject
when applicable, action, outcome, bounded reason code, requested/reserved/
committed byte counts when relevant, correlation ID, timestamp, and policy
version. Audit rows contain no names, email, passwords, tokens, provider
secrets, payment data, absolute or relative paths, filenames, backup content,
or raw request/provider payloads.

## Later administration

The customer portal and internal Admin console remain separate future work.
They will require explicit membership lifecycle, suspension, step-up approval,
staff roles, immutable support auditing, payment-provider redirects, retention,
export, refund/chargeback, and deletion policies. Provider dashboards should
remain the source for sensitive payment and identity-provider operations.

## Milestone 6 Stripe-hosted funnel

The first provider-backed slice uses Stripe-hosted Checkout and Billing Portal
from the existing Owner-only desktop Settings surface. ShowVault creates those
sessions server-side from a closed internal offering catalog and fixed return
origins. The desktop never submits Price or Customer IDs, collects payment
details, or treats a return redirect as purchase authority.

Provider facts stay in separate server-only organization/environment bindings,
purchase attempts, minimal signed-event receipts, and reconciliation cursors.
Verify each webhook against its exact raw body before parsing; deduplicate it
durably; then retrieve current provider objects through a narrow adapter before
transactionally changing `CommercialLicense` or `ServiceSubscription`. Do not
persist raw provider payloads or ephemeral Checkout/Portal URLs.

One approved subscription-mode Checkout offering may contain one recurring
Price and one one-time license Price. The license remains pending until the
exact initial invoice is proven paid. Redirect completion and metadata alone
grant nothing. With no separately approved grace duration, `past_due` has no
implicit grace and denies new hosted reservations.

Cancellation, refund, dispute, payment failure, or unsupported provider state
must deny or enter bounded billing attention without deleting backups,
releasing quota, or stranding an already reserved hosted session. Full
ShowVault customer-account pages, staff Admin, role changes, financial support
actions, final prices/taxes, and live provider operations remain later slices.
