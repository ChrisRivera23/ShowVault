# Deployable prototype storage

This runbook describes ShowVault's versioned prototype API, PostgreSQL migration job, and S3-compatible hosted-sync backend. It does not establish regional durability, retention compliance, disaster recovery, or venue readiness. The included MinIO stack is disposable test infrastructure only.

## Runtime boundary

Production startup is fail-closed. `HostedSync:Provider` must be `S3`; `Disabled` and `FileSystem` are restricted to Development. The S3 adapter uses the AWS SDK default credential chain. No access key, secret, token, bucket credential, or customer local path belongs in source control, application settings, manifests, receipts, or logs.

Prefer a workload identity attached to the API runtime. Its policy should allow only:

- `s3:GetObject` and `s3:PutObject` below the configured `SHOWVAULT_S3_PREFIX`;
- `s3:ListBucket` on the configured bucket, restricted to that prefix;
- the bucket-location read needed by readiness checks.

The runtime does not need bucket creation, bucket deletion, object deletion, ACL, public-access, or policy-management rights. Provision a private bucket separately, block public access, require encryption in transit and at rest, and decide versioning, replication, retention, backup, and lifecycle policies explicitly with the deployment owner. A compatible provider must support conditional object creation (`If-None-Match: *`) and strongly consistent reads/listing for newly written keys.

## Object layout and completion rule

The server derives every key after tenant authorization. A client never supplies a storage root or local path.

```text
<prefix>/<organization-guid-n>/<venue-guid-n>/packages/<sha256-package-id>/
├── manifest.json
├── files/<sha256-logical-path>/chunks/<20-digit-offset>.chunk
└── receipt.json
```

Logical paths are validated against the frozen remote manifest and are represented in keys only by a SHA-256 digest. Manifest and chunk objects are create-only. A duplicate write succeeds only when the stored bytes match. Chunks must form one contiguous sequence, each at most 256 KiB. Commit relists the package, rejects unexpected objects, checks every size and SHA-256, and conditionally creates `receipt.json` last. The receipt is the sole completion marker. Concurrent commits either create that receipt or validate the identical receipt created by the winner.

There are no multipart uploads or mutable temporary objects in this implementation. A package without `receipt.json` is incomplete and safe to retry from the listed contiguous offset. Do not expose incomplete prefixes to restore or recovery listings.

## Configuration

Copy `infra/.env.prototype.example` to an ignored local file, replace every placeholder, and keep it outside artifact bundles and support diagnostics:

```sh
cp infra/.env.prototype.example .env
docker compose --env-file .env -f infra/docker-compose.prototype.yml config
```

For production-compatible S3, leave `SHOWVAULT_S3_SERVICE_URL` empty and `SHOWVAULT_S3_FORCE_PATH_STYLE=false` unless the selected provider requires otherwise. Custom production endpoints must use HTTPS. Static AWS variables in the example exist for bounded local use; leave them empty when a workload role is available.

The deployment uses exact container tags. `migrate` applies EF Core migrations once and must complete before the API starts. `/health` and `/health/live` report process liveness without dependencies. `/health/ready` checks the configured hosted-storage bucket.

Start or update the prototype:

```sh
docker compose --env-file .env -f infra/docker-compose.prototype.yml build migrate
docker compose --env-file .env -f infra/docker-compose.prototype.yml run --rm migrate
docker compose --env-file .env -f infra/docker-compose.prototype.yml up -d api
curl --fail http://127.0.0.1:${SHOWVAULT_API_PORT:-8080}/health/ready
```

For a bounded synthetic write/resume/commit check against the configured private bucket:

```sh
docker compose --env-file .env -f infra/docker-compose.prototype.yml run --rm api --smoke-hosted-sync
```

The smoke command writes a new random tenant/package prefix, retries one identical chunk, resumes from the durable offset, verifies the content, commits twice, and requires one stable receipt. It prints only the path-free package ID. Remove the synthetic prefix later under an explicitly approved retention procedure; the runtime intentionally has no delete permission.

## Disposable local parity

The override supplies pinned MinIO and disposable credentials for controlled development only:

```sh
docker compose --env-file infra/.env.prototype.example \
  -f infra/docker-compose.prototype.yml \
  -f infra/docker-compose.s3-test.yml up --build --wait api

docker compose --env-file infra/.env.prototype.example \
  -f infra/docker-compose.prototype.yml \
  -f infra/docker-compose.s3-test.yml run --rm api --smoke-hosted-sync

docker compose --env-file infra/.env.prototype.example \
  -f infra/docker-compose.prototype.yml \
  -f infra/docker-compose.s3-test.yml down --volumes
```

This exercises the adapter contract and deployment order; it is not evidence for the selected provider's durability, IAM, encryption, retention, networking, or regional failure behavior.

## Incomplete-prefix cleanup

Cleanup is an operator-controlled maintenance operation; automatic deletion is not implemented. Inventory package prefixes without `receipt.json`, record their organization, venue, package ID, newest-object time, and total bytes, and compare them with active queue attempts. Delete only prefixes older than the approved grace period after confirming that no client is uploading or retrying them. Never delete individual chunks from an active prefix and never delete any prefix containing a valid receipt. Use a separate narrowly scoped maintenance identity, dry-run the exact prefix list, retain the audit output, and remove delete permission afterward.

## Filesystem-to-object migration

The Development filesystem backend remains readable for controlled legacy packages; production cannot select it. Migration is an attended offline procedure, not an automatic startup action:

1. Stop API writes and back up PostgreSQL plus the complete filesystem hosted-sync root.
2. Inventory `committed/<organization>/<venue>/<package>` directories. Reject links, partial directories, malformed tenant/package IDs, missing receipts, and any package whose manifest, receipt identity, file set, size, or SHA-256 does not verify.
3. For each verified package, call the object-store contract with the original tenant IDs, package ID, and exact manifest. Upload verified content in at-most-256-KiB chunks. Never copy filesystem names directly into object keys.
4. Commit through the adapter so it relists and rehashes the package and publishes the receipt last. Compare the returned manifest digest and package ID with the source receipt.
5. Produce a path-free reconciliation report with source count, destination receipt count, package IDs, manifest digests, failures, and timestamps. Re-run idempotently until every approved package reconciles.
6. Start the API with `HostedSync:Provider=S3`, run readiness and a synthetic smoke check, then resume clients. Keep the filesystem source read-only through the rollback window.

A migration utility implementing step 3 is still required before real legacy data is moved. Do not use shell-recursive bucket copies: they bypass logical-path hashing, conditional writes, checksum validation, and receipt-last publication.

## Rollback and incident response

Before deployment, record the image tag, database backup/restore point, object prefix, and prior configuration. If migration fails, leave the API stopped, preserve logs without secrets or content, and restore the database only from a verified backup compatible with the prior image. Do not run a blind down-migration. Object writes are immutable, so an abandoned new-version prefix can remain isolated while the prior version is assessed.

Do not roll production back to the filesystem provider; it is intentionally rejected outside Development. If S3 is unavailable, readiness fails and hosted-sync endpoints return retryable unavailability while desktop recovery points and the append-only local queue remain intact. Local Save, verify, and attended local restore remain the recovery boundary.
