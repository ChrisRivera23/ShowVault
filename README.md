# ShowVault

ShowVault is a venue-resilience platform that inventories production systems, creates verified recovery packages, and proves that critical show infrastructure can be restored.

The first product promise is intentionally focused:

> ShowVault can tell a venue what is installed, what is protected, whether its backup is usable, and exactly how to recover it.

## Current status

ShowVault is in Sprint 1: Architecture and Foundation.

Completed:

- Product direction and initial architecture approved.
- GitHub repository and CI foundation created.
- Flutter application shell created.
- Responsive desktop and mobile navigation created.
- ASP.NET Core health and status endpoints created.
- Flutter analysis and foundational tests passing.
- ASP.NET Core integration test passing.
- Foundation pull request merged into `main`.
- Control plane upgraded to .NET 10 LTS.
- Versioned Agent command and event contracts created.
- Venue Agent worker-service foundation created.
- Agent protocol endpoint and contract tests created.
- Current API dependency audit reports no known vulnerable packages.
- Auth0 selected as the managed human-identity provider.
- Auth0 Control Plane API created with RS256 access tokens.
- ASP.NET Core JWT issuer and audience validation added.
- Protected identity endpoint added for resolving the external identity subject.
- Provider-independent organization, membership, role, and venue domain foundations created.
- PostgreSQL schema and initial EF Core migration created for organizations, memberships, and venues.
- Authenticated organization and venue endpoints enforce membership and role-based tenant isolation.
- Venue-scoped Agent enrollment codes are short-lived, single-use, hashed at rest, and rate-limited.
- Durable Agent credentials use a separate authentication scheme and can be revoked by authorized venue managers.
- Venue Agent first-run bootstrap exchanges a one-time code and reuses its stored identity on restart.
- Agent credentials are stored in Windows Credential Manager or the macOS Keychain, never appsettings or SQLite.
- Agent credential rotation replaces the stored credential and invalidates the prior credential immediately.
- Venue Agent events and typed commands are durably queued in local SQLite.
- Authenticated event delivery retries with stable event IDs and PostgreSQL deduplication.
- Authorized venue managers can issue typed, expiring commands to a specific Agent.
- Agents poll with their separate credential, validate protocol/identity/expiry, persist commands to SQLite, and only then acknowledge receipt.
- Control-plane acknowledgements are idempotent, and local command state transitions are conditional and restart-safe.

Current development branch:

- `codex/agent-command-delivery` — durable control-plane command delivery and local command lifecycle.

Client sign-in, membership administration, command execution/cancellation, plugin, backup, verification, and restore functionality have not been implemented yet.

## Approved product direction

The long-term vision remains a production-infrastructure operating platform, but development will focus first on proving one complete recovery loop:

1. Create one organization and venue.
2. Enroll one Venue Agent.
3. Install one real plugin.
4. Discover one production application or device.
5. Create an immutable recovery package.
6. Verify its content and dependencies.
7. Restore it into a controlled target.
8. Produce an auditable recovery result.
9. Display the result in desktop and mobile applications.

The primary user workflow is:

1. Scan
2. Backup
3. Verify
4. Restore

## Approved system architecture

```text
Flutter Desktop and Mobile Applications
User interface, monitoring, approvals, and remote control
                     │
                     ▼
ASP.NET Core Control Plane
Identity, organizations, venues, policies, inventory,
jobs, alerts, audit, coordination, and reporting
                     │
           Secure outbound connection
                     │
                     ▼
.NET Venue Agent
Discovery, scheduling, plugins, backup,
verification, restore, and offline operation
                     │
          ┌──────────┼──────────┐
          ▼          ▼          ▼
       Devices   Local/NAS   Cloud object
       and apps    storage      storage
```

### Flutter applications

- One shared Flutter codebase for Windows, macOS, iOS, and Android.
- Desktop is the primary operational interface.
- Mobile is a companion for monitoring, alerts, approvals, and supported remote actions.
- Mobile is not responsible for persistent discovery or scheduled backup execution.
- SQLite will support local cached read models where offline viewing is valuable.

### Control plane

- ASP.NET Core modular monolith.
- PostgreSQL for business and operational metadata.
- S3-compatible object storage for large packages and assets.
- REST for application-facing operations.
- SignalR or WebSocket transport for live job status.
- A versioned HTTPS or gRPC protocol for Venue Agent communication.
- OpenTelemetry for logs, metrics, and traces.
- Managed OpenID Connect authentication; ShowVault will not handle passwords itself.

### Venue Agent

The Venue Agent is a separate .NET executable that will:

