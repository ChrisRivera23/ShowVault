# Local queue synchronization

ShowVault's first synchronization executor consumes only verified recovery points from the durable desktop JSON queue. This slice proves the contract against a controlled filesystem object-store substitute; it is not a production cloud transport.

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

Normal builds do not configure the filesystem substitute and show no synchronization control for it. An isolated build must define both:

- `SHOWVAULT_SYNTHETIC_FIXTURE_HOME`
- `SHOWVAULT_SYNTHETIC_OBJECT_STORE_ROOT`

Only that explicit synthetic configuration enables **Synchronize pending**. A production object-store/API transport, authenticated session binding, tenant authorization, and production operational telemetry remain separate work.

## Current evidence and limitations

Automated synthetic tests prove successful remote verification, restart resume, idempotency, offline retry and retry exhaustion, local content tamper, added content, package links, queue-state links, object-store-root links, remote corruption, privacy filtering, and UI state refresh.

This slice does not claim production cloud synchronization, authenticated remote storage, multi-process concurrency, bandwidth scheduling, Windows installed execution, restore, dependency closure, or Recovery Confidence.
