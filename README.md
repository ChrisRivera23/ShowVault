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
- The first-party filesystem discovery plugin inventories and SHA-256 hashes files only within locally allowed roots.
- `StartDiscovery` commands execute from the durable queue, resume after restart, and emit idempotent completion or failure events.
- `CreateBackup` writes an atomic, immutable, content-addressed local recovery package from a completed discovery inventory.
- Recovery package manifests use format 1.0 and record source/plugin identity, hashes, dependencies, relationships, restore prerequisites, compatibility rules, and verification evidence.
- `VerifyBackup` independently validates exact package structure, manifest identity, file sizes, and SHA-256 content hashes without following links.
- Verification results are stored once with an evidence digest; detected corruption completes with a failed integrity result rather than an executor error.
- `StartRestore` requires passing verification and restores only beneath locally allowed roots into absent or empty targets.
- Restore copying uses staging, immediate package revalidation, restored-file hashing, atomic publication, restart-safe intent, and write-once evidence.
- The control plane derives tenant-scoped recovery runs from issued commands and durable Agent outcomes without exposing local filesystem paths.
- Flutter native runners now exist for Android, iOS, macOS, and Windows under the shared `com.showvault.app` identity.
- The Flutter client uses Auth0 Universal Login, securely restores mobile/macOS sessions, and requests the ShowVault API audience.
- Authenticated Flutter loading discovers the operator's first accessible organization and venue and renders only live tenant-scoped recovery history; preview data has been removed.
- Read-only system inventory and exact allowlisted TCP endpoint discovery run through the durable Agent command boundary.
- The authoritative Version 1 catalog spans professional audio, audio networking/DSP, lighting platforms/protocols, video/media servers, DJ platforms, and projection; Resolume, Yamaha, and grandMA2/grandMA3 are highest priority.
- Q-SYS Designer offline recovery requires an editable `.qsys` design within an exact locally approved root and preserves colocated plugins, user-library components, scripts, media, notes, and other recovery companions.
- ETC Eos recovery accepts native `.esf3d`, `.esf2`, and legacy `.esf` show files within an exact locally approved archive root and preserves timestamped revisions, settings backups, exports, and other colocated recovery companions.
- Dante Controller recovery requires a saved XML network preset within an exact locally approved root and preserves colocated event and clock logs, diagrams, inventories, and restore notes as evidence and companions.
- Allen & Heath SQ recovery protects one exact numbered `AHSQ/SHOWS/SHOW####` directory at a time, requires its `SHOW.DAT` anchor, and preserves all scene, library, and mixer-configuration data without sweeping SQ-Drive recordings.
- Crestron SIMPL Windows recovery requires editable `.smw` source within an exact locally approved project/archive root and preserves colocated SIMPL+ modules, user macros, IR files, VT Pro-e source, Smart Graphics definitions, compiled deployment artifacts, drivers, and documentation.
- Shure Designer 6 recovery requires at least one current `.rdf` room design within an exact locally approved root and preserves legacy `.dprj` projects, floor plans, reports, and non-secret deployment documentation.
- Blackmagic ATEM recovery validates an ATEM `Profile` XML file within an exact locally approved save root and preserves timestamped switcher-state revisions, Media Pool contents, macro exports, and restore notes without accepting unrelated XML.
- DiGiCo SD/Quantum recovery requires at least one `.ses` session within an exact operator-declared SD/Quantum root and preserves revisions, templates, converter outputs, presets, and compatibility notes while keeping S-Series separate.
- SSL Live recovery requires at least one native `.show` showfile within an exact operator-declared root and preserves revisions, DataBackup diagnostics, presets, and restore notes while treating console model, software version, sample rate, clocking, I/O, and network settings as restore prerequisites.
- Lawo mc² recovery requires at least one complete `.lpn` production within an exact operator-declared root and preserves colocated snapshots, presets, copied Waves Integrated Sessions, and restore notes while retaining MCX/mxGUI build, hardware, DSP, I/O, HOME topology, and sample-rate boundaries.
- Calrec Apollo/Artemis recovery requires at least one native `.CalrecShow` export within an exact operator-declared root and preserves revisions, legacy-backup migration material, and restore notes while keeping console model, software version, sample rate, and system topology as supervised restore prerequisites.
- Studer Vista recovery requires a non-empty, utility-generated `BCK_D950_BACKUP…` title-backup directory within an exact operator-declared root and preserves the complete title, snapshot, preset, session-configuration, system-file, and restore-documentation tree rather than accepting loose `.snp` or `.pre` files.
- Midas PRO Series/XL8 recovery requires at least one exported `.show` file within an exact operator-declared root and preserves revisions, user-library/patching presets, and restore notes while explicitly rejecting M32 `.shw`/`.scn` artifacts and retaining model capacity, software, I/O hardware, and patching as supervised restore prerequisites.
- Behringer WING recovery requires a `.show` index within an exact operator-declared folder and preserves every referenced snapshot, snippet, preset, audio clip, revision, and restore note because WING show files contain references rather than the referenced data; X32 `.shw`/`.scn` artifacts remain a separate target.
- Soundcraft Vi recovery requires the documented `Snapshots` directory with at least one native `.snp` file within an exact operator-declared showfolder and preserves all ancillary files plus `.bk1`/`.bk2` restart backups while retaining console/software, Vistonics-bay/GEQ mode, installed hardware, and downgrade compatibility as supervised prerequisites.
- Tascam Model 12/16/24/2400 recovery protects one complete song directory directly beneath `MTR`, requires track `.wav` data, and preserves all internal metadata and audio without accepting `MUSIC` mixdowns; target model/channel layout, firmware, and sample format remain supervised restore prerequisites.

