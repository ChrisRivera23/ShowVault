# System inventory plugin

`showvault.system-inventory` is a signed, first-party, read-only Venue Agent plugin. It records the minimum host facts needed to understand a recovery target without running shell commands or collecting file contents.

## Command

The control plane issues `CollectSystemInventory` with an empty JSON payload. This additive command advances the Agent protocol to version 1.1. The Agent stores the complete result in its durable local SQLite result table before emitting `JobCompleted`; retries remain idempotent because the command ID is reused as the outcome event ID.

Protocol 1.21 added the narrower Agent `CollectCatalogApplications` compatibility command. The current installed-app path is simpler: **Scan this computer** checks exact catalog candidates directly in the app and submits only opaque allowlisted candidate keys to the manager-authorized `/computer-scans` endpoint. The API maps those keys to bounded product, type, and evidence metadata. Exact paths exist only transiently in app memory; no Agent, enrollment code, system inventory, local scan database, or file-content read is involved.

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

On the current installed-app path, candidate paths are not persisted. Only opaque allowlisted keys leave the app, and the control plane stores bounded product, candidate-type, and evidence fields. Installed-application detection remains distinct from recoverable data, validation, backup, verification, and protection. The legacy Agent path continues to retain its own paths locally for compatibility, but it is not the intended customer onboarding flow. Serato and rekordbox currently have detection only; external/removable libraries and protection workflows remain unsupported. Current Windows rekordbox 6/7 installed-app detection is also unsupported because official primary documentation does not publish a stable executable path.
