# Prototype readiness

ShowVault's prototype must be installable and testable without tailoring it to a specific venue. LIV nightclub is the intended first venue deployment, but it is not the design target, test environment, or source of hidden assumptions. Until the gates below pass on personal or otherwise controlled equipment, no venue deployment is part of the prototype-readiness workflow.

The acceptance path is:

**Install → initialize local vault → Scan → Backup → Verify → queue offline → synchronize → Restore → Prove**

Passing only the middle four recovery operations is insufficient. A prototype is ready for venue installation only when a production-like artifact can be installed, restarted, upgraded, operated, and diagnosed without repository access or a developer toolchain.

## Non-negotiable boundaries

- Venue identity, network ranges, product addresses, paths, models, and vendors are runtime inputs or discovered facts, never build-time assumptions.
- LIV nightclub-specific names, credentials, addresses, paths, topology, and equipment do not belong in application defaults, fixtures, packages, or acceptance criteria.
- The beta runs on the Product Owner's current Mac. A computer scan checks only bounded locations declared by the approved integration catalog and must not enumerate or report unrelated applications.
- The customer-facing app exposes no Agent installation, enrollment-code, service, credential-store, or Keychain workflow. The local recovery engine may be packaged as an internal application component rather than a separate customer-installed Agent.
- Backup data is first committed as an immutable recovery point in the configurable local ShowVault Pro vault. Local manifests and queue state are durable by design; secrets and authentication credentials remain separately protected.
- Installed catalog applications such as Resolume or Serato must appear as detected systems when their documented standard locations exist. Detection alone remains distinct from recoverable data, approval, validation, protection, backup, verification, and restore.
- The final product targets both macOS and Windows. Platform readiness is claimed separately and only after equivalent installation and scan/recovery gates pass on that operating system.
- Personal and controlled equipment is the only authorized validation environment until every required readiness gate passes.
- Synthetic protocol fixtures remain the authority for exact parser and safety boundaries. Personal hardware validation confirms interoperability; it does not loosen those boundaries.
- The first restore for every workflow targets an absent or empty controlled location. Loading state into live production equipment remains a separate supervised operation.
- Catalog expansion is paused while readiness work is active. New integration research resumes only for a concrete readiness blocker or an explicitly reprioritized product need.

## Current evidence

| Area | Current evidence | Readiness status |
|---|---|---|
| Recovery semantics | The earlier API/Agent path completed a controlled filesystem recovery loop with matching SHA-256 output. | Useful implementation evidence, but the customer path is being simplified to direct app-to-cloud operation. |
| Package safety | The local package format is immutable and content-addressed; the canonical vault, independent manifests, verified-only queue, append-only journal, resumable chunks, and remote verification now feed an authenticated API. The server independently validates the manifest and catalog identity, derives tenant-scoped S3-compatible keys, conditionally creates immutable chunks, and publishes a receipt only after verification. | The adapter and disposable-emulator proof do not establish production-provider retention, regional durability, or dependency closure. |
| Native operator application | The Flutter macOS application performs exact catalog scanning and an installed synthetic Save → authenticated hosted synchronization → attended Restore drill through native pickers. App termination/resume, exact hosted/restored bytes, authorization denial, and offline retry are recorded. A universal release bundle builds with user-selected read/write access. | Personal-data runtime proof, clean-machine packaging, production-like infrastructure, and Windows installed proof remain. |
| Control plane | A pinned multi-stage API image, PostgreSQL service, one-shot migration job, fail-closed object-storage configuration, and liveness/readiness endpoints support the authenticated workflow. | Provider provisioning, workload identity, external TLS/ingress, monitoring, backup, and deployed-environment evidence remain operator work. |
| Onboarding | The intended customer path is install and scan, then authenticate when the first paid cloud operation is requested. No Agent or enrollment control appears in the app. | Clean-machine execution still requires recorded evidence; the installed personal-Mac direct scan is proven. |
| Platform coverage | Direct exact-location scan definitions include macOS and Windows candidates for the current beta products. | Windows packaging and installed execution remain unproven. |
| Operational resilience | Unit and integration tests cover restart-resume, idempotency, offline backoff, retry exhaustion, local/remote tamper, links, malformed state, privacy-filtered remote manifests, immutable object chunks, concurrent commits, and unavailable storage. A disposable S3-compatible stack proves migration ordering, readiness, and a synthetic write/resume/commit. | Host reboot, selected-provider deployment, broader failure/tamper automation, and personal-data recovery remain unproven. |
| Integration breadth | Recovery and exact-identity capabilities cover representative production products, with explicit deferrals where evidence is unsafe. | Sufficient to pause catalog expansion; breadth does not substitute for installability and recovery proof. |

