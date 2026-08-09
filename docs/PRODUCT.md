# ShowVault Product Record

## Positioning

ShowVault is an operating system for production infrastructure. Backup is one application inside a broader production-resilience platform. Its job is to let a production professional know—not merely hope—that an environment can be recovered.

Its primary discovery promise is that an operator can start ShowVault at an unknown venue and scan for supported equipment and software represented in the integration catalog without first knowing its address, path, model, or vendor. This includes nightclubs, concert halls, houses of worship, touring systems, and similar production environments.

Prototype readiness is venue-neutral. Live Nightclub is the intended first venue deployment, but it must not shape application defaults, fixtures, packaging, or acceptance criteria. Installation and recovery validation occur on personal or otherwise controlled equipment until the gates in [`PROTOTYPE_READINESS.md`](PROTOTYPE_READINESS.md) pass; no venue is treated as a pilot environment during that work.

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
- Compatibility is designed and tested for older venue Macs and Windows PCs, not only current developer hardware. Every release must publish an honest minimum-OS matrix and avoid claiming support without testing.
- Direct laptop-to-device Ethernet is a supported discovery scenario for equipment that is not connected to a venue network, subject to the same explicit authorization and product-signature requirements as any other active scan.

## Core workflow

- Scan: discover software, devices, assets, relationships, and dependencies.
- Backup: build a portable, versioned package rather than merely copying files.
- Verify: check integrity, dependencies, compatibility, and recoverability.
- Restore: produce a recovery plan and coordinate a secure, auditable recovery.

## MVP navigation

Dashboard, Venues, Devices, Projects, Assets, Discovery, Backups, Verification, Recovery, Digital Twin, Plugins, Reports, Monitoring, Documentation, Administration, and Settings.

Every screen should answer: “What does the user need to do right now to protect or recover their production environment?”

## Version 1 production integrations

The Product Owner-approved launch scope is maintained in [`INTEGRATION_CATALOG.md`](INTEGRATION_CATALOG.md). It covers professional audio manufacturers, audio networking and DSP, lighting platforms and protocols, video/media servers, DJ platforms, and projection. Resolume, Yamaha, and MA Lighting grandMA2/grandMA3 are highest priority.

Product-specific breadth will be defined honestly per tested model, software/firmware version, and recovery workflow rather than claiming that one plugin covers every product made by a manufacturer. Protocol support is recorded as a capability, not mislabeled as a vendor integration.

Implementation proceeds one tested recovery workflow at a time. A roadmap commitment does not justify empty placeholder plugins or claims of compatibility without product-specific discovery, backup, verification, and recovery evidence.
