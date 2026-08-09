# System inventory plugin

`showvault.system-inventory` is a signed, first-party, read-only Venue Agent plugin. It records the minimum host facts needed to understand a recovery target without running shell commands or collecting file contents.

## Command

The control plane issues `CollectSystemInventory` with an empty JSON payload. This additive command advances the Agent protocol to version 1.1. The Agent stores the complete result in its durable local SQLite result table before emitting `JobCompleted`; retries remain idempotent because the command ID is reused as the outcome event ID.

## Collected data

- machine name;
- operating-system description;
- operating-system and process architectures;
- logical processor count; and
- up to 64 mounted volume records containing name, drive type, total bytes, and available bytes when readable;
- up to 128 existing catalog-defined local application or recovery-data candidates; and
- bounded local subnet proposals derived without contacting hosts.

Unreadable or unready volumes remain visible with null capacity values. Application discovery checks only declared standard paths, ignores missing or inaccessible locations, and does not read candidate file contents. The catalog currently covers Resolume Arena, Resolume Avenue, and Serato DJ Pro on macOS and Windows. It gives each product its own plugin identity, so detection never silently credits an unrelated integration. The plugin performs no writes, network probing, or registry traversal. Its manifest requests `ReadSystemInformation` and `ReadFiles`; the latter covers bounded candidate existence checks rather than authorization to protect candidate contents.

## Boundary

Candidate paths remain in Agent-local SQLite. Only opaque IDs and bounded product, candidate-type, and evidence fields leave the Agent. Every candidate requires an operator decision before an exact local scope can exist, and installed-application detection is distinct from recoverable data, validation, backup, verification, and protection. Serato currently has detection only; its external-drive libraries and protection workflow remain unsupported.