## Required readiness gates

### Gate 1 — Reproducible release artifacts

- Build a versioned macOS ShowVault app from a clean checkout.
- Install it on a clean personal Mac without Flutter, .NET, Git, repository access, or a separate Agent installer.
- Document minimum tested macOS versions and Apple silicon/Intel behavior honestly.
- Add signing and notarization before any venue installation; personal-equipment drills may use clearly labeled unsigned development artifacts.
- Define upgrade behavior that preserves the local vault and durable queue without exposing credentials.

### Gate 2 — Venue-neutral onboarding and preflight

- An operator installs ShowVault and sees **Scan this computer** without login, Agent installation, or enrollment controls. Commercial builds require a ShowVault account before the first cloud operation.
- The scan checks only exact allowlisted catalog candidates and submits only opaque candidate keys. Paths and file contents remain in memory and never enter control-plane requests or logs.
- Preflight reports control-plane connectivity and only permissions required for the selected attended operation.
- Failed preflight blocks recovery operations with an actionable local explanation.

### Gate 3 — Controlled personal-equipment recovery

- Run the complete workflow from the installed app, not `dotnet run` or `flutter run`.
- Exercise at least one generic filesystem recovery unit and representative application-export workflows available on personal equipment.
- Create and verify an immutable local recovery point, then synchronize it through the authenticated hosted API while preserving independent local and cloud status.
- Repeat after app restart and host reboot. The controlled no-login beta remains loopback-only; commercial sessions and reauthentication behavior require separate evidence.

### Gate 4 — Failure and tamper behavior

- Demonstrate safe failures for unavailable control plane, expired operator session, insufficient cloud storage, unreadable source, changed source during upload, corrupt or incomplete cloud object, failed verification, non-empty restore target, and interrupted restore.
- Confirm retry behavior is idempotent and does not duplicate completion evidence or publish a partial restore.
- Confirm logs are useful without containing enrollment codes, durable credentials, private file contents, or unbounded device responses.

### Gate 5 — Upgrade, reinstall, and supportability

- Upgrade and reinstall preserve cloud recovery evidence without requiring endpoint identity or scan-state migration.
- A bounded local diagnostic bundle can be produced for support without including credentials, package contents, or unrestricted filesystem/network inventories.
- Installation, validation, recovery, rollback, and attended removal instructions are versioned with the artifacts.

### Gate 6 — Venue-installation release decision

- Every preceding gate has recorded evidence from personal or controlled equipment.
- Remaining limitations are visible in the product and release notes.
- The exact release artifacts installed at the first venue are the artifacts that passed readiness testing.
- LIV nightclub contributes no special configuration beyond normal runtime venue onboarding.

## Prioritized implementation sequence

1. Produce a reproducible macOS operator-application release artifact and a personal-equipment clean-install procedure that does not require Flutter. A release packaging script now creates a venue-neutral personal-test app/ZIP and checksum; clean-machine execution remains to be recorded.
2. Prove the direct, path-free installed-app scan on the Product Owner's Mac without Agent enrollment, Keychain access, or local persistence.
3. Run an installed synthetic Save → authenticated hosted synchronization → attended Restore drill, including restart/resume and denial evidence.
4. Replace development-only control-plane and filesystem-storage assumptions with a versioned deployable prototype environment, production object-storage adapter, and migration procedure. — Adapter, deployment definition, and runbook complete; provider deployment and any real legacy migration remain unproven.
5. Automate and record the personal-equipment success, restart, reboot, failure, and tamper matrix.
6. Add Windows packaging and execute the same gates before claiming Windows venue readiness.
7. Resume catalog work only after these gates are substantially complete or a tested workflow exposes a specific missing integration.

## Immediate bounded task

Automate the controlled personal-equipment success, restart/reboot, failure, and tamper matrix around the installed customer app and deployable API. Preserve synthetic isolation and record each gate without using personal application data, venue equipment, or production-provider durability claims.
