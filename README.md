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
- Roland M-5000/M-5000C recovery requires at least one native `.m5pj` console project within an exact operator-declared root and preserves revisions and topology/restore documentation while rejecting audio recordings and `M-5000.PRG` firmware packages; matching console firmware and M-5000 RCS versions remain restore prerequisites.
- PreSonus StudioLive Series III recovery requires a Universal Control full `.BAK` file within an exact operator-declared root and preserves revisions and restore documentation while rejecting individual scenes and Capture recordings; only saved projects, scenes, and presets are included, so operators must store pending changes before backup.
- Biamp Tesira recovery requires at least one editable `.tmf` configuration within an exact operator-declared root and preserves revisions and restore notes while keeping Canvas `.bcv` projects separate; Tesira software/firmware, equipment-table topology, and device serial assignments remain supervised restore prerequisites.
- Symetrix Composer recovery requires at least one `.symx` site file within an exact operator-declared root and preserves revisions, generated reports, control exports, and restore notes; matching Composer/firmware versions, device models, site identity, and network-audio state remain supervised restore prerequisites, while legacy SymNet Designer stays separate.
- Bose Professional ControlSpace recovery requires at least one native `.csp` Designer project within an exact operator-declared root and preserves `.cpf` control panels, `.cpz` remote packages, retrieved `.cab` archives, revisions, and restore notes; matching Designer/firmware versions, hardware models, IP assignments, and Dante state remain supervised restore prerequisites.
- Peavey MediaMatrix NWare recovery requires at least one `.npa` Project Archive within an exact operator-declared root and preserves `.npp` plug-ins, Kiosk personality files, media, revisions, and restore notes; matching NWare/firmware versions, processor and I/O models, installed cards, node roles, and CobraNet/Dante/AES67 topology remain supervised restore prerequisites.
- Ashly Protea NE recovery requires a `.cpj` canvas project within an exact operator-declared root and preserves linked `.pre` all-preset files, `.pne` single presets, FIR coefficients, images, revisions, and restore notes; exact device models, firmware, installed options, and network configuration remain supervised restore prerequisites, while legacy RS-232 Protea formats stay separate.
- Powersoft ArmoníaPlus 2.9 recovery requires a current `.paw4` project within an exact operator-declared root and preserves legacy `.paw3` revisions, `.pam2` speaker data, reports, and restore notes; exact ArmoníaPlus and bundled firmware versions, amplifier models, routing, sources/zones, and AES67 flows remain supervised restore prerequisites.
- Crown recovery requires a HiQnet Audio Architect `.audioarchitect` venue file within an exact operator-declared root and preserves parameter exports, event logs, revisions, and restore notes; the exact Audio Architect version, Crown models and firmware, HiQnet identities, device matching, and network-audio routing remain supervised restore prerequisites.
- Lab Gruppen Lake recovery requires a `.csc` system configuration within an exact operator-declared root and preserves Contour/Mesa module and base files, revisions, reports, and restore notes; exact Lake Controller and firmware versions, compatible PLM/D Series Lake/LM frames, module and group assignments, I/O routing, and Dante state remain supervised restore prerequisites.
- Dynacord SONICUE recovery requires a `.snc` project within an exact operator-declared root and preserves speaker databases, reports, revisions, and restore documentation; exact SONICUE and device-firmware versions, supported amplifier/DSP models, routing, loudspeaker protection, control-panel/Task Engine state, and Dante configuration remain supervised restore prerequisites.
- Electro-Voice IRIS-Net recovery requires an outer `.ds` ZIP project archive containing a non-empty top-level `main.ds` within an exact operator-declared root and preserves revisions, device inventories, reports, diagrams, commissioning records, and restore documentation; exact IRIS-Net/device-firmware versions, hardware topology, addressing, protection settings, and network-audio state remain supervised restore prerequisites.
- d&b audiotechnik recovery requires a non-empty shared R1/ArrayCalc `.dbpr` project within an exact operator-declared root and preserves legacy `.r1p`/`.dbac2` revisions, `.rcs`/`.rss` settings, graphics, logs, system-check evidence, inventories, and restore notes; exact R1/ArrayCalc and firmware versions, device topology, remote identities, routing, protection, and deliberate online synchronization remain supervised restore prerequisites.
- L-Acoustics recovery requires a non-empty current Soundvision `.xmlp` project within an exact operator-declared root and preserves `.xmls` venue models, revisions, reports, LA Network Manager Session and preset/layout backups, XML logs, M1 evidence, and restore notes; software/firmware compatibility, unit topology, presets, routing, calibration, and deliberate live loading remain supervised restore prerequisites.
- Meyer Sound recovery requires a non-empty top-level MAPP 3D `.mapp` project within an exact operator-declared root and preserves the `MAPP Backup` autosave folder, project revisions, imported DXF/SKP venue drawings, exported reports, and restore notes; matching MAPP 3D and loudspeaker-data versions, accurate venue geometry, Galileo GALAXY processor state, and deliberate live synchronization remain supervised restore prerequisites.
- NEXO recovery requires a non-empty top-level NS-1 `.nexo` or `.nexo3` project within an exact operator-declared root and preserves project revisions, imported venue material, exported loudspeaker lists and reports, and restore notes; matching NS-1 version and speaker database, venue geometry, system design, amplifier/controller configuration, and deliberate deployment remain supervised restore prerequisites.
- JBL Professional recovery requires a non-empty top-level Venue Synthesis `.vysn` project within an exact operator-declared root and preserves revisions, Line Array Calculator `.lac3` designs, ArrayLink `.al` deployment files, venue drawings, EASE exports, reports, and restore notes; matching Venue Synthesis/Performance software, supported JBL system groups, firmware, HControl identities, array/DSP parameters, and deliberate online synchronization remain supervised restore prerequisites.
- Martin Audio recovery requires a non-empty top-level Vu-Net `.vun` project within an exact operator-declared root and preserves revisions, snapshots, presets, zone/device documentation, reports, and restore notes; matching Vu-Net and device-firmware versions, device models and static identities, zones, DSP state, and deliberate online matching remain supervised restore prerequisites, while U-Hub `.vup`, DX controller `.prj`, and DISPLAY projects remain separate targets.
- DAS Audio recovery requires at least one signature-validated ALMA `.prj` project beneath the exact operator-declared ALMA data root and preserves configuration, backups, health reports, snapshots, DASaim material, and every other recovery companion in that root; ALMA/firmware compatibility, supported ARA/Integral or ALMA-485 hardware, device identity, grouping, processing, and deliberate online synchronization remain supervised restore prerequisites.
- Authenticated operators can list active venue Agents and issue typed discovery, backup, verification, and restore stages through workflow-specific API endpoints; manager authorization, Agent tenancy, payload bounds, prior-stage types, and backup/verification pairing are validated before commands are queued.
- The Flutter dashboard can select an active venue Agent, collect an exact allowlisted plugin/root and restore target, issue Scan → Backup → Verify → Restore through the typed API, require explicit restore confirmation, and automatically refresh active recovery history.
- A live macOS prototype proof now authenticates through Auth0, loads a real local organization and venue, enrolls a native Venue Agent with Keychain-backed credentials, and completes Scan → Backup → Verify → Restore against a controlled filesystem fixture. The restored file's SHA-256 hash matches the source and the native dashboard reports `Recovery loop proven`.
- A self-contained macOS Venue Agent package now provisions a hidden service account, dedicated restart-safe Keychain, one-shot non-persistent enrollment, root-owned LaunchDaemon, and restart/credential validation workflow.
- System inventory now derives read-only recovery candidates from standard host locations without pre-entered paths; the first provider detects Resolume Arena/Avenue applications and user-data roots on macOS and Windows and requires operator approval before protection.
- Path-free recovery-candidate metadata and manager decisions are now persisted per Venue Agent in the control plane, and the native dashboard provides approve/reject onboarding without exposing local filesystem paths.
- Approved or rejected opaque candidate IDs are delivered through protocol 1.3 to the originating Agent, resolved only against Agent-local inventory, and applied idempotently to durable exact local scopes; unknown IDs fail without granting access.
- Protocol 1.4 can validate an approved Resolume user-data candidate through the real product plugin using only its opaque ID; the Agent resolves the path locally, enforces the exact durable scope, stores the discovery result, and emits only path-free findings. The native dashboard exposes this action without manual path entry.
- Candidate validation outcomes are correlated by command ID and persisted as pending, passed, or failed with bounded path-free evidence; onboarding auto-refreshes, displays file counts and errors, clears stale evidence on a new decision, and permits a path-free backup only after the latest validation passes.
- Venue Agent system inventory now enumerates local interfaces read-only and proposes at most eight unique private IPv4 subnets without contacting hosts; only active physical Ethernet/Wi-Fi interfaces qualify, broad connected networks are narrowed to /24, /31-/32 and non-contiguous masks are rejected, and every proposal remains pending explicit approval.
- Protocol 1.5 persists subnet proposals within the originating Agent and venue tenancy, shows bounded evidence in native onboarding, and delivers manager approve/reject decisions back to the Agent; approval records local scope but deliberately does not authorize or start discovery.
- Protocol 1.6 adds a separate manager authorization for reachability-only discovery of one approved Agent-local subnet; each run probes at most 32 usable addresses by ICMP with 500 ms timeouts and concurrency eight, emits only attempted/responding counts, and never opens ports, collects banners, publishes host addresses, or claims product support.
- Responding IPv4 addresses from each bounded run are retained only in Agent SQLite, keyed by the exact discovery authorization command and proposal; stored path-free results and control-plane events continue to contain counts only, and rejecting the proposal removes the retained set.
- Protocol 1.7 adds separately authorized grandMA3 identification against only the responders retained by one exact bounded discovery command. The Agent performs a bounded HTTP check of the officially documented Web Remote service on port 8080 and requires a `grandMA3` application response signature; matches and addresses stay in Agent SQLite, while completion evidence publishes only attempted/matched counts and the `grandMA3` family. It does not authenticate, enumerate sessions, collect arbitrary banners, identify grandMA2, or treat reachability/open ports as support.
- Tenant-scoped grandMA3 identification state is correlated to the exact Agent, proposal, discovery command, and identification command and persisted as pending/completed/failed with attempted/matched counts and bounded product-family evidence. Native onboarding polls pending work, exposes a separate `Identify grandMA3` action only after responders exist, and displays path-free review evidence while stating that addresses remain local. Re-approval or rediscovery clears stale identification.
- Protocol 1.8 adds separately authorized Yamaha DME7 identification against only one exact Agent-local responder set. The Agent connects to Yamaha's documented remote-control TCP port 49280, sends only LF-terminated `devinfo productname` and `devinfo manufacturer` queries, and requires exact `DME7` plus `Yamaha Corporation` responses. Addresses and matches stay in Agent SQLite, while the completion publishes only attempted/matched counts and `Yamaha DME7`; a connection, open port, partial response, or other Yamaha model is not treated as support.
- Yamaha DME7 identification now has independent tenant-scoped pending/completed/failed state, command correlation, counts, bounded evidence, errors, and timestamps. Native onboarding offers and polls a separate Yamaha action and displays path-free results without overwriting grandMA3 evidence; a new subnet decision or discovery clears both product results as stale.
- Protocol 1.9 adds independently authorized grandMA2 identification against only one exact Agent-local responder set. The Agent connects to MA Lighting's documented Telnet Remote command-line port 30000, sends zero bytes, reads at most 4,096 bytes with a 100-500 ms timeout, and requires the official guest/login-prompt greeting before returning `grandMA2`. Host matches remain in Agent SQLite; the control plane receives only counts and bounded family evidence. Open ports, generic banners, partial greetings, grandMA3 behavior, and reachability do not qualify.
- grandMA2 has independent exact command correlation and pending/completed/failed state in the API and native onboarding. Telnet Remote must already be enabled, so disabled consoles safely remain unidentified; representative hardware/onPC fixture validation is still required.
- L-Acoustics automatic network identification is deliberately deferred after official-evidence review. Public primary material documents capable discovery/control tools but no safe read-only wire signature, and the Electronics HTTP API contract requires identity submission and separate terms acceptance before download. No probe or protocol change was added; generic HTTP, open ports, Milan/AVDECC or Dante metadata, and reachability remain insufficient evidence.
- The permanent product goal explicitly requires catalog-backed discovery at unknown venues, compatibility testing for older macOS and Windows computers, and direct laptop-to-device Ethernet. Protocol 1.10 permits one IPv4 link-local proposal only when exactly one active physical Ethernet interface qualifies; Wi-Fi, virtual/routed interfaces, and ambiguous multiple-Ethernet cases remain excluded. Protocol 1.11 presents the full `169.254.0.0/16` for explicit approval, reads the bounded OS ARP cache, prioritizes complete entries from that exact interface, and still actively contacts at most 32 targets. Passive entries are candidates only and must pass ICMP before product identification.
- Protocol 1.12 carries only selected passive-cache and fallback-target counts with the exact path-free discovery outcome. Both must be non-negative and sum to attempted targets. PostgreSQL and native onboarding display the diagnostic beside attempted/responding counts; addresses, MAC data, interface identity, and cache output remain Agent-local.

