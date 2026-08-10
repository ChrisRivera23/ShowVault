# Local-first milestone 4 extraction manifest

## Outcome

Milestone 4 reconstructs the deployable S3-compatible hosted-sync provider,
production fail-closed configuration, health and smoke checks, and pinned
container/PostgreSQL migration topology on the then-current milestone-3 branch.

This is a local planning artifact. It does not authorize cloud resources,
credentials, pushes, PR operations, merges, workflow dispatch, deployments,
external equipment, destructive bucket cleanup, personal data, or venue use.

The local recovery point remains authoritative. Provider unavailability must
degrade only cloud synchronization and readiness, never local Save, Verify, or
Restore.

## Historical source boundary

Use the two-commit range `fff4434..69b83ab`:

| Commit | Historical concern |
| --- | --- |
| `c965719` | Deployable object-storage implementation and container topology |
| `69b83ab` | Storage operations, migration, rollback, and evidence limits |

The range has 28 net files and no transient net-zero files. It adds 17 files;
three files overlap the paused legacy slice, three overlap milestone 1, two
overlap milestone 2, and nine extend milestone 3.

## Reconstruction order

Build the milestone as four code/infrastructure commits plus the operating
runbook. Split shared files by concern rather than replaying the complete diff.

### 1. Provider abstraction and fail-closed configuration

Add or reconstruct:

- `services/api/src/ShowVault.Api/HostedSync/IHostedSyncStore.cs`;
- `services/api/src/ShowVault.Api/HostedSync/IHostedObjectStore.cs`;
- `services/api/src/ShowVault.Api/HostedSync/DisabledHostedSyncStore.cs`;
- the provider-neutral portions of
  `services/api/src/ShowVault.Api/HostedSync/ObjectHostedSyncStore.cs`;
- `services/api/src/ShowVault.Api/HostedSync/HostedSyncOptions.cs`;
- `services/api/src/ShowVault.Api/HostedSync/HostedSyncOptionsValidator.cs`;
- `services/api/src/ShowVault.Api/HostedSync/HostedSyncServiceCollectionExtensions.cs`;
- matching registration/configuration in
  `services/api/src/ShowVault.Api/Program.cs` and `appsettings.json`;
- the provider abstraction in the existing Development filesystem
  `HostedSyncStore.cs`.

Required behavior:

- Production accepts only the S3 provider and fails startup on disabled,
  filesystem, missing, malformed, or unsafe configuration;
- Disabled and server-owned filesystem providers are Development-only;
- custom production endpoints require HTTPS, while HTTP is allowed only in
  Development for a controlled emulator;
- bucket, region, prefix, service URL, and path-style settings are bounded and
  validated on startup;
- the client cannot choose a provider, storage root, bucket, prefix, or local
  path; and
- storage unavailability is retryable and cannot delete or downgrade a locally
  verified package.

### 2. Immutable S3 object adapter and commit protocol

Add or reconstruct:

- `services/api/src/ShowVault.Api/HostedSync/S3HostedObjectStore.cs`;
- `services/api/src/ShowVault.Api/HostedSync/ObjectHostedSyncStore.cs`;
- `AWSSDK.S3` in `services/api/src/ShowVault.Api/ShowVault.Api.csproj`;
- object-store cases in
  `services/api/tests/ShowVault.Api.Tests/ObjectHostedSyncStoreTests.cs`;
- disposable adapter integration cases in
  `services/api/tests/ShowVault.Api.Tests/S3HostedSyncIntegrationTests.cs`;
- matching fixture configuration in `TenantApiFactory.cs`.

Required object layout:

```text
<prefix>/<organization-guid-n>/<venue-guid-n>/packages/<package-sha256>/
├── manifest.json
├── files/<sha256-logical-path>/chunks/<20-digit-offset>.chunk
└── receipt.json
```

Required behavior:

- derive every key after tenant authorization from bounded server-owned values;
- hash logical paths before placing them in object keys;
- create manifests and chunks conditionally and immutably;
- accept a duplicate only when stored bytes are identical;
- enforce contiguous chunks no larger than 256 KiB and derive the resumable
  offset from the durable listing;
- relist the complete package at commit, reject unexpected objects and gaps,
  and independently verify all sizes and SHA-256 values;
- conditionally create `receipt.json` last as the sole completion marker; and
- make concurrent commit idempotent by validating the identical winner receipt.

Do not introduce multipart uploads, mutable temporary objects, public ACLs, or
runtime delete operations. Incomplete prefixes remain retryable and are not
visible as completed recovery copies.

### 3. Readiness and bounded synthetic smoke

Add or reconstruct:

- `services/api/src/ShowVault.Api/HostedSync/HostedSyncHealthCheck.cs`;
- `services/api/src/ShowVault.Api/HostedSync/HostedSyncSmokeCheck.cs`;
- readiness/error mapping in
  `services/api/src/ShowVault.Api/Endpoints/HostedSyncEndpoints.cs`;
- liveness/readiness and `--smoke-hosted-sync` handling in
  `services/api/src/ShowVault.Api/Program.cs`.

Required behavior:

- `/health` and `/health/live` report process liveness without storage
  dependencies;
- `/health/ready` checks the selected storage provider;
- unavailable storage produces a bounded retryable response, not a false
  integrity failure;
- the one-shot smoke command uses a random synthetic tenant/package identity,
  repeats one identical chunk, resumes, verifies, commits twice, and emits only
  a path-free package ID; and
- smoke data is never treated as customer or retention evidence.

### 4. Pinned image, migration, and disposable parity topology

Add or reconstruct:

