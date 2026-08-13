# Milestone 4 hosted-sync reconstruction review — 2026-08-13

## Decision

Reconstruct hosted synchronization as a new current-architecture slice. Do not
replay the historical implementation.

The current milestone-2/3 system already has a stronger authority boundary than
the old code: verified recovery points, package-relative lookup, queue state,
Restore evidence, and repair live in the packaged .NET engine and per-vault
SQLite. Reintroducing the old Dart recovery service, JSON journal, or absolute
package paths would create a second filesystem/security authority.

## Retained historical invariants

- only freshly reverified queued packages can synchronize;
- the user explicitly initiates bounded work and can cancel;
- authentication and organization/venue membership are required;
- resumability uses durable exact offsets and idempotent identical replay;
- the server independently validates the closed manifest and all completed
  object bytes;
- an immutable receipt created last is the only completion marker;
- retry/attention never deletes or downgrades the local verified copy; and
- the UI refreshes durable state after completion.

## Replaced historical mechanisms

- Dart queue and filesystem verification → current .NET/SQLite authority;
- append-only JSON under the selected vault → transactional SQLite sync tables;
- Flutter HTTP/object-store authority → a separate packaged .NET sync host;
- absolute path-bearing queue records → package-relative, host-internal lookup;
- server-local filesystem as hosted durability → provider-neutral immutable
  object abstraction plus database-backed sessions/receipts;
- in-process semaphore as concurrency control → durable transactional
  compare-and-set and conditional object writes;
- product name in remote metadata → opaque candidate key/plugin allowlist;
- ambiguous production configuration → no production provider in this slice.

## Security and privacy findings

The content bytes and package-relative filenames are customer backup data, not
telemetry. Synchronization therefore needs explicit informed consent and may be
tested only with synthetic fixtures in this milestone. Tokens are ephemeral
process input. Neither desktop nor server records may contain local absolute
paths, unrestricted manifests, credentials, raw error responses, or provider
keys.

Route organization/venue IDs are selectors, not proof of authority. Every API
operation must join the authenticated subject to current membership and verify
that the venue belongs to that organization. The existing product role model
supports the historical write boundary: Manager, Administrator, and Owner may
synchronize; Viewer and Technician may not.

## Durability findings

The historical local server store used filesystem receipts and a process-local
lock. That proves useful protocol behavior but not hosted durability or
multi-instance correctness. Current reconstruction needs durable upload-session
and receipt rows, immutable conditional object writes, and receipt-last commit.
A filesystem adapter is evidence-only in Development/tests.

If the remote commit succeeds but the desktop stops before recording it, the
next attempt must fetch and verify the same receipt, then complete the local
transaction without uploading again. If local content changes before commit,
fresh verification fails and hosted completion is not claimed.

## Product and scope findings

Hosted synchronization is separable from account/billing administration. The
first can use current authentication, tenancy, role membership, and a closed
synthetic policy seam. Invitations, role-management UI, subscription state,
Stripe or another billing provider, and production quota enforcement would
materially enlarge the data, authorization, and external-system surface and
remain deferred.

Likewise, the later historical S3/container commits are not required to prove
the hosted protocol locally. Production object-storage selection, IAM,
retention, replication, deployment, migrations, and cleanup require their own
explicitly authorized slice after the protocol boundary exists.

## Planning conclusion

The bounded implementation plan is ready for a separate authorization. No code,
database migration, network request, cloud resource, credential, personal data,
native action, or external repository state was changed by this review.
