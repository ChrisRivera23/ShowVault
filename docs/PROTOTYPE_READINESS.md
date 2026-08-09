# Prototype readiness

ShowVault's prototype must be installable and testable without tailoring it to a specific venue. Live Nightclub is the intended first venue deployment, but it is not the design target, test environment, or source of hidden assumptions. Until the gates below pass on personal or otherwise controlled equipment, no venue deployment is part of the prototype-readiness workflow.

The acceptance path remains:

**Install → Enroll → Scan → Backup → Verify → Restore → Prove**

Passing only the middle four recovery operations is insufficient. A prototype is ready for venue installation only when a production-like artifact can be installed, restarted, upgraded, operated, and diagnosed without repository access or a developer toolchain.

## Non-negotiable boundaries

- Venue identity, network ranges, product addresses, paths, models, and vendors are runtime inputs or discovered facts, never build-time assumptions.
- Live Nightclub-specific names, credentials, addresses, paths, topology, and equipment do not belong in application defaults, fixtures, packages, or acceptance criteria.
- Personal and controlled equipment is the only authorized validation environment until every required readiness gate passes.
- Synthetic protocol fixtures remain the authority for exact parser and safety boundaries. Personal hardware validation confirms interoperability; it does not loosen those boundaries.
- The first restore for every workflow targets an absent or empty controlled location. Loading state into live production equipment remains a separate supervised operation.
- Catalog expansion is paused while readiness work is active. New integration research resumes only for a concrete readiness blocker or an explicitly reprioritized product need.

## Current evidence

| Area | Current evidence | Readiness status |
|---|---|---|
| Recovery semantics | The API, Agent, and native dashboard complete an authenticated filesystem Scan → Backup → Verify → Restore loop with matching SHA-256 output. | Proven on one controlled development fixture; broader system validation remains. |
| Package safety | Backup creation is immutable and content-addressed; verification checks structure, manifest identity, sizes, and SHA-256 hashes; restore is confined to allowlisted empty targets and revalidates immediately before publication. | Implemented and covered by focused automated tests. |
| Agent identity | Enrollment codes are short-lived and single-use; durable credentials use macOS Keychain or Windows Credential Manager; missing credentials fail closed. | Implemented and tested. |
| macOS Agent service | A self-contained payload can run under a hidden service account and LaunchDaemon with a dedicated Keychain and restart validation. | Production-style packaging exists; signing, notarization, upgrades, and a full clean-machine drill remain. |
| Native operator application | The Flutter macOS application completes the recovery loop when launched from the development workflow. | No documented signed, distributable, clean-machine installation path yet. |
| Control plane | The API and PostgreSQL stack support the authenticated workflow and durable evidence. | The prototype runbook still requires Docker, the .NET SDK, repository access, migrations, and command-line startup. |
| Onboarding | Enrollment and path-free candidate approval contracts exist. | The end-to-end install/enrollment/configuration experience still relies on attended command-line configuration. |
| Platform coverage | Agent credentials and core code include macOS and Windows behavior. | No equivalent production-style Windows service installer or validated minimum-OS installation matrix yet. |
| Operational resilience | Unit and integration tests cover many restart, malformed-input, tamper, and failure boundaries. | A production-like end-to-end failure/restart drill on personal equipment is not yet recorded. |
| Integration breadth | Recovery and exact-identity capabilities cover representative production products, with explicit deferrals where evidence is unsafe. | Sufficient to pause catalog expansion; breadth does not substitute for installability and recovery proof. |

## Required readiness gates

### Gate 1 — Reproducible release artifacts

- Build versioned macOS operator-app and Venue Agent artifacts from a clean checkout.
- Install both on a clean personal Mac without Flutter, .NET, Git, or repository access.
- Document minimum tested macOS versions and Apple silicon/Intel behavior honestly.
- Add signing and notarization before any venue installation; personal-equipment drills may use clearly labeled unsigned development artifacts.
- Define upgrade behavior that preserves Agent identity, state, packages, logs, and configuration.

### Gate 2 — Venue-neutral onboarding and preflight

- An authorized operator can create a venue, obtain one short-lived enrollment code, install the Agent, and confirm its identity without editing source files.
- Configuration accepts only explicit control-plane, storage, discovery, and restore boundaries and contains no venue-specific defaults.
- Preflight reports service state, control-plane connectivity, credential availability, storage capacity/writability, configured boundaries, and required permissions without exposing credentials or private paths to the control plane.
- Failed preflight blocks recovery operations with an actionable local explanation.

### Gate 3 — Controlled personal-equipment recovery

- Run the complete workflow from installed artifacts, not `dotnet run` or `flutter run`.
- Exercise at least one generic filesystem recovery unit and representative application-export workflows available on personal equipment.
- Preserve command/event evidence, package identity, verification digest, restoration digest, and independent source/restored hashes.
- Repeat after an Agent restart and a host reboot with no interactive service-account login.

### Gate 4 — Failure and tamper behavior

- Demonstrate safe failures for an expired/reused enrollment code, unavailable control plane, unavailable Agent, insufficient storage, unreadable source, changed source during backup, corrupt or incomplete package, failed verification, non-empty restore target, and interrupted restore.
- Confirm retry behavior is idempotent and does not duplicate completion evidence or publish a partial restore.
- Confirm logs are useful without containing enrollment codes, durable credentials, private file contents, or unbounded device responses.

### Gate 5 — Upgrade, reinstall, and supportability

- Upgrade and reinstall preserve durable identity and recovery evidence unless the operator explicitly chooses a destructive reset.
- A bounded local diagnostic bundle can be produced for support without including credentials, package contents, or unrestricted filesystem/network inventories.
- Installation, validation, recovery, rollback, and attended removal instructions are versioned with the artifacts.

### Gate 6 — Venue-installation release decision

- Every preceding gate has recorded evidence from personal or controlled equipment.
- Remaining limitations are visible in the product and release notes.
- The exact release artifacts installed at the first venue are the artifacts that passed readiness testing.
- Live Nightclub contributes no special configuration beyond normal runtime venue onboarding.

## Prioritized implementation sequence

1. Produce a reproducible macOS operator-application release artifact and a personal-equipment clean-install procedure that does not require Flutter.
2. Run the existing self-contained macOS Agent package through the same clean-machine procedure and close packaging/validation defects.
3. Replace development-only control-plane startup assumptions with a versioned deployable prototype environment and migration procedure.
4. Connect venue creation, enrollment, configuration, and preflight into one attended onboarding path.
5. Automate and record the personal-equipment success, restart, reboot, failure, and tamper matrix.
6. Add Windows packaging and execute the same gates before claiming Windows venue readiness.
7. Resume catalog work only after these gates are substantially complete or a tested workflow exposes a specific missing integration.

## Immediate bounded task

Build and verify a versioned, unsigned-for-personal-testing macOS release artifact for the Flutter operator application. The artifact must use runtime-provided control-plane configuration, contain no Live Nightclub data or secrets, and launch on a personal Mac without Flutter, the repository, or a development command. Signing and notarization remain mandatory before venue installation and must not be implied by the personal-test artifact.
