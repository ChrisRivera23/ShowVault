# Local macOS prototype runbook

This runbook reproduces the authenticated Scan → Backup → Verify → Restore proof on a macOS development host. It uses a controlled local filesystem fixture and does not substitute for a deployed-environment or real-venue pilot.

## Prerequisites

- Docker Desktop with PostgreSQL 18 running through `infra/docker-compose.yml`.
- .NET 10 SDK, Flutter 3.44 stable, Xcode, and CocoaPods.
- Auth0 Native application and Control Plane API configured with the repository domain, client ID, audience, and callback URLs.
- The development API permits user-delegated access. Machine-to-machine access remains separately restricted.

## Start the control plane

```bash
docker compose -f infra/docker-compose.yml up -d postgres

cd services/api
dotnet tool restore
dotnet tool run dotnet-ef database update \
  --project src/ShowVault.Api/ShowVault.Api.csproj \
  --startup-project src/ShowVault.Api/ShowVault.Api.csproj
dotnet run --no-launch-profile \
  --project src/ShowVault.Api/ShowVault.Api.csproj
```

The local proof uses `http://127.0.0.1:5000`. Production and non-loopback environments must use HTTPS.

## Start the native client

```bash
cd apps/showvault_app
flutter run -d macos \
  --dart-define=SHOWVAULT_API_BASE_URL=http://127.0.0.1:5000
```

Sign in through the macOS system authentication session. Verify that the client loads the expected organization and venue from the API rather than preview data.

## Prepare a controlled fixture

Choose explicit absolute paths. The discovery root and restore root must be configured on the Agent before startup.

```bash
mkdir -p .prototype/source .prototype/restore .prototype/agent-data .prototype/packages
printf 'ShowVault controlled recovery fixture\n' > .prototype/source/showvault-prototype.txt
```

The configured restore root must already exist and must not be a symbolic link. The workflow restore target must be an absent or empty child beneath that root, such as `.prototype/restore/run-1`; do not select the root itself.

## Enroll and start the Agent

Create a short-lived, single-use enrollment through the authenticated enrollment endpoint. Supply the returned `sve_…` code only to the first Agent process invocation; never write it to appsettings or source control.

```bash
cd services/agent
Agent__ControlPlaneUri=http://127.0.0.1:5000 \
Agent__Name='Prototype Mac Agent' \
Agent__EnrollmentCode='<one-time-enrollment-code>' \
Agent__DataDirectory='<absolute-path>/.prototype/agent-data' \
Agent__PackageDirectory='<absolute-path>/.prototype/packages' \
Agent__DiscoveryRoots__0='<absolute-path>/.prototype/source' \
Agent__RestoreRoots__0='<absolute-path>/.prototype/restore' \
dotnet run --no-launch-profile \
  --project src/ShowVault.Agent/ShowVault.Agent.csproj
```

After enrollment, remove `Agent__EnrollmentCode`. The durable Agent identity is stored in macOS Keychain and reused on restart.

## Execute and verify

In the native dashboard:

1. Select `Prototype Mac Agent`.
2. Use plugin `showvault.filesystem`.
3. Enter the exact allowlisted discovery root and queue **Scan**.
4. Queue **Backup** after discovery completes.
5. Queue **Verify** after backup completes.
6. Enter an absent or empty child beneath the restore root.
7. Review the confirmation and queue **Restore**.

Success requires all four durable completion events, structural and SHA-256 verification passing, the dashboard reporting `Recovery loop proven`, and an exact hash match:

```bash
shasum -a 256 \
  .prototype/source/showvault-prototype.txt \
  .prototype/restore/run-1/showvault-prototype.txt
```

Keep the PostgreSQL event evidence, Agent SQLite state, immutable package, verification evidence digest, restoration evidence digest, and matching hashes with the pilot record.
