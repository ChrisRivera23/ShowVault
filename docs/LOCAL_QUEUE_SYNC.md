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

The deployable provider stores immutable manifest/chunk objects in a private S3-compatible bucket and publishes the receipt last as the sole completion marker. Production startup requires this provider and fails closed on missing or unsafe configuration. The configured server-owned filesystem backend remains available only in Development and controlled tests. See `docs/DEPLOYABLE_PROTOTYPE_STORAGE.md` for key layout, credentials, deployment, cleanup, and migration boundaries.

An isolated build can still use the test substitute by defining both:

- `SHOWVAULT_SYNTHETIC_FIXTURE_HOME`
- `SHOWVAULT_SYNTHETIC_OBJECT_STORE_ROOT`

Only that explicit synthetic configuration selects the direct folder substitute instead of the authenticated API.

Installed resilience drills may additionally define `SHOWVAULT_SYNTHETIC_SYNC_CHUNK_BYTES` and `SHOWVAULT_SYNTHETIC_SYNC_CHUNK_DELAY_MS` together with the fixture-home seam. Those test-only values make durable partial chunks observable. Normal builds ignore them unless fixture isolation is active and retain 256 KiB chunks with no artificial delay.

## Current evidence and limitations

Automated tests prove successful remote verification, restart resume, cancellation resume, idempotency, concurrent completion, duplicate chunks, expired-session retry, manager authorization, viewer/outsider denial, cross-tenant isolation, stale/conflicting data rejection, local and remote tamper, zero-byte files, package/storage links, privacy filtering, bounded request shapes, and UI state refresh.

The installed release-mode macOS synthetic drill traversed the authenticated loopback API without the direct folder substitute. It captured a durable partial hosted object, terminated and relaunched the app, reopened the vault, and completed from append-only local attempt 2 without duplicate completion. The committed tenant-derived package contained only the expected content, path-free manifest, and receipt, with matching SHA-256 values. A request without authentication returned `401`. A second isolated local vault remained verified while the API was stopped, recorded a 30-second retry, and synchronized on attempt 2 after the API returned.

The S3 adapter and disposable MinIO proof establish immutable writes, resumability, checksum verification, concurrent receipt publication, health checks, and container startup ordering. They do not claim production-provider retention or regional durability, bandwidth scheduling, billing enforcement, Windows installed execution, or Recovery Confidence.