- `.dockerignore` and the local configuration exclusion in `.gitignore`;
- `infra/Dockerfile.api`;
- `infra/docker-compose.prototype.yml`;
- `infra/docker-compose.s3-test.yml`;
- `infra/.env.prototype.example`.

Required behavior:

- use reviewed exact SDK/runtime, PostgreSQL, MinIO, and MinIO-client tags;
- build a non-root, read-only runtime image with only bounded temporary space;
- run EF migrations as a one-shot job after PostgreSQL health and before API
  startup;
- require all production placeholders and select S3 explicitly;
- support workload identity through the AWS SDK default credential chain;
- keep disposable static emulator credentials confined to the Development
  override; and
- name and scope disposable volumes so cleanup cannot affect unrelated Docker
  state.

Do not add access keys, secrets, tokens, real bucket names, venue identity, or
customer paths to source, images, Compose output, manifests, logs, diagnostics,
or artifacts.

### 5. Operations, migration, and rollback contract

Reconcile:

- `docs/DEPLOYABLE_PROTOTYPE_STORAGE.md`;
- `docs/LOCAL_QUEUE_SYNC.md`;
- `docs/PROTOTYPE_READINESS.md`;
- `README.md`.

The runbook must preserve these boundaries:

- a private bucket is provisioned separately with public access blocked and
  encryption, versioning, replication, retention, backup, and lifecycle decided
  explicitly by the deployment owner;
- runtime IAM is prefix-scoped to required Get/Put/List/location operations and
  has no bucket creation, policy, ACL, or delete power;
- incomplete-prefix deletion is an attended, dry-run, separately authorized
  maintenance action using a temporary narrow identity;
- filesystem-to-object migration rejects links/tamper and passes all bytes
  through the object contract rather than recursively copying names;
- no real migration proceeds before its bounded migration utility exists;
- rollback records image/configuration/database restore points, never blindly
  down-migrates, and never returns Production to filesystem storage; and
- MinIO remains emulator evidence only, not a production durability claim.

## Complete file accounting

The 28 net files divide into:

- 18 API source/project/config/test files; and
- 10 repository, infrastructure, and runbook files.

Reproduce the accounting from the repository root:

```bash
test "$(git rev-list --count fff4434..69b83ab)" = 2
test "$(git diff --name-only fff4434..69b83ab | sort -u | wc -l | tr -d ' ')" = 28
test "$(git diff --diff-filter=A --name-only fff4434..69b83ab | wc -l | tr -d ' ')" = 17

legacy_files="$(mktemp)"
milestone_1_files="$(mktemp)"
milestone_2_files="$(mktemp)"
milestone_3_files="$(mktemp)"
milestone_4_files="$(mktemp)"
git diff --name-only 254cbbf..310190c | sort -u > "$legacy_files"
git diff --name-only 310190c..ce5be25 | sort -u > "$milestone_1_files"
git diff --name-only ce5be25..c172e49 | sort -u > "$milestone_2_files"
git diff --name-only c172e49..fff4434 | sort -u > "$milestone_3_files"
git diff --name-only fff4434..69b83ab | sort -u > "$milestone_4_files"
test "$(comm -12 "$legacy_files" "$milestone_4_files" | wc -l | tr -d ' ')" = 3
test "$(comm -12 "$milestone_1_files" "$milestone_4_files" | wc -l | tr -d ' ')" = 3
test "$(comm -12 "$milestone_2_files" "$milestone_4_files" | wc -l | tr -d ' ')" = 2
test "$(comm -12 "$milestone_3_files" "$milestone_4_files" | wc -l | tr -d ' ')" = 9
```

Temporary accounting files may be discarded through normal temporary-file
cleanup. Do not broaden cleanup beyond those exact files.

## Verification gate

After reconstruction, run at minimum:

```bash
dotnet test services/contracts/tests/ShowVault.AgentContracts.Tests/ShowVault.AgentContracts.Tests.csproj
dotnet test services/platform/tests/ShowVault.Platform.Tests/ShowVault.Platform.Tests.csproj
dotnet test services/agent/tests/ShowVault.Agent.Tests/ShowVault.Agent.Tests.csproj
dotnet tool run dotnet-ef migrations has-pending-model-changes \
  --project services/api/src/ShowVault.Api/ShowVault.Api.csproj \
  --startup-project services/api/src/ShowVault.Api/ShowVault.Api.csproj
dotnet test services/api/tests/ShowVault.Api.Tests/ShowVault.Api.Tests.csproj

docker compose --env-file infra/.env.prototype.example \
  -f infra/docker-compose.prototype.yml \
  -f infra/docker-compose.s3-test.yml config
docker build -f infra/Dockerfile.api -t showvault-api:milestone-4 .
```

For a separately approved disposable local execution, start only the named
prototype/MinIO project, run the one-shot migration and smoke command, verify
readiness and one stable receipt, then remove only its disposable containers,
network, and volumes. This manifest does not itself authorize that mutation.

Also run the complete Flutter suite and `git diff --check`, because storage
failure must remain isolated from desktop local recovery behavior.

Audit source, rendered Compose configuration, image history, logs, and smoke
output for credentials, tokens, local/customer paths, mutable keys, unhashed
logical paths, cross-tenant prefixes, public access, runtime delete permission,
unsafe custom endpoints, filesystem production fallback, unbounded listings,
receipt-before-verification, and cleanup broader than the named disposable
project.

Passing this gate does not establish production-provider retention, regional
durability, IAM correctness in a real account, ingress/TLS, monitoring, billing,
bandwidth scheduling, clean rollback, Windows execution, venue readiness, or
Recovery Confidence.