- Run as a Windows Service or macOS LaunchDaemon.
- Continue operating when the Flutter application is closed.
- Maintain a durable local SQLite job queue.
- Continue essential work without internet connectivity.
- Store credentials in the operating-system keychain.
- Connect outbound to the control plane.
- Buffer events until connectivity returns.
- Enforce plugin permissions.
- Execute discovery, backup, verification, and restore operations.
- Support local and NAS storage without requiring cloud availability.

The control plane must send typed operations such as `StartDiscovery` or `CreateBackup`; it must never send arbitrary shell commands.

### Data architecture

- PostgreSQL stores metadata, policies, relationships, jobs, manifests, and audit records.
- SQLite provides durable local Venue Agent state and selected client caches.
- Backup content is stored on local disk, NAS, or S3-compatible object storage—not inside PostgreSQL.
- Stable UUIDs identify domain objects.
- Immutable manifests and cryptographic hashes identify backup content.
- Graph views are derived from relational relationship records; no graph database is planned for the MVP.

### Plugin architecture

- Plugins never write directly to the control-plane database.
- Plugin output is treated as untrusted and validated by the Venue Agent.
- Plugins declare identity, version, compatibility, permissions, configuration, and capabilities.
- Initial plugins may be signed first-party modules.
- Third-party plugins should eventually run in an isolated subprocess or another suitable sandbox.
- The Plugin SDK will not be frozen until three representative integrations work end to end:
  1. A file-oriented application.
  2. A network-discovered device.
  3. A workstation/system inventory source.

### Recovery packages

Recovery packages must be immutable, versioned, content-addressed, independently verifiable, and usable without the cloud where practical.

A package manifest will record:

- Source identity.
- Plugin identity and version.
- Product and firmware versions.
- Backup timestamp.
- File inventory and cryptographic hashes.
- Dependencies and relationship snapshot.
- Restore prerequisites.
- Compatibility rules.
- Verification results.
- Optional encrypted content chunks.

### Verification levels

1. Structural verification — required package components exist.
2. Cryptographic verification — content matches recorded hashes and signatures.
3. Dependency verification — required software, firmware, assets, licenses, and devices are represented.
4. Compatibility verification — the package matches the proposed recovery target.
5. Restoration test — the package has been restored successfully in a controlled environment.

The Recovery Confidence Score must be explainable and derived from recorded evidence. It must not be an opaque or AI-generated percentage.

## Technology baseline

The project uses the newest stable, supported, production-appropriate releases available as of August 6, 2026. Preview, beta, and release-candidate software is not used for production foundations.

| Component | Approved baseline | Policy |
|---|---:|---|
| .NET / ASP.NET Core | 10.0 LTS | Use current .NET 10 security patch and SDK feature band. |
| Entity Framework Core | 10.x | Keep aligned with the .NET 10 runtime. |
| Flutter | 3.44 stable | Use current stable patch; 3.44.8 is installed locally. |
| Dart | 3.12.x | Use the version bundled with Flutter stable. |
| PostgreSQL | 18.x | Use current stable minor release; PostgreSQL 19 beta is not approved. |
| Docker Desktop | 4.84.x | Use current stable security-patched release. |
| Docker Compose | 5.x | Use the version bundled with supported Docker Desktop. |
| GitHub Actions | Current supported major actions | Pin major versions and review upgrades. |

Dependency policy:

- Prefer stable releases over previews.
- Prefer LTS runtimes for backend and agent services.
- Commit lockfiles.
- Use automated dependency and vulnerability updates.
- Review breaking upgrades intentionally rather than following `latest` blindly.
- Apply security patches promptly.
- Record major architectural upgrades in an Architecture Decision Record.

## Engineering approach

- Start as a modular monolith, not microservices.
- Use explicit module boundaries and constructor injection.
- Use EF Core directly within appropriate application/infrastructure boundaries.
- Do not create a universal generic repository abstraction.
- Keep commands typed and auditable.
- Keep long-running jobs durable, resumable, observable, and cancellable.
- Keep venue operations available during internet outages.
- Use established cryptographic libraries; never invent cryptography.
- Treat plugins, networks, files, and device responses as untrusted input.
- Add complexity only after a demonstrated operational need.

## Initial domain model

The first concrete model will include:

- Organization
- User
- Membership
- Venue
- Agent
- Device
- SoftwareInstallation
- Asset
- Relationship
- DiscoverySnapshot
- BackupPolicy
- BackupJob
- BackupPackage
- VerificationRun
- RecoveryPlan
- RestoreRun
- PluginInstallation
- AuditEvent

Universal object abstractions will be considered only after real plugin implementations reveal stable common behavior.

## Implementation sequence