Current development branch:

- `codex/link-local-discovery-diagnostics` — path-free passive-versus-fallback diagnostics persisted and displayed through the exact approved discovery workflow.

The Auth0 Native application and live local macOS login/API proof are complete. A reproducible local prototype runbook is available in `docs/PROTOTYPE_RUNBOOK.md`. No pilot venue is selected; onboarding and discovery must work at an unknown venue without requiring operators to pre-enter computer specifications, application paths, device addresses, or vendor inventory. Membership administration, user-requested command cancellation, digital signatures, NAS/cloud storage, and persistent control-plane package records have not been implemented yet.

## Approved product direction

The long-term vision remains a production-infrastructure operating platform, but development will focus first on proving one complete recovery loop:

The primary discovery promise is that an operator can start ShowVault at an unknown nightclub, concert hall, house of worship, or similar venue and find supported equipment and software represented in the integration catalog without already knowing its path, address, model, or vendor. This must be practical on a documented range of older venue Macs and Windows PCs and when a laptop is connected directly to one device by Ethernet.

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
13. Implement and pilot the approved Version 1 vendor integrations. — Priority and representative foundations implemented; remaining professional-audio manufacturer wave now also covers Biamp Tesira
14. Add cloud upload and mobile monitoring.
15. Pilot repeatedly with one real venue.

