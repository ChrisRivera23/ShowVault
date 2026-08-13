# Local-first milestone 5 extraction manifest

## Exact outcome

Milestone 5 reconstructs one provider-independent commercial outcome:

**Sign in as an organization Owner → open Plan and storage → review
server-derived license/subscription eligibility and logical hosted usage →
allow or deny each new hosted-sync reservation from the same projection →
retain path-free audited evidence**

The plan view is read-only. Existing Manager, Administrator, and Owner hosted
sync permission remains intact, but commercial details are Owner-only. Local
Save, inspection, and Restore remain signed-out/offline capable, and existing
hosted sessions and receipts remain recoverable after a later commercial
change.

This is an extraction and architecture artifact only. It authorizes no source
implementation, migration, network request, account/provider action,
credential, customer data, cloud resource, native build, external Git action,
deployment, or destructive cleanup.

## Historical source accounting

Extract behavior from exactly four historical commits, in this order:

| Commit | Historical concern | Disposition |
| --- | --- | --- |
| `c683b8f` | Organization, venue, membership, and role foundation | Retain organization-scoped authority and role separation; reconcile with the current platform domain. |
| `e017769` | Durable tenant persistence and isolation | Retain subject-membership joins, tenant isolation, constrained relationships, and adversarial tests; regenerate against the current database model. |
| `eea1d45` | Full account/billing/admin architecture amid navigation and beta work | Extract separate license/subscription projections, stable plan codes, password/provider boundaries, server-derived entitlement, audit minimization, and lifecycle non-deletion; do not replay mixed UI/beta code or select Stripe now. |
| `805a96c` | Direct-scan account exclusion boundary on a sibling historical line | Retain its explicit prohibition on personal-beta entitlement and client/route authorization bypass; revise the former billing exclusion for this bounded read/quota slice. |

The selected commits are noncontiguous and the final two represent distinct
historical source variants, not a linear patch series. They are provenance and
design evidence only.

The selected source has 40 unique paths, all of which overlap the current tree
and therefore require reconciliation rather than replay. Summed per-commit
statistics are 1,694 insertions and 59 deletions. Its concatenated binary
patches have SHA-256
`b841d1ce2ab385e7fb10fb77f56468cbd7c395952b5c188086928f712974ba5c`;
its sorted unique path list has SHA-256
`41d2d316e4e31ed6a8e8ad16cbbeb3bec565607a67f6182e8ee054e815c1c5fd`.

Reproduce the accounting:

```bash
for commit in \
  c683b8f987c2c05f7c312d8bf676beb1ad7ba031 \
  e017769cd6b745dac533dbb83a9a5cde8a10b004 \
  eea1d45543aaafd014ceeac32a38675bdf742188 \
  805a96c0035271d0ff9a31edadcea17516706c8a; do
  git show --format= --binary "$commit"
done | shasum -a 256

for commit in \
  c683b8f987c2c05f7c312d8bf676beb1ad7ba031 \
  e017769cd6b745dac533dbb83a9a5cde8a10b004 \
  eea1d45543aaafd014ceeac32a38675bdf742188 \
  805a96c0035271d0ff9a31edadcea17516706c8a; do
  git show --format= --name-only "$commit"
done | sed '/^$/d' | sort -u | shasum -a 256
```

## Current authority and API boundary

The API database is the sole authority for current membership, commercial
projections, effective policy, usage, reservations, and commercial audit.
Flutter renders an Owner-only response and bounded sync decision; it never
calculates entitlement or quota. Hosted object storage and the local SQLite
queue are not commercial authorities.

Add one authenticated Owner-only read endpoint under the exact organization,
for example `GET /api/v1/organizations/{organizationId}/plan`. Its closed
response contains only normalized commercial state and logical byte figures
defined in the account architecture. Missing subject returns 401; nonmember or
non-Owner returns 403 without revealing whether commercial records exist.

Keep hosted sync's current tenant/venue and role check. For a new `begin`, then
evaluate the server projection and reserve exact manifest bytes atomically.
Detailed status is not disclosed through the sync route: use bounded
`commercial_access_required` and `quota_exceeded` problem codes as non-retry
attention outcomes. Malformed requests, manifest conflicts, provider
unavailability, and authentication/authorization retain their distinct current
semantics.

## Data and state contract