Current development branch:

- `codex/tascam-model-mtr-songs` — complete Tascam Model-series MTR song directories.

The Auth0 Native application is configured. A live login/API proof still needs a deployed API and a native build host. Membership administration, user-requested command cancellation, digital signatures, NAS/cloud storage, and persistent control-plane package records have not been implemented yet.

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
6. Implement outbound Agent communication and durable local jobs. — Initial event, command, and discovery execution loop complete
7. Implement the first file-oriented discovery plugin. — Complete (generic locally allowlisted filesystem integration)
8. Define and create the immutable recovery-package format. — Complete (local format 1.0)
9. Implement cryptographic verification. — Initial structural and SHA-256 integrity verification complete
10. Implement a controlled local restore. — Complete (allowlisted test targets)
11. Display the complete recovery loop in Flutter. — Native client, Auth0 application registration, and authenticated live loading implemented; end-to-end login/API proof awaits a deployed API and a native build host
12. Add the network-device and system-inventory plugins. — Bounded, read-only system inventory and allowlisted TCP network-device discovery implemented
13. Implement and pilot the approved Version 1 vendor integrations. — Priority and representative foundations implemented; remaining professional-audio manufacturer wave now covers DiGiCo SD/Quantum, SSL Live, Lawo mc², Calrec Apollo/Artemis, Studer Vista, Midas PRO Series/XL8, Behringer WING, Soundcraft Vi, and Tascam Model-series MTR
14. Add cloud upload and mobile monitoring.
15. Pilot repeatedly with one real venue.

## Conversation handoff

This section is maintained so a new Codex task can resume without relying on the previous chat transcript.

