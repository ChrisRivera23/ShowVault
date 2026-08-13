# Local-first milestone 5 implementation — 2026-08-13

## Result

Milestone 5 implements the authorized provider-independent commercial outcome:

**Sign in as an organization Owner → open Plan and storage → review
server-derived license/subscription eligibility and logical hosted usage →
allow or deny each new hosted-sync reservation from the same projection →
retain path-free audited evidence**

The implementation is local and synthetic. It did not install or contact a
payment provider, choose prices, create provider/customer records, use
credentials or customer data, mutate cloud resources, deploy, perform native
installation, or change external Git state.

## Commercial authority

The platform now defines normalized license and subscription states, stable
plan-policy codes, and a deterministic evaluator. An active effective license
plus a trialing/active subscription, or past-due subscription strictly before
its grace deadline, permits a new hosted session. Missing, inactive, expired,
unsupported, or inconsistent state denies closed.

The synthetic `synthetic.standard` policy is registered only in Development
and tests with a 100 MiB logical limit. Other environments use a disabled
catalog, so even a synthetic database record cannot grant production access.
No fixture is automatically attached to the personal-beta identity.

## Persistence and migration

Migration `20260813065747_AddCommercialEntitlements` adds:

- one license and one subscription projection per organization;
- nonnegative organization committed/reserved logical byte counters;
- one nonnegative reservation per hosted-sync session;
- append-only minimized commercial audit events; and
- an explicit manifest-total-byte column on hosted sessions.

Existing hosted sessions are backfilled from their closed JSON manifest.
Completed sessions become committed usage; incomplete sessions become retained
reservations. Relationships use restrictive deletion, byte columns have check
constraints, and usage/session/reservation revisions are concurrency tokens.
The database context refuses audit mutation or deletion.

## API and quota behavior

`GET /api/v1/organizations/{organizationId}/plan` is authenticated and
Owner-only. It returns normalized license/subscription status, stable plan code,
period/grace dates, effective limit, committed/reserved logical bytes,
eligibility, and one bounded reason code. It returns no provider IDs, prices,
email, payment data, credentials, paths, filenames, manifests, or contents.

Hosted-sync begin preserves its exact tenant/venue and
Manager/Administrator/Owner authorization, then atomically creates the session,
reserves the full manifest total, and records an audit decision. Repeated begin
does not reserve twice; a digest change conflicts; concurrent begins cannot
over-allocate. Commercial and quota denials return only
`commercial_access_required` or `quota_exceeded`.

Commit atomically transfers reserved to committed bytes exactly once and emits
one append-only commit event. Concurrent/repeated commit returns the immutable
winner without double counting. An already reserved session can append, commit,
and retrieve its receipt after a later pause, cancellation, expiry, refund, or
revocation. New sessions remain denied. No deletion or reservation release was
added.

## Local client and desktop

The local sync engine recognizes the two bounded policy-denial codes and stores
them as non-retry attention. It does not persist a response body, payment
detail, token, path, or filename. All milestone-2/3 local behavior remains
independent of sign-in and hosted availability.

Flutter's existing Settings destination now renders **Plan and storage**. A
signed-in Owner receives the compact read-only summary and accessible hosted
eligibility state. A non-Owner does not request the plan endpoint and sees only
the Owner-required boundary. There are no checkout, invoice, payment-method,
plan mutation, invitation, role, or staff actions.

## Verification evidence

The final local gate passed:

- Platform tests: 23;
- API tests: 39;
- local-engine tests: 67;
- Flutter tests: 30, with clean analysis;
- Agent tests: 291;
- contract tests: 22;
- repeated concurrent-begin and concurrent-commit stress cases;
- EF pending-model check;
- zero-warning Release builds for API, Agent, local host, and sync host;
- .NET and Dart formatting;
- `git diff --check`; and
- changed-file credential, provider-secret, personal-path, and bearer-token
  audit.

The tests cover the license/subscription/time matrix, disabled catalog,
Owner-only/cross-tenant disclosure, role gates, bounded denials, exact quota
edges, duplicate/conflicting/concurrent operations, append-only audit,
reservation-to-commit accounting, post-revocation recovery, local attention,
and minimized desktop rendering. All test identities, organizations, venues,
content, and policies are synthetic.

## Honest limits

This milestone does not prove payment correctness, webhook integrity,
production billing, tax/refund/dispute behavior, customer or staff
administration, production storage durability, deployment/operations, native
platform correctness, equipment readiness, or hosted-copy recovery. The 100
MiB synthetic limit is not a product tier or price commitment.