1. Upgrade the API foundation from .NET 9 to .NET 10 LTS. — Complete
2. Freeze the initial control-plane and Venue Agent protocol boundary. — Complete
3. Select the managed OpenID Connect provider. — Complete (Auth0)
4. Implement organizations, venues, memberships, and tenant isolation. — Initial vertical slice complete
5. Implement secure Venue Agent enrollment and identity. — Initial end-to-end slice complete
6. Implement outbound Agent communication and durable local jobs. — Durable event delivery and command receipt complete; execution follows with the first plugin
7. Implement the first file-oriented discovery plugin. — Next
8. Define and create the immutable recovery-package format.
9. Implement cryptographic verification.
10. Implement a controlled local restore.
11. Display the complete recovery loop in Flutter.
12. Add the network-device and system-inventory plugins.
13. Add cloud upload and mobile monitoring.
14. Pilot repeatedly with one real venue.

## Conversation handoff

This section is maintained so a new Codex task can resume without relying on the previous chat transcript.

- Completed draft PR stack: PR #3 `codex/auth-tenancy-foundation`, PR #4 `codex/agent-enrollment-identity`, and PR #5 `codex/agent-outbound-queue`.
- Active work: `codex/agent-command-delivery`, stacked on PR #5.
- This slice adds PostgreSQL-issued Agent commands, manager authorization, authenticated polling, protocol/identity/expiry checks, local SQLite persistence before acknowledgement, idempotent acknowledgement, and guarded local states (`pending`, `running`, `completed`, `failed`, `cancelled`).
- The next implementation task is the first file-oriented discovery plugin and the minimal command executor that moves `StartDiscovery` through the durable state machine and emits outcome events.
- Auth0 is configured for human identity. Agent authentication intentionally remains a separate credential scheme.
- Exact Codex context-window percentages are not exposed to the assistant. A context compaction occurred while building this slice; this README is the durable source of truth for a new task.

## Explicitly deferred

- Hundreds of plugins and a plugin marketplace.
- AI recommendations.
- Full Digital Twin visualization.
- General-purpose workflow engine.
- Backup Box hardware.
- Government and enterprise compliance programs.
- Multi-region architecture.
- Microservices and Kubernetes.
- Graph database.
- Advanced analytics.
- Arbitrary automation scripting.
- General bidirectional offline editing.

## Repository structure

```text
apps/
  showvault_app/        Flutter desktop and mobile application
services/
  api/                  ASP.NET Core control plane
  agent/                Planned .NET Venue Agent
docs/                   Product and engineering records
infra/                  Local and deployment infrastructure
```

## Local prerequisites

- Flutter stable
- .NET 10 SDK
- Docker Desktop
- Git and GitHub CLI

## Current verification commands

```bash
cd apps/showvault_app
flutter pub get
flutter analyze
flutter test

cd ../../services/api
dotnet tool restore
dotnet tool run dotnet-ef migrations has-pending-model-changes \
  --project src/ShowVault.Api/ShowVault.Api.csproj \
  --startup-project src/ShowVault.Api/ShowVault.Api.csproj
dotnet test tests/ShowVault.Api.Tests/ShowVault.Api.Tests.csproj

cd ../agent
dotnet test tests/ShowVault.Agent.Tests/ShowVault.Agent.Tests.csproj
```

## Decisions still requiring product-owner approval

1. Auth0 native application identifiers and callback/logout URLs once Flutter platform runners exist.
2. First real plugin/product integration.
3. Initial storage targets: local disk only, local plus NAS, or local plus S3-compatible cloud.
4. First pilot venue and its recovery workflow.

## Instructions for a new development conversation

1. Read this entire README before making changes.
2. Inspect the repository, current branch, open pull requests, and test status.
3. Treat the architecture and scope above as approved.
4. Do not broaden the MVP without product-owner approval.
5. Discuss material architecture or product changes before implementing them.
6. Prefer one tested vertical slice over many placeholder modules.
7. Keep this README current when approved decisions, versions, repository status, or the next implementation step changes.
8. At significant milestones, update this handoff so another conversation can continue without reconstructing chat history.

## Handoff snapshot

The Product Owner approved the focused venue-resilience direction, the control-plane/Venue-Agent separation, Flutter clients, ASP.NET Core modular monolith, PostgreSQL metadata storage, SQLite local state, S3-compatible content storage, evidence-based verification, and a narrow one-venue/one-plugin recovery MVP.

Draft PR #3 establishes Auth0 and the PostgreSQL-backed tenant boundary. Draft PR #4 adds secure Agent enrollment, OS credential storage, revocation, and rotation. The stacked `codex/agent-outbound-queue` branch adds a durable SQLite event outbox and typed command queue. The Agent records connection events before attempting the network, retries failures with exponential backoff and stable event IDs, and the authenticated control plane persists each event once. The next vertical slice is control-plane command issuance, authenticated Agent command polling, and durable acknowledgement before execution; that slice should lead directly into the first file-oriented discovery plugin. macOS LaunchDaemon keychain access remains an installer-validation requirement. Keep this README current and continue through focused, validated draft pull requests.