- Completed draft PR stack: PRs #3 through #11, ending with `codex/recovery-history-read-model`.
- Draft PR #12 branch: `codex/flutter-auth0-live-history`, stacked on PR #11. It adds native Android/iOS/macOS/Windows runners, Auth0 Universal Login/session restoration, bearer-authenticated tenant discovery and recovery-history loading, and removes all preview recovery records.
- Draft PR #13 branch: `codex/system-inventory-plugin`, stacked on PR #12. It adds protocol 1.1 `CollectSystemInventory` and bounded, permission-scoped, read-only host inventory.
- Draft PR #14 branch: `codex/network-device-discovery`, stacked on PR #13. It advances the protocol to 1.2 and adds exact local `host:port` allowlisting, bounded TCP reachability probes, durable results, and completion events without subnet sweeps or banner collection.
- Draft PR #15 branch: `codex/resolume-portable-bundle`, stacked on the network-discovery branch. It adds the first vendor workflow around a Resolume Collect Media portable show bundle and the existing immutable package/verify/controlled-restore loop.
- Draft PR #16 branch: `codex/resolume-user-data`, stacked on PR #15. It adds a distinct, exact-root recovery unit for compositions, fixtures, preferences, presets, recordings, and shortcut profiles while rejecting arbitrary folders.
- Draft PR #17 branch: `codex/grandma-show-backups`, stacked on PR #16. It adds distinct exact-root grandMA2 and grandMA3 USB/export discovery and preserves their separate version-compatibility boundaries.
- Draft PR #18 branch: `codex/yamaha-console-exports`, stacked on PR #17. It adds exact-root DM7 `.dm7f` and current/legacy RIVAGE PM settings-export recovery as separate compatibility targets.
- Draft PR #19 branch: `codex/yamaha-clql-tf-exports`, stacked on PR #18. It adds exact-root CL/QL `.CLF` and TF `.TFF` settings-export recovery with cross-family rejection.
- Draft PR #20 branch: `codex/yamaha-dm3-exports`, stacked on PR #19. It requires a `.DM3F` all-settings artifact and preserves companion `.DM3S` scenes and `.DM3P` presets.
- Draft PR #21 branch: `codex/yamaha-dsp-projects`, stacked on PR #20. It adds distinct DME7 ProVisionaire Design `.pvd` and MTX/MRX Editor `.mtx` recovery targets.
- Draft PR #22 branch: `codex/yamaha-pc-amplifiers`, stacked on PR #21. It adds a separate PC-D/DI amplifier compatibility target for operator-declared ProVisionaire Design `.pvd` projects.
- Draft PR #23 branch: `codex/yamaha-provisionaire-control`, stacked on PR #22. It requires an editable Control PLUS `.pvcppj` project and preserves exported `.pvksk` Kiosk controllers and companion assets.
- Draft PR #24 branch: `codex/yamaha-dme5-dme3`, stacked on PR #23. It adds DME5/DME3 as a separate ProVisionaire Design `.pvd` compatibility target and preserves Custom Control Panel exports as companions.
- Q-SYS branch: `codex/qsys-offline-design`, based on PR #24. It adds exact-root Q-SYS Designer offline recovery, requires an editable `.qsys` design, and preserves colocated `.qplug` plugins, `.quc` user-library components, and other recovery companions. Official Q-SYS documentation confirms Designer/Core firmware version alignment remains an operator restore prerequisite.
- ETC Eos branch: `codex/etc-eos-show-files`, based on the Q-SYS branch. It adds exact-root Eos show-archive recovery, requires at least one native `.esf3d`, `.esf2`, or `.esf` show file, and preserves timestamped revisions, `.ini` system-settings backups, documented exports, and other recovery companions. Official ETC documentation confirms `.esf3d` requires Eos 3.0.0 or later and `.esf2` requires 2.9.0 or later.
- Dante branch: `codex/dante-controller-presets`, based on the ETC Eos branch. It adds exact-root Dante Controller recovery, requires a saved XML preset, and preserves colocated `.log` event evidence, XML clock logs, diagrams, inventories, and restore notes. Preset application remains supervised because device-role matching, subscription replacement, model capabilities, static addresses, clocking, and firmware can affect recovery safety.
- Allen & Heath branch: `codex/allen-heath-sq-shows`, based on the Dante branch. It adds exact-root recovery for original SQ show directories under `AHSQ/SHOWS`, requires the numbered `SHOW####` directory and its `SHOW.DAT` anchor, and preserves the entire show folder. SQ+, dLive, Avantis, Qu, CQ, and AHM remain separate compatibility targets; SQ+ is explicitly rejected because it uses `AHSQ/SQP-SHW`.
- Crestron branch: `codex/crestron-simpl-projects`, based on the Allen & Heath branch. It adds exact-root SIMPL Windows recovery, requires editable `.smw` source, and preserves colocated `.usp` modules, user macros, IR files, VT Pro-e `.vtp` source, Smart Graphics definitions, compiled `.cpz`/`.lpz` programs, `.vtz`/`.ch5z` UI packages, drivers, and documentation. SIMPL#, Crestron Construct source, D3 Pro, Crestron Home, and `.AV Framework` remain separate compatibility targets.
- Shure branch: `codex/shure-designer-rooms`, based on the Crestron branch. It adds exact-root Shure Designer 6 recovery, requires a current `.rdf` room design, and preserves legacy `.dprj` projects, floor plans, reports, and non-secret deployment notes. Wireless Workbench `.shw` shows and Show Packs remain a separate live-RF recovery workflow, and device passphrases and IntelliMix license credentials must remain in an approved secret store rather than recovery packages.
- Blackmagic branch: `codex/blackmagic-atem-state`, based on the Shure branch. It adds exact-root ATEM switcher-state recovery, securely validates an XML `Profile` whose product identifies an ATEM switcher, rejects unrelated XML and document-type declarations, and preserves timestamped state revisions, Media Pool folders, macro exports, and restore notes. DaVinci Resolve `.drp`/`.dra`, HyperDeck, Videohub, camera, and other Blackmagic workflows remain separate compatibility targets.
- DiGiCo branch: `codex/digico-sd-quantum-sessions`, based on the Blackmagic branch. It adds exact-root recovery for operator-declared DiGiCo SD/Quantum `.ses` sessions and preserves revisions, templates, converted sessions, presets, and compatibility notes. Target console model, software build, sample rate, I/O structure, and licensed extensions remain restore prerequisites. S-Series remains separate because DiGiCo states that S and SD/Quantum sessions are fundamentally different and cannot be converted; the `.ses` extension alone cannot identify the family.
- SSL/Lawo branch: `codex/audio-console-recovery-wave`, based on the DiGiCo branch. It adds two independently configured exact-root targets: SSL Live native `.show` showfiles and Lawo mc² complete `.lpn` productions. Both preserve colocated recovery companions while rejecting incomplete or child folders; console software/build, hardware topology, sample rate, clocking, I/O, and applicable network or licensed-extension state remain supervised restore prerequisites.
- Calrec branch: `codex/calrec-apollo-artemis-shows`, based on the SSL/Lawo branch. It adds exact-root recovery for Apollo/Artemis `.CalrecShow` exports and preserves revisions, legacy folder-backup migration material, and restore notes. Current Calrec documentation defines one file per show and continued legacy import support; Brio, Type R, Argo, and other Calrec platforms remain separate compatibility targets until their formats are independently verified.
- Studer branch: `codex/studer-vista-title-backups`, based on the Calrec branch. It adds exact-root recovery for non-empty Vista `BCK_D950_BACKUP…` directories created by Make Backup and preserves every contained title and pertinent configuration/system file. Loose `.snp` snapshots and `.pre` presets do not qualify; Vista model, software release, Session Configuration ID, DSP/hardware topology, and supervised import remain restore prerequisites.
- Midas branch: `codex/midas-pro-show-files`, based on the Studer branch. It adds exact-root recovery for PRO Series/XL8 `.show` exports and preserves revisions, user-library/patching presets, and restore notes. PRO2 documentation describes compatibility across PRO Series/XL8, but destination channel/bus capacity, software, installed I/O hardware, and system patching remain supervised boundaries. HD96, M32, and M AIR are deliberately separate targets; M32 `.shw` and `.scn` files are rejected.
- Behringer branch: `codex/behringer-wing-show-folders`, based on the Midas branch. It adds exact-root recovery for WING folders containing a `.show` index and preserves referenced `.snap`, `.snip`, presets, audio clips, revisions, and restore notes. WING documentation explicitly states that a show contains references rather than referenced content, so the complete colocated folder is the recovery unit. X32 `.shw` and `.scn` files are rejected and remain a separate compatibility target.
- Soundcraft branch: `codex/soundcraft-vi-showfolders`, based on the Behringer branch. It adds exact-root recovery for Vi showfolders with the required `Snapshots` directory and native `.snp` data, preserving ancillary files and `.bk1`/`.bk2` restart backups. Vi console/software version, surface bay count and GEQ display mode, installed hardware, and newer-to-older software compatibility remain supervised restore boundaries. Si and Ui show exports remain separate targets.
- Current branch: `codex/tascam-model-mtr-songs`, based on the Soundcraft branch. It protects one exact song directory directly under the Model-series `MTR` folder, requires recorded `.wav` track data, and preserves every internal file because Tascam warns that renaming or deleting individual files can make a song unloadable. `MUSIC` mixdowns are rejected; Model 12/16/24/2400 hardware/channel layout, firmware, and sample format remain supervised restore boundaries. Sonicview remains separate.
- Avid VENUE is deliberately deferred until a verified real S6L export fixture establishes a dependable show anchor and directory boundary. Current official documentation describes category/folder backup and transfer behavior but does not provide a sufficiently reliable standalone show-file extension or signature. Password files, Waves licenses, and other credentials must never be collected as recovery content.
- Next recommended implementation step: continue the professional-audio manufacturer wave with Roland Pro A/V after verifying its current mixer/project formats, recovery unit boundaries, and model/firmware compatibility from official documentation.
- Auth0 Native application `ShowVault Flutter` is registered and its callback/logout URLs and public Client ID are configured. A live login/API proof still requires a deployed ShowVault API and a native build host; this workstation has only Xcode Command Line Tools.
- The Product Owner supplied the authoritative Version 1 venue-technology scope in `docs/INTEGRATION_CATALOG.md`, including rekordbox, Serato DJ Pro, Traktor Pro, VirtualDJ, Engine DJ, djay Pro, Mixxx, and Denon Engine OS as mandatory launch targets. Resolume, Yamaha, and grandMA2/grandMA3 are highest priority. Coverage is tracked per tested product/model, platform version, or protocol capability; empty catalog plugins and generic reachability do not qualify.
- Auth0 is configured for human identity. Agent authentication intentionally remains a separate credential scheme.
- Exact Codex context-window percentages are not exposed to the assistant. The current conversation is estimated at 90% context usage as of the DME5/DME3 slice. This README is the durable source of truth for a new task.

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

