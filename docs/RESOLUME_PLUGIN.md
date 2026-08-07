# Resolume recovery plugin

`showvault.resolume` is the first Version 1 vendor integration. It protects two distinct recovery units:

1. A portable show bundle created with Resolume's **Collect Media → Copy Composition** workflow. Resolume documents that this operation copies referenced media into one folder, places a composition copy beside it, and rewrites that copy to the collected locations.
2. The Resolume Arena or Avenue user Documents tree containing compositions, third-party effects, custom fixtures, preferences, presets (including Advanced Output), recordings, and keyboard/MIDI/OSC/DMX shortcuts.

ShowVault never drives the live Resolume UI or changes an active composition.

Official references:

- [Resolume Media Manager and Collect Media](https://resolume.com/support/en/6/media-manager)
- [Resolume directory list](https://resolume.com/support/en/directory-list)

## Agent configuration

Configure portable-bundle parent directories under `ResolumeDiscoveryRoots` and exact Arena/Avenue Documents directories under `ResolumeUserDataRoots`. Empty lists disable their respective recovery units. A user-data root must contain at least one recognized Resolume directory and only the configured root itself—not an arbitrary child—may be scanned. A `StartDiscovery` command names `showvault.resolume`, the selected absolute root, and a file limit from 1–100,000.

The plugin recursively inventories regular files without following links, records relative paths, sizes, modification times, and SHA-256 hashes, and stores the result through the existing durable command executor. The standard immutable package, verification, and controlled-restore commands then provide the first full Resolume recovery loop.

## Operator workflow

1. Save the active composition in Resolume.
2. Use Media Manager to collect the composition and media into a dedicated bundle folder.
3. Ask ShowVault to discover that bundle with `showvault.resolume`.
4. Create and cryptographically verify the immutable recovery package.
5. Restore only to an empty, locally allowlisted test target.
6. Open the restored composition in a compatible Resolume installation and confirm media and output behavior before production use.

For user data, close Resolume or otherwise ensure it is not writing files, then discover the configured Arena/Avenue Documents directory. Restore user data only to an empty test location first. The operator must review version compatibility and copy selected content into the intended Resolume Documents directory while the application is stopped.

## Current boundary

Application binaries, registration/licensing data, live output control, caches, logs, and unattended restoration into a running show are excluded. ShowVault records file integrity but does not yet parse compositions or XML presets for semantic compatibility; that requires fixtures from real Arena/Avenue versions and remains a later verification level.