## Conversation handoff

This section is maintained so a new Codex task can resume without relying on the previous chat transcript.

### Product Owner work preferences

- Continue autonomously with the next documented implementation step when asked to continue.
- Proactively take control of the workstation and install required reputable tooling when it is safe, authorized, and necessary to continue project progress; stop only for credentials, legally binding terms, security-sensitive authorization, or other actions that require explicit user participation or confirmation.
- Always use the user's logged-in Google Chrome session for browser work; do not use Safari or another browser unless the Product Owner explicitly changes this preference.
- Research current official vendor documentation before implementing each integration.
- Do not ask for permission merely to search public or official sources; research autonomously. Stop only for credentials, identity/account submission, legally binding terms, payment, or security-sensitive authorization.
- Keep the current product goal in README.md and every new-chat handoff, with repository decisions, contracts, tests, and implementation as the authority.
- Preserve and test compatibility with older venue Macs and Windows PCs; define and publish honest minimum OS versions instead of assuming current developer hardware.
- Include direct Cat5/Cat6 laptop-to-device discovery in the test strategy, including grandMA2 and Yamaha DM7 examples, without weakening authorization, scope, or primary-signature requirements.
- Implement two or three tasks together only when they are independently safe, clearly bounded, and can be fully verified; otherwise complete one focused task.
- Use exact locally approved roots, reject incomplete/lookalike data, preserve recovery companions, and keep incompatible product families separate.
- Run focused tests followed by the complete relevant regression suite.
- Create a new `codex/` branch for each implementation slice and commit the feature and README handoff separately.
- Keep responses short and direct to conserve context.
- End every user-facing reply with an estimated conversation-context percentage. Codex does not expose an exact measurement, so label it as unmeasured.
- Warn when the estimate approaches 90%. Before recommending a new task, update this README with completed work, branches, commits, tests, research decisions, deferred items, the exact next step, and these work preferences so it can be copied into the next chat.

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
- Tascam branch: `codex/tascam-model-mtr-songs`, based on the Soundcraft branch. It protects one exact song directory directly under the Model-series `MTR` folder, requires recorded `.wav` track data, and preserves every internal file because Tascam warns that renaming or deleting individual files can make a song unloadable. `MUSIC` mixdowns are rejected; Model 12/16/24/2400 hardware/channel layout, firmware, and sample format remain supervised restore boundaries. Sonicview remains separate.
- Roland branch: `codex/roland-m5000-projects`, based on the Tascam branch. It adds exact-root recovery for M-5000/M-5000C `.m5pj` projects, preserving revisions and system-topology/restore documentation. Roland documents projects as the complete restorable console data unit, covering mixer settings, scene memories, fader banks, user assignments, preferences, libraries, network/remotes, and system settings. Firmware `.PRG` packages and recordings are rejected; console firmware and M-5000 RCS versions must match.
- PreSonus branch: `codex/presonus-studiolive-series-iii-backups`, based on the Roland branch. It adds exact-root recovery for Universal Control full `.BAK` files containing all saved StudioLive Series III projects, scenes, and presets, plus revisions and restore documentation. Individual local scenes and Capture recordings do not qualify. Unsaved mixer changes are not included, and target StudioLive model, firmware, and Universal Control version remain supervised restore prerequisites.
- Biamp branch: `codex/biamp-tesira-configurations`, based on the PreSonus branch. It adds exact-root recovery for editable Tesira `.tmf` configurations and preserves revisions and restore documentation. Canvas `.bcv` projects do not qualify. Restore remains supervised because Tesira configuration files retain equipment-table topology and device serial assignments; improper Save As duplication can affect another configured system. Match Tesira software and firmware, verify device allocation, compile, and send deliberately.
- Symetrix branch: `codex/symetrix-composer-sites`, based on the Biamp branch. It adds exact-root recovery for Symetrix Composer `.symx` site files and preserves revisions, reports, control exports, and restore documentation. Legacy SymNet Designer artifacts and standalone SymVue `.svlx` control screens do not qualify. Official Symetrix guidance requires Composer/firmware alignment and warns that site files saved in newer Composer versions may not remain readable by older versions; device models, site identity, network-audio routing, and supervised push/pull behavior remain restore prerequisites.
- Bose branch: `codex/bose-controlspace-projects`, based on the Symetrix branch. It adds exact-root recovery for native ControlSpace Designer `.csp` projects and preserves `.cpf` control panels, `.cpz` packaged remote projects, retrieved `.cab` archives, revisions, and restore documentation. Companion files alone do not qualify. Bose documentation states that `.csp` contains configuration, settings, and control functions for the system and recommends using the commissioned Designer/firmware version; hardware models and configurations, project-network/IP assignments, Dante state, and deliberate online send/save-to-flash behavior remain supervised restore boundaries.
- Peavey branch: `codex/peavey-nware-projects`, based on the Bose branch. It adds exact-root recovery for NWare `.npa` Project Archives and preserves `.npp` project plug-ins, Kiosk personality XML, media, revisions, and restore documentation. Companion files alone do not qualify. Peavey's NWare guide identifies `.npa` as the editable archive and warns of cross-version device compatibility issues; matching NWare and firmware, processor/I/O models, installed cards, node roles, and CobraNet/Dante/AES67 routing remain supervised restore boundaries.
- Ashly branch: `codex/ashly-protea-ne-projects`, based on the Peavey branch. It adds exact-root recovery for Protea NE `.cpj` canvas projects and preserves linked `.pre` all-preset files, `.pne` individual presets, FIR coefficients, images, revisions, and restore documentation. Preset files and legacy RS-232 artifacts alone do not qualify. Ashly's official file-type guide defines `.cpj` as the canvas containing devices with saved and linked presets; exact product models, firmware, installed options, network settings, and safe supervised preset recall remain restore boundaries.
- Crest Audio is deferred until an official PCX Editor or NexSys fixture/document establishes a dependable native configuration-file signature; supported Crest products currently span unrelated PCX Editor, NexSys/NWare, and non-configurable amplifier workflows.
- Powersoft branch: `codex/powersoft-armoniaplus-projects`, based on the Ashly branch. It requires the current ArmoníaPlus 2.9 `.paw4` project anchor and preserves `.paw3` revisions, `.pam2` speaker data, reports, and restore documentation. Powersoft changed `.paw3` to `.paw4` and `.pam` to `.pam2` in 2.9; older artifacts alone do not qualify. Exact application/firmware versions, amplifier models, sources/zones, routing, Views, and project-contained AES67 flows remain supervised restore boundaries.
- Crown branch: `codex/crown-audio-architect-venues`, based on the Powersoft branch. It requires a Crown-containing HiQnet Audio Architect `.audioarchitect` venue and preserves parameter JSON exports, `.sdf` event logs, revisions, and restore documentation. Companion files alone do not qualify. HARMAN documents that newer-saved venues cannot open in older Audio Architect versions and that version changes can require firmware/configuration updates; exact software version, Crown models and firmware, HiQnet identities, device matching, routing, and deliberate online synchronization remain supervised restore boundaries.
- Lab Gruppen branch: `codex/lab-gruppen-lake-systems`, based on the Crown branch. It requires the Lake Controller `.csc` whole-system configuration and preserves Contour `.csm`/`.cbm` and Mesa `.msm`/`.mbm` module/base files, revisions, reports, and restore documentation. Module files alone do not qualify. Official Lake documentation defines `.csc` as storing all module information plus frame and group assignments; exact Controller/firmware versions, compatible frame models, I/O and failover routing, groups, module assignments, loudspeaker protection, and Dante state remain supervised restore boundaries.
- Dynacord branch: `codex/dynacord-sonicue-projects`, based on the Lab Gruppen branch. It requires a SONICUE `.snc` project and preserves colocated speaker databases, reports, revisions, and restore documentation. Speaker databases and legacy IRIS-Net/MARC artifacts alone do not qualify. Dynacord's official SONICUE 1.6 package supplies `.snc` demo projects, while official product and firmware documentation establishes version-specific support across L, C, IX, IPX, TGX, RCM-28, and MXE devices. Exact SONICUE/device-firmware versions, hardware models, routing, loudspeaker protection, control-panel/Task Engine state, and Dante configuration remain supervised restore boundaries.
- Electro-Voice branch: `codex/electro-voice-iris-net-projects`, based on the completed prototype proof. It requires a ZIP-structured IRIS-Net `.ds` project archive containing a non-empty top-level `main.ds` and preserves the entire exact operator-approved root. Plain lookalikes, incomplete archives, loose inner project files, and unapproved child roots do not qualify. IRIS-Net and device-firmware versions, hardware topology, addressing, loudspeaker protection, supervision, and network-audio state remain supervised restore boundaries; QuickSmart Mobile and PREVIEW are not credited by this integration.
- d&b branch: `codex/db-audiotechnik-r1-projects`, based on the Electro-Voice branch. It requires a non-empty shared R1/ArrayCalc `.dbpr` project and preserves the entire exact operator-approved root, including legacy `.r1p`/`.dbac2` projects, `.rcs`/`.rss` settings, and commissioning companions. Empty lookalikes, legacy companions alone, and unapproved child roots do not qualify. Exact R1/ArrayCalc and device-firmware versions, hardware and remote-network topology, remote identities, routing, protection, System check/ArrayVerification, Soundscape, and deliberate online synchronization remain supervised restore boundaries.
- L-Acoustics branch: `codex/l-acoustics-network-manager-projects`, based on the d&b branch. It requires a non-empty current Soundvision `.xmlp` project and preserves the exact operator-approved root, including `.xmls` venues and LA Network Manager recovery companions. Venue/session companions alone and unapproved child roots do not qualify. Software/firmware compatibility, unit topology, presets, routing, calibration, and deliberate loading to live units remain supervised restore boundaries.
- Meyer Sound branch: `codex/meyer-sound-projects`, based on the L-Acoustics branch. It requires a non-empty top-level MAPP 3D `.mapp` project and preserves the exact operator-approved root, including the documented `MAPP Backup` autosave folder, versioned projects, imported DXF/SKP venue drawings, exports, and restore notes. Empty projects, backup-only or drawing-only lookalikes, and unapproved child roots do not qualify. Exact MAPP 3D and loudspeaker-data versions, venue geometry, Galileo GALAXY processor state, and deliberate live synchronization remain supervised restore boundaries.
- NEXO branch: `codex/nexo-ns1-projects`, based on the Meyer Sound branch. It requires a non-empty top-level NS-1 `.nexo` or `.nexo3` project and preserves the exact operator-approved root, including revisions, imported venue models and drawings, exported loudspeaker lists and reports, and restore notes. Empty projects, venue-only or nested-only lookalikes, and unapproved child roots do not qualify. Exact NS-1 and speaker-database versions, venue geometry, system design, amplifier/controller configuration, and deliberate deployment remain supervised restore boundaries.
- RCF research branch: `codex/rcf-recovery-research`, based on the NEXO branch. RCF is deliberately deferred because current official RDNet material documents cloud project and measurement save/recall without publishing a dependable local project extension or signature, while RDShape advertises a native export without defining its format. Implement only after official documentation or a verified real fixture establishes an exact recovery anchor; do not infer support from generic files, cloud state, or `lib.zip` device-library exports.
- JBL branch: `codex/jbl-performance-manager-venues`, based on the RCF research branch. It requires a non-empty top-level JBL Venue Synthesis `.vysn` project and preserves the exact operator-approved root, including revisions, `.lac3` Line Array Calculator designs, `.al` ArrayLink deployment files, venue models, EASE exports, reports, and restore notes. Empty projects, LAC-only or ArrayLink-only lookalikes, and unapproved child roots do not qualify. Exact Venue Synthesis/Performance versions, supported system groups, device firmware, HControl identities, array and DSP parameters, and deliberate online synchronization remain supervised restore boundaries.
- Martin Audio branch: `codex/martin-audio-vunet-projects`, based on the JBL branch. It requires a non-empty top-level Vu-Net `.vun` project and preserves the exact operator-approved root, including revisions, snapshots, presets, zone/device documentation, reports, and restore notes. Empty projects, legacy U-Hub `.vup` or DX controller `.prj` lookalikes, and unapproved child roots do not qualify. Exact Vu-Net and device-firmware versions, supported device models, static addressing and matching, zones, DSP state, and deliberate online synchronization remain supervised restore boundaries; DISPLAY remains separate.
- Funktion-One research branch: `codex/funktion-one-recovery-research`, based on the Martin Audio branch. Funktion-One is deliberately deferred because its official F-Series documentation links NST Audio D-Net for amplifier DSP control, but neither vendor publishes a dependable local project extension or signature. Implement only after primary documentation or a verified real fixture establishes an exact recovery anchor; do not infer support from generic project files, presets, installer artifacts, or network reachability.
- Adamson research branch: `codex/adamson-recovery-research`, based on the Funktion-One research branch. Adamson is deliberately deferred because the official Blueprint AV handbook documents `.rm` as a room-only export containing floors, objects, stages, and reference axes rather than the complete design/control project, while current ArrayIntelligence material does not publish a dependable complete-project extension or signature. Implement only after primary documentation or a verified real fixture establishes a complete recovery anchor; do not credit room geometry alone as system recovery.
- Outline research branch: `codex/outline-recovery-research`, based on the Adamson research branch. Outline is deliberately deferred because the official Newton manual documents saving and opening Outline Dashboard projects, but does not publish a dependable project extension, signature, or storage boundary; it also warns that associating a device reads live parameters into the project and documents a supervised workaround for applying offline project settings. Current OpenArray material likewise does not establish a complete portable recovery anchor. Implement only after primary documentation or a verified real fixture identifies an exact complete-project boundary; do not infer support from generic files, loudspeaker-library data, or network reachability.
- DAS Audio branch: `codex/das-audio-alma-projects`, based on the Outline research branch. It protects the exact ALMA data root, requires a JSON `.prj` anchor under `prj` with ALMA project identity fields, and preserves colocated configuration, backups, `.almahc` health reports, snapshots, DASaim data, and other recovery companions. Loose and malformed `.prj` lookalikes and unapproved child roots do not qualify. Exact ALMA and device-firmware versions, compatible ARA/Integral or ALMA-485 product families, device identities, arrays, zones, processing, and deliberate online synchronization remain supervised restore boundaries.
- macOS packaging branch: `codex/macos-launchdaemon-packaging`, based on the DAS Audio branch. It adds self-contained Apple-silicon/Intel package staging, a hidden `_showvault` account, exact filesystem ownership, a dedicated Keychain that is reopened after locking, one-shot enrollment without persisting the code, a root-owned LaunchDaemon, and a restart/Keychain validator. Privileged installation and reboot-without-login validation remain outstanding on a suitable test Mac.
- Candidate onboarding branch: `codex/candidate-approval-onboarding`, based on `codex/zero-config-candidate-discovery`. It gives each local result an opaque candidate ID, sends only bounded product/type/evidence metadata to the control plane, persists tenant-scoped pending/approved/rejected decisions, and surfaces manager approve/reject controls in the native dashboard. Local paths remain only in Agent SQLite discovery results. Approval is intentionally not yet an authorization to read files; durable Agent-local approved scopes and approved-subnet network discovery remain outstanding.
- Agent-local scope branch: `codex/agent-local-candidate-scopes`, based on candidate onboarding. It advances the typed protocol to 1.3, queues path-free candidate decisions for the originating Agent, durably stores detected candidate mappings and approved exact scopes in local SQLite, removes scopes on rejection, and rejects IDs absent from local inventory. The control plane and completion events never receive the resolved path.
- Approved-candidate validation branch: `codex/approved-candidate-validation`, based on Agent-local scopes. It advances the protocol to 1.4, adds a manager-authorized validation endpoint and native Validate action for approved `UserDataRoot` candidates, resolves the opaque ID locally, lets the Resolume plugin trust only an exact durable approved scope, stores its real hashed discovery result in Agent SQLite, and emits candidate ID, plugin ID, file count, and truncation status without a path. Installed-application candidates are not treated as recovery roots.
- Candidate validation-results branch: `codex/candidate-validation-results`, based on approved-candidate validation. It persists pending/passed/failed validation state, file count, truncation, bounded failure messages, and the unique validation command ID on the tenant candidate. Agent outcomes update only their matching pending candidate. Native onboarding polls while validation is pending, shows results, and queues `CreateBackup` against the successful Agent-local discovery result without accepting or transmitting a path. Re-deciding a candidate clears stale validation evidence.
- Safe subnet-proposals branch: `codex/safe-subnet-proposals`, based on candidate validation results. It adds read-only interface enumeration to system inventory and emits at most eight unique private IPv4 /24-/30 proposals with bounded network-safe evidence. Loopback, link-local, tunnel, VPN/virtual, point-to-point, inactive, public, malformed-mask, network-address, and broadcast-address candidates are excluded. No host is contacted and proposals grant no scan authority.
- Subnet proposal-approval branch: `codex/subnet-proposal-approval`, based on safe subnet proposals. Protocol 1.5 gives proposals opaque IDs, persists them in Agent SQLite and tenant-scoped PostgreSQL, exposes manager-only approve/reject controls in native onboarding, and records approved CIDRs only on the originating Agent. API input independently validates aligned private IPv4 /24-/30 networks. Approval does not queue or authorize discovery.
- Bounded approved-subnet discovery branch: `codex/bounded-approved-subnet-discovery`, based on subnet proposal approval. Protocol 1.6 requires a second manager action, queues only an opaque proposal ID plus hard bounds, resolves the CIDR from Agent-local approval, probes no more than 32 usable hosts via ICMP at 100-500 ms with concurrency eight, and correlates count-only outcomes to the exact authorization. Responding addresses are retained only in Agent SQLite under that authorization; no addresses are published, and no ports, banners, routed networks, product claims, or synchronization are included.
- Avid VENUE is deliberately deferred until a verified real S6L export fixture establishes a dependable show anchor and directory boundary. Current official documentation describes category/folder backup and transfer behavior but does not provide a sufficiently reliable standalone show-file extension or signature. Password files, Waves licenses, and other credentials must never be collected as recovery content.
- The local prototype milestone is complete and catalog implementation has resumed with signature-validated Electro-Voice IRIS-Net project discovery. The complete catalog remains core Version 1 launch scope.
- Prototype workflow API branch: `codex/prototype-workflow-api`, based on the Dynacord branch. It adds authenticated active-Agent listing plus typed discovery, backup, verification, and restore endpoints. Manager authorization, venue/Agent tenancy, payload limits, dependency command types, and exact backup-to-verification pairing are enforced before durable Agent commands are issued.
- Current prototype branch: `codex/prototype-flutter-workflow`, based on the workflow API branch. It adds native Agent selection and sequential Scan → Backup → Verify → Restore controls, explicit restore confirmation, command failure feedback, manual refresh, and three-second refresh while a recovery run is active.
- Next recommended implementation step: build an end-to-end direct-link fixture harness using injected macOS and Windows interface/ARP samples, bounded ICMP outcomes, Agent queue execution, and API path-free correlation. Cover empty and populated caches and prove that addresses never enter emitted events. Then validate grandMA2 against representative hardware/onPC when available. Resume the catalog with KV2 Audio after zero-configuration onboarding is proven.
- Auth0 Native application `ShowVault Flutter` is registered and its callback/logout URLs and public Client ID are configured. The development API now permits user-delegated access from tenant applications; Auth0 saved and reload-verified the `All apps allowed` policy after the dashboard Admin session was refreshed. Machine-to-machine access remains restricted to per-application authorization.
- Local prototype prerequisites are installed: Docker Desktop 4.85.0, PostgreSQL 18 in Compose, Xcode 26.6, and CocoaPods 1.17.0. The PostgreSQL schema is migrated, the local API is healthy on loopback HTTP, and the macOS Flutter client builds, authenticates, loads live tenant data, and has completed a controlled recovery workflow through a Keychain-backed native Agent.
- The Product Owner supplied the authoritative Version 1 venue-technology scope in `docs/INTEGRATION_CATALOG.md`, including rekordbox, Serato DJ Pro, Traktor Pro, VirtualDJ, Engine DJ, djay Pro, Mixxx, and Denon Engine OS as mandatory launch targets. Resolume, Yamaha, and grandMA2/grandMA3 are highest priority. Coverage is tracked per tested product/model, platform version, or protocol capability; empty catalog plugins and generic reachability do not qualify.
- Auth0 is configured for human identity. Agent authentication intentionally remains a separate credential scheme.
- Exact Codex context-window percentages are not exposed to the assistant; repository state and this handoff are authoritative instead of chat history.

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
9. Treat the completed-work list, branch history, expected starting state, and handoff snapshot as the authoritative memory of prior conversations; do not redo completed work unless verification shows it is incomplete or incorrect.
10. Work in one bounded vertical slice: state the intended outcome, inspect existing implementation and tests, preserve unrelated changes, implement, run proportional verification, commit the feature, then commit the README handoff separately when the milestone materially changes project state.
11. In the final response, always report the outcome, important files or commits, verification results, remaining limitations, and the next recommended task.
12. End every final response with an approximate conversation-context usage percentage and say whether a new conversation is recommended. Exact context-window usage is not exposed, so label the number as an estimate and base it on the amount of accumulated discussion and tool output.