Persist independent organization-scoped license and subscription projections,
an organization usage counter, a one-to-one session reservation, and an
append-only decision log. Use explicit normalized enums, timestamps, stable
internal policy codes, and concurrency revisions. Do not store authoritative
usage only inside `ReceiptJson` or recalculate it from the in-memory object
store.

Eligibility and quota rules are the canonical rules in
`docs/ACCOUNT_BILLING_ADMIN_ARCHITECTURE.md`. Important recovery invariants:

- a new manifest is reserved exactly once before any object write;
- concurrent organizations are isolated and concurrent begins cannot exceed
  one organization's limit;
- repeat begin for the same recovery point/digest does not reserve again;
- a conflicting digest remains a conflict rather than a second reservation;
- commit moves reserved to committed exactly once in the same transaction as
  durable completion state;
- append/commit/resume and receipt fetch for an existing reservation do not
  rerun the new-session commercial gate; and
- no billing state change deletes or releases a backup or reservation.

## Product exposure

Add a compact read-only **Plan and storage** section to the existing desktop
Settings surface for a signed-in Owner. It shows internal plan label/code,
normalized license/subscription status, renewal or grace date when applicable,
logical committed/reserved/limit figures, and an accessible eligibility
message. It offers no checkout, payment method, invoice, cancel-plan, invite,
role, or staff controls.

Managers and Administrators may still synchronize, but do not receive the
Owner plan surface or detailed payment lifecycle state. A commercial or quota
denial becomes a durable, path-free non-retry sync attention code; local
recovery remains usable and no payment detail appears in local SQLite.

## Provider and fixture boundary

Use deterministic pure policy evaluation and explicit synthetic records in
tests. A Development-only synthetic fixture may make the plan visible for
manual UI proof, but must be opt-in and must never attach automatically to the
personal-beta identity. Unsupported/missing policy in Non-Development denies
new sync closed.

Do not install or call Stripe or another billing plugin/provider; create
checkout sessions; use dashboard state; receive webhooks; choose prices;
retrieve customers, invoices, payment methods, or email; or introduce provider
secrets. Those actions need a later provider-specific extraction and explicit
authorization.

## Explicit non-goals

Milestone 5 does not implement or claim:

- signup, invitations, membership state changes, role administration, account
  suspension, installation management, customer portal, or internal Admin;
- checkout, payment collection, prices, invoices, tax, refunds, disputes,
  chargebacks, signed webhooks, customer-portal sessions, or a provider;
- physical object-store measurement, deduplication, retention expiry, remote
  deletion, quota release, abandoned-reservation reclamation, or export;
- production hosted-object storage, cloud durability, deployment, operations,
  or migration; or
- native installation, signing, notarization, equipment, or venue proof.

## Reconstruction sequence

1. Add pure commercial status, policy, entitlement, and reason-code domain
   types with deterministic time-bound tests.
2. Add database projections, usage/reservation/audit records, restrictive
   relationships, concurrency controls, and an EF migration.
3. Add the Owner-only plan query and response minimization tests.
4. Integrate atomic reservation into new hosted-sync begin and atomic transfer
   into commit while preserving existing-session recovery.
5. Map bounded commercial/quota denial into current local sync attention and
   add the read-only Flutter Settings section.
6. Add only explicit synthetic Development/test fixtures and reconcile docs.

## Verification gate

Implementation authorization, if separately granted, must prove at minimum:

- the full normalized license/subscription/time matrix, missing/unsupported
  fail-closed behavior, and deterministic boundary timestamps;
- Owner-only plan reads and no cross-tenant or non-Owner commercial disclosure;
- Manager/Admin/Owner new-sync behavior plus Viewer/Technician/outsider denial;
- exact organization-wide logical-byte accounting, quota boundaries, duplicate
  begin, conflicting manifests, concurrent begins, and concurrent commit;
- reservation-to-commit atomicity and recovery after process/database/object
  failures without double counting;
- an existing reserved session can resume/commit and its receipt can be read
  after later pause, cancel, refund, revoke, or grace expiry;
- path-free bounded UI/local/audit results and absence of provider/payment,
  password, token, path, filename, manifest, and backup-content leakage;
- signed-out/offline local Save/inspect/Restore and milestone-4 protocol
  behavior remain intact; and
- complete .NET/Flutter suites, EF pending-model gate, zero-warning Release
  builds, formatting, `git diff --check`, and repository secret/path audits.

Synthetic tests do not establish payment correctness, production billing,
production storage durability, native correctness, or operational readiness.
