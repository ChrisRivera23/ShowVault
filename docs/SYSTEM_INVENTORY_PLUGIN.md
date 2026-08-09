# System inventory plugin

`showvault.system-inventory` is a signed, first-party, read-only Venue Agent plugin. It records the minimum host facts needed to understand a recovery target without running shell commands or collecting file contents.

## Command

The control plane issues `CollectSystemInventory` with an empty JSON payload. This additive command advances the Agent protocol to version 1.1. The Agent stores the complete result in its durable local SQLite result table before emitting `JobCompleted`; retries remain idempotent because the command ID is reused as the outcome event ID.

Protocol 1.21 adds the narrower `CollectCatalogApplications` command behind the native **Scan this computer** manager action. It runs only the catalog location provider and does not collect system volumes, machine identity, network interfaces, or subnet proposals. Exact detected paths remain in Agent-local SQLite. The completion event contains only the catalog plugin ID, a bounded candidate count, and each candidate's opaque ID, plugin ID, product name, candidate type, and evidence.

## Collected data

- machine name;
- operating-system description;
- operating-system and process architectures;
- logical processor count; and
- up to 64 mounted volume records containing name, drive type, total bytes, and available bytes when readable;
- up to 128 existing catalog-defined local application or recovery-data candidates; and
- bounded local subnet proposals derived without contacting hosts.

Unreadable or unready volumes remain visible with null capacity values. Application discovery checks only declared standard paths, ignores missing or inaccessible locations, and does not read candidate file contents. The catalog currently covers Resolume Arena, Resolume Avenue, Serato DJ Pro, rekordbox, and Traktor Pro. Each product has its own plugin identity. The authorized personal-Mac fixture confirms the current nested Resolume application layout (`/Applications/Resolume Arena/Arena.app`); the registry uses that exact bounded candidate rather than enumerating unrelated applications. Versioned application and user-data directories are prefix-scoped, sorted, and capped at 32 per catalog location. The plugin performs no writes, network probing, shortcut resolution, or registry traversal.

## Boundary

Candidate paths remain in Agent-local SQLite. Only opaque IDs and bounded product, candidate-type, and evidence fields leave the Agent. Every candidate requires an operator decision before an exact local scope can exist, and installed-application detection is distinct from recoverable data, validation, backup, verification, and protection. Serato and rekordbox currently have detection only; external/removable libraries and protection workflows remain unsupported. Current Windows rekordbox 6/7 installed-app detection is also unsupported because official primary documentation does not publish a stable executable path.
