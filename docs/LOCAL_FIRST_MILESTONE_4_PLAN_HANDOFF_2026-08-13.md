# Local-first milestone 4 planning handoff — 2026-08-13

## Checkpoint

- Planning branch: `codex/local-first-milestone-4-plan`
- Planning worktree: `/private/tmp/showvault-local-first-m4-plan/worktree`
- Exact milestone-3 base: `32f1b2c74241f89b6185c90db31a9f508f61739c`
- Extraction/architecture commit: `270b60954ebc66cd464df3f3bbb806d95dcec044`
- Exact outcome: **Sign in → open a local vault → synchronize verified
  queued recovery points or Cancel → verify an immutable hosted receipt →
  retain durable path-free status**

Read `docs/LOCAL_FIRST_MILESTONE_4_EXTRACTION.md` and
`docs/LOCAL_FIRST_MILESTONE_4_RECONSTRUCTION_REVIEW_2026-08-13.md` completely
before acting from this checkpoint.

## Completed planning

Exactly three historical commits were selected and accounted for: `f016ad1`,
`5f05f44`, and `a7eee0d`. Their useful protocol invariants were separated from
the obsolete Dart queue/filesystem authority and server-local hosted store.

The reconstruction contract now fixes:

- the current .NET/per-vault SQLite engine as the only package and queue
  authority;
- a separate network-capable .NET sync host, leaving the existing local host
  network-free;
- explicit manual consent for customer backup content and relative filenames;
- ephemeral token handling and path-free durable state;
- Manager/Administrator/Owner tenant authorization with
  Viewer/Technician/outsider denial;
- a closed manifest, bounded exact-offset chunk protocol, durable database
  sessions, conditional immutable objects, independent commit verification,
  and receipt-last completion;
- cancellation, retry, attention, restart recovery, and local/offline isolation;
  and
- synthetic Development/test storage only, with production storage, cloud
  operations, account administration, billing, quotas, and native proof
  explicitly deferred.

The roadmap separates hosted synchronization from account/billing
administration. No implementation source, database migration, network request,
credential, cloud resource, customer data, native artifact, or external Git
state was changed.

## Authorization boundary

This checkpoint completes extraction and architecture planning only. It does
not authorize implementation.

Do not modify application/API/engine code or migrations; contact a hosted
service; use credentials, personal/customer/venue data, or cloud resources;
build/install meaningful native packages; fetch/push Git state; create/mutate a
PR; dispatch workflows; deploy; or perform destructive cleanup without new
explicit authorization.

## Next gated action

Stop for Product Owner direction. The next bounded action is implementation of
the exact milestone-4 hosted-synchronization contract in the extraction
manifest, using synthetic local fixtures only. It requires separate explicit
implementation authorization.

Account, role, subscription, quota, and billing administration remains the
following independent decision and is not included in that implementation.
