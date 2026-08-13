# System inventory plugin

`showvault.system-inventory` is bounded legacy Venue Agent compatibility infrastructure. It is not part of customer onboarding or the installed application's **Scan this computer** flow. The customer scan uses exact catalog candidates and opaque allowlisted keys without installing or enrolling an Agent.

## Command

The repaired foundation already uses Agent protocol 1.1 for bounded legacy
system inventory. The milestone-1 manifest made protocol 1.21 catalog-command
reconstruction conditional. It was not required on this base and was therefore
not imported. Customer desktop Scan remains completely independent of this
legacy Agent command.

## Collected data

- machine name;
- operating-system description;
- operating-system and process architectures;
- logical processor count; and
- up to 64 mounted volume records containing name, drive type, total bytes, and available bytes when readable.

Unreadable or unready volumes remain visible with null capacity values. Host strings, architecture strings, processor count, volume names/types, capacities, and the 64-volume maximum are validated against closed limits before storage. Machine and volume identifiers are sensitive local metadata: they must not enter completion events, API requests, logs, diagnostics, UI, or evidence. The outbound completion event contains only plugin ID, bounded OS description, OS architecture, logical processor count, and volume count.

The plugin performs no writes, subprocess execution, network probing, registry traversal, credential access, or file-content reads. Its manifest requests only `ReadSystemInformation`, not `ReadFiles`. Tests use a synthetic inventory source and never enumerate the developer or CI host.

## Boundary

This slice establishes local-only compatibility inventory. It does not authorize network discovery, subnet proposals, catalog expansion, equipment access, or venue testing. Direct desktop detection remains distinct from backup, verification, protection, and recoverability.
