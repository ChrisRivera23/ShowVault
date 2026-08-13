# ShowVault Product Record

## Positioning

ShowVault is a recovery-first, local-first platform for production
infrastructure. The customer promise begins with a simple desktop path:
**Install → Scan this computer → Sign in for cloud service**.

## Users and markets

Primary users are AV technicians, technical directors, production managers, and production departments serving entertainment venues, nightclubs, touring productions, broadcast, universities, and houses of worship. Long-term markets include hospitality, theme parks, cruise ships, esports, enterprise, and government.

## Product principles

- Offline-first operation with safe synchronization.
- Metadata is separate from binary content.
- Stable object UUIDs and versioned records.
- Immutable discovery and topology snapshots.
- Commands express intent; events express completed facts.
- Plugins use contracts and never write directly to the database.
- Recovery never bypasses platform security.
- Simple by default; advanced controls appear when needed.
- Exact local paths and machine identity remain local unless a later,
  explicitly bounded feature requires otherwise.
- A direct desktop detection is never presented as approved, protected,
  verified, backed up, or recoverable.

## Core workflow

- Scan: discover software, devices, assets, relationships, and dependencies.
- Backup: build a portable, versioned package rather than merely copying files.
- Verify: check integrity, dependencies, compatibility, and recoverability.
- Restore: produce a recovery plan and coordinate a secure, auditable recovery.

## Current milestone

Milestone 1 provides a signed-out/offline direct desktop scan of exact approved
catalog candidates. It performs no unrestricted application enumeration,
content reads, network inspection, Agent installation, or Agent enrollment.
When signed in, only opaque candidate keys can be submitted to an allowlisted,
manager-authorized organization/venue endpoint.

## MVP navigation

Dashboard, Venues, Devices, Projects, Assets, Discovery, Backups, Verification, Recovery, Digital Twin, Plugins, Reports, Monitoring, Documentation, Administration, and Settings.

Every screen should answer: “What does the user need to do right now to protect or recover their production environment?”
