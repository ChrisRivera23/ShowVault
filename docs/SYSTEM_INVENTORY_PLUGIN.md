# System inventory plugin

`showvault.system-inventory` is a signed, first-party, read-only Venue Agent plugin. It records the minimum host facts needed to understand a recovery target without running shell commands or collecting file contents.

## Command

The control plane issues `CollectSystemInventory` with an empty JSON payload. This additive command advances the Agent protocol to version 1.1. The Agent stores the complete result in its durable local SQLite result table before emitting `JobCompleted`; retries remain idempotent because the command ID is reused as the outcome event ID.

## Collected data

- machine name;
- operating-system description;
- operating-system and process architectures;
- logical processor count; and
- up to 64 mounted volume records containing name, drive type, total bytes, and available bytes when readable.

Unreadable or unready volumes remain visible with null capacity values. The plugin performs no writes, subprocess execution, network probing, registry traversal, or file-content reads. Its manifest requests only `ReadSystemInformation`, not `ReadFiles`.

## Boundary

This slice establishes local system inventory only. Network-device discovery will use an explicit allowlist and bounded probe behavior in a separate slice. Vendor-specific product inventory and recovery still require Product Owner selection of the first integration and pilot workflow.