1. First pilot venue and its recovery workflow.
2. Yamaha product families/models available in the first pilot venue.
3. Initial storage targets: local disk only, local plus NAS, or local plus S3-compatible cloud.

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

Draft PRs #3 through #11 establish Auth0 tenancy, the Agent-side Scan → Backup → Verify → Restore loop, and tenant recovery history. PR #12 adds native Flutter and Auth0; PR #13 adds system inventory; PR #14 adds protocol 1.2 allowlisted TCP discovery; PRs #15–#16 add Resolume recovery; PR #17 adds grandMA2/grandMA3; PRs #18–#20 add Yamaha DM7, RIVAGE PM, CL/QL, TF, and DM3; PR #21 adds distinct Yamaha DME7 `.pvd` and MTX/MRX `.mtx` recovery targets; PR #22 adds PC-D/DI amplifier project compatibility; PR #23 protects editable ProVisionaire Control PLUS `.pvcppj` projects and exported `.pvksk` Kiosk controllers; PR #24 adds DME5/DME3 `.pvd` compatibility and Custom Control Panel companions. Subsequent branches complete the representative platform wave with Q-SYS Designer, ETC Eos, Dante Controller, original Allen & Heath SQ, Crestron SIMPL Windows, Shure Designer 6, and Blackmagic ATEM. The professional-audio manufacturer wave now covers DiGiCo SD/Quantum `.ses`, SSL Live `.show`, Lawo mc² `.lpn`, Calrec Apollo/Artemis `.CalrecShow`, Studer Vista generated title backups, Midas PRO Series/XL8 `.show`, complete Behringer WING show folders, Soundcraft Vi showfolders, and Tascam Model-series MTR song recovery. Avid VENUE is deferred pending a verified export fixture; Roland Pro A/V is next for research and implementation. The full 100-target Version 1 scope is preserved in `docs/INTEGRATION_CATALOG.md`. A live Auth0/API proof still requires a deployed API and native build host; full Xcode is not installed here. macOS LaunchDaemon keychain access remains an installer-validation requirement.
