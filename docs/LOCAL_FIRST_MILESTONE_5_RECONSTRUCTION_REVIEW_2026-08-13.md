# Milestone 5 commercial-boundary reconstruction review — 2026-08-13

## Decision

Reconstruct a provider-independent commercial projection and logical quota
boundary against the current tenancy and hosted-sync implementation. Do not
replay historical account code and do not select or contact a payment provider.

The exact product result is an Owner-only read-only Plan and storage view plus
server-side new-session entitlement and organization-wide byte reservation.
Membership administration, financial operations, customer portal, and staff
Admin are materially different authorization surfaces and remain deferred.

## Historical reconciliation

The organization and persistence commits remain structurally useful, but their
domain and migrations have evolved. The large account architecture appeared in
a mixed navigation/personal-beta commit and described a target system rather
than executable authority. A later sibling-history document correctly bounded
personal beta and tenancy but excluded billing altogether.

Current reconstruction retains organization membership, separate license and
subscription concepts, stable internal plan codes, server-derived entitlement,
provider/password separation, minimized audit, and non-deletion lifecycle
rules. It replaces historical product breadth, provider assumptions, and mixed
UI mechanisms with the smallest current hosted-sync enforcement seam.

## Current-system findings

The API already authorizes hosted sync by subject membership, exact
organization/venue relationship, and Manager/Administrator/Owner role. Its
database-backed `HostedSyncSession` stores the closed manifest and receipt, but
has no explicit manifest-byte column, usage ledger, quota reservation, or
commercial projection. `ReceiptJson` and the Development in-memory object store
cannot be usage authority.

The current Flutter shell already has a Settings destination but only a generic
section placeholder. A compact Owner-only read surface fits there without
inventing a customer portal. The desktop must remain a presenter: no local
entitlement calculation and no persisted payment detail.

## Authorization and disclosure findings

The route organization ID selects a record; only the authenticated subject's
current membership authorizes it. The plan endpoint is Owner-only. Existing
sync roles remain Manager, Administrator, and Owner, but sync denials reveal
only bounded commercial/quota reason codes. This preserves operational access
without exposing license, renewal, grace, or usage details to lesser roles.

The personal-beta identity is not a customer identity and must receive no
implicit license or subscription. Synthetic records must be explicit and
Development/test-only. Missing or inconsistent production projection state
denies a new hosted session.

## Concurrency and lifecycle findings

Checking usage and then creating a session in separate transactions would
permit concurrent over-allocation. The full manifest size therefore needs one
organization-wide reservation in the same serializable or compare-and-set
transaction that creates the session. Idempotent begin binds one reservation
to one session; commit transfers the exact amount once.

Re-evaluating commercial eligibility on every append or commit would strand
accepted bytes and break milestone-4 receipt recovery. Commercial state gates
only a new reservation. Authorized continuation and receipt reads use the
existing reservation/session boundary. Cancellation retains it. Releasing,
expiring, deleting, or reclaiming abandoned data needs an explicit retention
policy and is not safely inferable here.

## Provider and privacy findings

The useful historical Stripe guidance is architectural: separate provider
facts from authorization, use signed idempotent events, keep stable internal
plan codes, and never trust redirect/email/client state. It is premature to
choose products, prices, IDs, webhook schemas, or operational credentials in
this slice.

Public plan data and audit evidence can remain provider-neutral and minimized.
Neither contains payment instruments, invoice details, email, provider IDs,
raw events, credentials, paths, filenames, manifests, or backup contents.

## Planning conclusion

The bounded implementation contract is ready for a separate Product Owner
authorization. This review changed documentation only. It performed no
application implementation, migration, provider or network action, credential
use, customer-data access, cloud operation, native action, external Git action,
deployment, or destructive cleanup.
