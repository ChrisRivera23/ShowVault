# Local queue synchronization

ShowVault synchronizes only verified recovery points already recorded in the
selected local vault's SQLite queue. Local Save, inspection, and Restore remain
signed-out/offline operations and do not depend on hosted availability.

## User boundary

Synchronization is manual. It appears only when the user is signed in, an
organization and venue are loaded, a local vault is open, and at least one
verified point is not synchronized. The confirmation shows the bounded point
count and bytes and states that backup content and package-relative filenames
will be uploaded. Cancel preserves both the verified local point and any
durable resumable remote chunks.

Flutter passes the selected vault, organization ID, venue ID, current access
token, and configured API origin to the separately packaged sync host for one
invocation. The token stays in process memory. It is never written to the
vault, SQLite, manifests, evidence, stdout, stderr, or application logs.

## Local authority

The .NET local engine and per-vault SQLite database remain authoritative. The
sync host:

1. takes only queued records from SQLite, at most 25 per invocation;
2. reopens the package through no-follow directory handles;
3. freshly verifies the manifest and exact file topology;
4. retains verified file handles throughout upload;
5. constructs the closed hosted manifest without display names or local paths;
6. verifies the tenant-bound receipt; and
7. atomically records Synchronized only after receipt verification.

SQLite states are Synchronizing, Retry scheduled, Sync attention, and
Synchronized. Retry uses bounded exponential backoff capped at 30 minutes.
Local integrity failure moves the queue record to attention and uploads no
bytes. Tokens, absolute paths, provider keys, and raw server responses are not
durable fields.

The existing `showvault-local-engine` executable remains network-free and
accepts only Save, inspect, Restore, and Cancel. Network access is isolated in
`showvault-sync-engine`, whose closed JSON protocol accepts only Synchronize and
Cancel.

## Hosted protocol

The API requires an authenticated Manager, Administrator, or Owner membership
for the route organization and venue. Viewer, Technician, outsider, and
cross-tenant requests are denied. Candidate key and plugin ID must match the
server allowlist.

The versioned remote manifest contains only recovery-point/manifest identity,
opaque candidate key and plugin ID, creation time, file count, total bytes, and
each normalized package-relative path, size, and SHA-256. Extra fields,
absolute/traversal/control-character paths, duplicate paths, unapproved
metadata, malformed hashes, and exceeded bounds fail closed.

`begin` idempotently binds a database session to the tenant, recovery point,
and canonical manifest digest. File state returns the durable next offset.
Append accepts at most 256 KiB at that exact offset with a chunk digest;
identical replay is accepted and conflicting replay is rejected. Commit rejects
missing, extra, truncated, or corrupt objects, independently hashes all bytes,
and transactionally writes the immutable receipt last. Repeated or concurrent
completion returns only the same winning receipt.

The receipt binds organization, venue, recovery point, manifest digest, object
digests, counts, bytes, and completion time. There is no hosted overwrite,
delete, remote Restore, provider selection, bucket/key capability, or
client-supplied storage path.

## Provider boundary and evidence limit

Development and automated tests use an in-memory synthetic object adapter.
Non-Development environments register a disabled adapter and return unavailable
before creating a session. This milestone therefore proves the protocol and
state boundaries, not production storage durability. The synthetic adapter's
bytes do not survive an API restart and must never be used for customer data.

A production object provider, credentials/IAM, retention, replication,
readiness, migration, cleanup, billing, quota enforcement, deployment, and
hosted-copy Restore remain separately gated.
