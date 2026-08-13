# Local-first milestone 4 extraction manifest

## Exact outcome

Milestone 4 reconstructs one bounded product outcome:

**Sign in → open a local vault → synchronize verified queued recovery points or
Cancel → verify an immutable hosted receipt → retain durable path-free status**

Local Save, inspection, and Restore remain available signed out and offline.
Synchronization is attended and user-initiated; it never scans automatically,
changes local content, or makes hosted availability a prerequisite for local
recovery.

This is an extraction and architecture artifact only. It authorizes no
implementation, network call, credential use, cloud resource, deployment,
personal/customer/venue data, native build, push, PR, or destructive cleanup.
Account, subscription, quota administration, billing providers, and production
object-storage topology are separate gated slices.

## Historical source accounting

Extract behavior from exactly three noncontiguous historical commits, in this
order:

| Commit | Historical concern | Disposition |
| --- | --- | --- |
| `f016ad1` | Durable local upload queue and resumable executor | Retain retry, cancellation, re-verification, and idempotency invariants; replace the Dart queue, append-only JSON state, absolute package paths, and Dart filesystem verification. |
| `5f05f44` | Authenticated tenant-scoped hosted transport and receipt-last store | Retain authorization, closed manifests, exact offsets, independent commit verification, receipt-last completion, and adversarial tests; replace Flutter transport authority and server-local filesystem storage. |
| `a7eee0d` | Refresh synchronized status after completion | Retain refresh behavior; regenerate it against current .NET/SQLite results and current Flutter state. |

The selected source has 20 unique paths. Summed per-commit statistics are 3,683
insertions and 52 deletions. Its concatenated binary patches have SHA-256
`cbce2c3130dc7725963ec095fc7654c5bf25c46385117a89ba9cdf6aa7356e66`;
its sorted unique path list has SHA-256
`ad32139f0376d26bd72fb331d0ca50e3e98b3c32009c9aad678635363c574aec`.

Six paths overlap the current tree and must be reconciled, never replayed:

- `apps/showvault_app/lib/src/config/app_config.dart`
- `apps/showvault_app/lib/src/dashboard/dashboard_screen.dart`
- `apps/showvault_app/test/app_test.dart`
- `services/api/src/ShowVault.Api/Program.cs`
- `services/api/src/ShowVault.Api/appsettings.json`
- `services/api/tests/ShowVault.Api.Tests/TenantApiFactory.cs`

The remaining 14 historical paths are absent from the current tree. They are
design evidence, not an instruction to restore their old boundaries.

Reproduce the selected-source accounting:

```bash
for commit in f016ad1 5f05f44 a7eee0d; do
  git show --format= --binary "$commit"
done | shasum -a 256

for commit in f016ad1 5f05f44 a7eee0d; do
  git show --format= --name-only "$commit"
done | sed '/^$/d' | sort -u | shasum -a 256
```

The containing range `c172e49..fff4434` remains mixed historical context: 10
commits, 31 unique paths, 5,387 insertions, 76 deletions, binary-diff SHA-256
`7cb9d0c81ac5646353c9645eefd86844afa9706c8569fb2595afa241d188a317`,
and sorted-path SHA-256
`751bd1a7eaceee71b89fd1a798ea4514acba92586c1e5479ebdae55a346ae0eb`.
Restore fixes and documentation in that range are already accounted for by
milestone 3 or are historical only; they are not milestone-4 source.

## Current authority boundary

The packaged .NET local engine and its per-vault SQLite database remain the
only authority for verified recovery-point identity, package-relative lookup,
queue state, retry state, and hosted receipt evidence. Flutter owns consent,
authentication context, presentation, progress, and Cancel only.

Add a separately packaged, narrowly callable .NET synchronization host rather
than adding networking to the existing Save/inspect/Restore host. The sync host
may reuse local-engine verification and stable-handle primitives, but the
existing host must remain network-free. Flutter passes the selected vault,
organization ID, venue ID, and current access token over the child process's
stdin for that invocation only. The token is held only in process memory and
is never written to SQLite, a manifest, evidence, stdout, stderr, or logs.

The synchronization host:

1. opens the explicitly selected vault through the current no-follow boundary;
2. obtains only queued recovery points from SQLite, bounded per invocation;
3. freshly verifies manifest identity and exact content while retaining stable
   package/file access for the upload;
4. constructs the closed remote manifest and performs the hosted protocol;
5. verifies the returned receipt; and
6. atomically records synchronized or retry/attention state in SQLite before
   returning a path-free result.

It exposes no arbitrary source selection, Restore target, agent identity,
device/application loading, background service, or filesystem-root parameter
beyond the independently selected local vault.

## Explicit hosted data contract

Synchronization transfers customer backup content and therefore requires a
signed-in user's explicit action. The remote manifest is a closed,
versioned schema containing only:

- recovery-point ID and local-manifest SHA-256 (the same value in milestone 4);
- opaque approved candidate key and plugin ID;
- recovery-point creation time;
- file count and total bytes;
- each package-relative content path, byte length, and SHA-256; and
- a canonical remote-manifest digest.

The content bytes and package-relative filenames are customer data. Tests and
all implementation evidence use synthetic fixtures only. The remote contract
excludes local absolute paths, selected vault/source paths, product display
name, local operation IDs, local verification/evidence IDs, restore evidence,
credentials, unrestricted local manifest metadata, device identity, and
personal/venue names.

