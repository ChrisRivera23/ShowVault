# Local queue synchronization

ShowVault's synchronization executor consumes only verified recovery points from the durable desktop JSON queue. Normal signed-in builds now send the path-free package through an authenticated ShowVault API transport. A controlled filesystem object-store substitute remains available only to explicitly isolated synthetic builds.

## Execution boundary

Before uploading any bytes, the executor:

1. reopens the authorized local vault through the existing bounded vault inspection;
2. verifies the queue record identity and canonical package path;
3. verifies the package manifest SHA-256 against the immutable recovery-point ID;
4. validates bounded source metadata and every manifest file entry;
5. rejects unsafe logical paths, links, unsupported entries, duplicates, missing files, added unlisted files, size-limit violations, and checksum mismatches; and
6. constructs a privacy-filtered remote manifest.

The remote manifest contains the package ID, creation time, opaque candidate key, plugin ID, product name, file logical paths, sizes, SHA-256 values, and local manifest digest. It excludes the local source identity, package path, vault path, credentials, and unrestricted local manifest metadata.

## Durable state and retry behavior

The original `Upload Queue/<package ID>.json` intent remains immutable. Synchronization state is an append-only journal:

```text
Upload Queue/State/<package ID>/
├── 00000001.json
├── 00000002.json
└── ...
```

Each bounded event records status, attempt count, update time, next retry time, a path-free error, the remote-manifest digest, and completion time where applicable. States are `queued`, `syncing`, `retry`, `failed`, and `synchronized`.

Unavailable transport errors use exponential backoff capped at 30 minutes and stop after five attempts by default. Permanent local-package or remote-integrity failures move the job to **Queue attention**. No failure changes or deletes the locally verified recovery point.

## Resumability and idempotency

The object-store contract exposes the durable remote byte length for each logical object. Uploads append bounded chunks from that offset. If a process stops after a remote chunk is written but before local completion, a new executor instance resumes from the stored remote length.

Finalization independently hashes every remote object, writes the filtered remote manifest and receipt, and atomically publishes the completed package. Reprocessing a completed package checks the existing receipt and performs no duplicate upload.

## Product exposure

After authentication and organization/venue loading, a normal build constructs the hosted transport in memory from the current access token and server-authorized tenant context. **Synchronize pending** remains hidden while signed out or while no organization and venue are available. Tokens and hosted-storage capabilities are never written into the vault, package, manifest, evidence, or queue journal.

The hosted API requires manager, administrator, or owner membership for the route organization and venue. Before receiving chunks, its `begin` operation independently validates the exact remote-manifest shape, package identity, approved catalog candidate metadata, logical paths, file count, sizes, and hashes. Server storage paths are derived only from authorized organization/venue GUIDs, the bounded package ID, and validated logical segments. The client cannot supply a storage root or local filesystem path.

The first hosted storage implementation uses a configured server-owned filesystem root behind the API. `HostedSync:RootPath` is empty by default, causing a retryable `503` until an operator explicitly configures controlled development storage. This proves authenticated hosted transport and tenant binding, but it is not production object-storage durability.

An isolated build can still use the test substitute by defining both:

- `SHOWVAULT_SYNTHETIC_FIXTURE_HOME`
- `SHOWVAULT_SYNTHETIC_OBJECT_STORE_ROOT`

Only that explicit synthetic configuration selects the direct folder substitute instead of the authenticated API.

## Current evidence and limitations

Automated tests prove successful remote verification, restart resume, cancellation resume, idempotency, concurrent completion, duplicate chunks, expired-session retry, manager authorization, viewer/outsider denial, cross-tenant isolation, stale/conflicting data rejection, local and remote tamper, zero-byte files, package/storage links, privacy filtering, bounded request shapes, and UI state refresh.

This slice does not claim production object-storage retention or regional durability, distributed multi-server locking, bandwidth scheduling, billing enforcement, installed hosted-sync execution, Windows installed execution, dependency closure, or Recovery Confidence.