### Copy/paste continuation prompt

```text
Open and read /Users/infamous/Documents/ChatGPT/showvault/README.md and docs/AUTOMATIC_DISCOVERY.md in full. Keep this goal authoritative in every handoff: start ShowVault at an unknown nightclub, concert hall, house of worship, or similar venue and discover supported catalog equipment/software without pre-entered paths, addresses, models, or vendors, including on documented older Macs/Windows PCs and direct laptop-to-device Ethernet links. Inspect Git and the committed handoff, then build one end-to-end direct-link fixture harness using injected macOS and Windows interface/ARP samples, bounded ICMP outcomes, Agent queue execution, and API path-free correlation. Cover empty and populated caches and prove that addresses never enter emitted events. Preserve separate approval, strict host/time limits, Agent-local addresses, and current physical-interface exclusions. Do not ask permission merely to search public/official sources; stop only for credentials, identity/account submission, binding terms, payment, or security-sensitive authorization. Commit the tested feature, update and separately commit the handoff, and keep responses short and direct. End with completed work, verification, commits, limitations, exact next task, untouched files, estimated context usage, and a new-chat recommendation.
```

Expected starting state:

- Repository: `/Users/infamous/Documents/ChatGPT/showvault`
- Branch: `codex/link-local-discovery-diagnostics`
- Clean worktree after the compatibility-goal and workflow handoff commits, except the pre-existing untracked `NEXT_CONVERSATION.md`
- Latest feature commit: `ea797d4 feat: report path-free discovery target diagnostics`
- Verified baseline: 2 contract tests, 21 platform tests, 278 Agent tests, 7 API tests, Flutter analysis, and 14 Flutter tests passing; EF Core reports no pending model changes
- Next operational target: end-to-end macOS/Windows direct-link fixture harness
- Next catalog target after onboarding: KV2 Audio