The API derives tenant scope from the authenticated subject's membership plus
the route organization and venue. Upload requires Manager, Administrator, or
Owner; Viewer and Technician are denied. Tenant IDs are never sufficient
authorization by themselves. Candidate key/plugin identity must match a
server-owned allowlist rather than client-supplied display metadata.

## Resumable protocol and durable completion

All request bodies, header values, manifest fields, file counts, logical paths,
objects, chunks, and totals use explicit bounds. The default maximum chunk is
256 KiB; the server selects and returns the effective bound.

1. `begin` validates membership, venue ownership, the exact manifest schema,
   manifest digest, approved candidate identity, package identity, and policy.
   It idempotently creates or returns one durable upload session bound to the
   tenant, recovery point, and immutable manifest digest.
2. `file state` returns the next durable offset for one manifest-listed object.
   It reveals no physical storage key or provider capability.
3. `append` accepts only the exact next offset, a bounded length, and a chunk
   digest. Repeating an already durable chunk is accepted only when the bytes
   and digest are identical; gaps, overlaps, conflicts, and unlisted objects
   fail closed.
4. `commit` independently relists the session's complete logical object set,
   rejects extras and gaps, rehashes every assembled object, and transactionally
   publishes immutable objects plus one durable receipt. The receipt is the
   sole hosted completion marker and is created last.
5. `receipt` returns the canonical receipt. Concurrent/repeated commit succeeds
   only by validating the same immutable winner.

The receipt binds schema version, organization ID, venue ID, recovery-point ID,
remote-manifest digest, file count, total bytes, verified object digests, and
completion time. The local engine marks `synchronized` only after canonical
receipt verification and a durable SQLite transaction.

Server session/receipt state must be database-backed with concurrency tokens or
equivalent transactional compare-and-set. Hosted bytes sit behind an object
store abstraction with conditional immutable writes; a server-local filesystem
is permitted only as an explicit Development/test substitute. No production
provider or cloud account is selected by this milestone.

## Local state, retry, and product exposure

Extend SQLite with sync-attempt/session state rather than mutating the immutable
Save intent. States are `queued`, `synchronizing`, `retry_scheduled`,
`attention`, and `synchronized`. Store only bounded path-free error codes,
attempt count, next retry time, remote-manifest digest, opaque server session
ID, receipt digest, and completion time. Never store a token, absolute path,
raw response, or provider key.

Transient network, authentication expiry, cancellation, and service
unavailability preserve the local recovery point and resumable state. Use
bounded exponential backoff capped at 30 minutes; an explicit later user action
may retry. Local tamper, manifest conflict, remote digest conflict, and rejected
schema/policy enter attention. Cancellation stops before the next remote write,
does not delete durable remote chunks, and leaves the point retryable.

Flutter shows **Synchronize** only when a vault is open, at least one verified
point is queued, and an authenticated organization/venue context exists. The
action is manual, previews item count/bytes and the fact that backup content and
filenames will be uploaded, supports Cancel, refreshes from the host result,
and uses only path-free states: Cloud queued, Synchronizing, Retry scheduled,
Sync attention, and Synchronized. Signing out hides the action but never hides
or damages local recovery points.

## Explicit non-goals

Milestone 4 does not implement or claim:

- automatic/background synchronization or bandwidth scheduling;
- account creation, invitations, role administration, subscriptions, billing,
  entitlements, customer quota UI, or payment-provider integration;
- a production S3 provider, cloud credentials, retention, replication,
  regional durability, disaster recovery, migration, or destructive cleanup;
- remote Restore or remote deletion/overwrite;
- native installation, signing, notarization, Windows execution, equipment,
  venue readiness, or Recovery Confidence.

A deterministic policy seam may reject over-limit test uploads. It must not be
presented as billing entitlement or production quota enforcement.

## Reconstruction sequence

1. Extend current SQLite authority and verification access with durable sync
   attempts, transitions, reselect repair, and path-free inspection summaries.
2. Add the separate .NET synchronization host and closed stdin/stdout protocol;
   keep the existing local host network-free.
3. Add API contracts, manifest validation, tenant/role authorization, durable
   session/receipt entities, concurrency behavior, and object-store abstraction.
4. Add only synthetic Development/test object storage and transport fixtures;
   no production provider configuration.
5. Add Flutter consent, manual Synchronize/Cancel, progress, refresh, signed-out
   isolation, and accessible path-free state.
6. Reconcile documentation and packaging guards so the new sync host is
   packaged deliberately without weakening the Save/Restore host boundary.

## Verification gate

Implementation authorization, when separately granted, must at minimum prove:

- exact verified-only intake; fresh verification; stable-handle tamper/link
  resistance; added/unlisted file rejection; and no queueing of unverified data;
- closed manifests, bounds, tenant isolation, Manager/Admin/Owner permission,
  Viewer/Technician/outsider denial, catalog mismatch rejection, and no path or
  credential leakage;
- interrupted and cancelled upload resume, expired-token retry, identical chunk
  replay, conflict rejection, concurrent begin/append/commit, receipt-last
  publication, and local transaction recovery after a remote commit;
- signed-out/offline Save, inspect, and Restore remain unchanged while hosted
  unavailability affects synchronization only;
- synthetic end-to-end process-host/API execution with no real account, bucket,
  customer data, or external service; and
- complete .NET and Flutter suites, EF pending-model gate, zero-warning Release
  builds, formatting, packaging guards, `git diff --check`, and a path/secret
  audit.

Passing synthetic tests will not establish production durability, operational
readiness, native-platform correctness, or recoverability from a hosted copy.