## Handoff snapshot

The Product Owner approved the focused venue-resilience direction, the control-plane/Venue-Agent separation, Flutter clients, ASP.NET Core modular monolith, PostgreSQL metadata storage, SQLite local state, S3-compatible content storage, evidence-based verification, and a narrow one-venue/one-plugin recovery MVP.

Draft PRs #3 through #11 establish Auth0 tenancy, the Agent-side Scan → Backup → Verify → Restore loop, and tenant recovery history. Later stacked branches add approved vendor recovery integrations through DAS Audio ALMA; RCF, Funktion-One, Adamson, and Outline are explicitly deferred pending documented or fixture-verified complete recovery formats. No pilot venue is selected. The repository is authoritative. The standing goal is to start ShowVault at an unknown venue and discover supported catalog equipment/software without pre-entered paths, addresses, models, or vendor inventory, including on documented older Macs/Windows PCs and direct laptop-to-device Ethernet. Protocols 1.3-1.6 provide path-free recovery-candidate approval/validation/backup, tenant-scoped subnet approval, bounded reachability authorization, and Agent-local responder retention. Protocols 1.7-1.9 independently identify grandMA3, Yamaha DME7, and grandMA2 with documented evidence. Protocol 1.10 accepts one unambiguous physical link-local Ethernet proposal. Protocol 1.11 presents `169.254.0.0/16` for explicit approval and prioritizes bounded OS ARP neighbors within at most 32 active ICMP targets. Protocol 1.12 persists and displays only passive-versus-fallback target counts, requiring them to sum to attempted targets; no addresses, MAC data, interface identity, or cache output leave the Agent. Empty cache still cannot guarantee discovery. The verified baseline is 2 contract tests, 21 platform tests, 278 Agent tests, 7 API tests, Flutter analysis, and 14 Flutter tests passing, with no pending EF Core model changes. Next is an end-to-end macOS/Windows direct-link fixture harness. The catalog resumes with KV2 Audio after zero-configuration onboarding is proven.
