# Resolume portable bundle plugin

`showvault.resolume` is the first Version 1 vendor integration. Its initial recovery unit is a portable show bundle created with Resolume's **Collect Media → Copy Composition** workflow. Resolume documents that this operation copies referenced media into one folder, places a composition copy beside it, and rewrites that copy to the collected locations. ShowVault never drives the live Resolume UI or changes an active composition.

Official references:

- [Resolume Media Manager and Collect Media](https://resolume.com/support/en/6/media-manager)
- [Resolume directory list](https://resolume.com/support/en/directory-list)

## Agent configuration

Configure one or more absolute parent directories under `ResolumeDiscoveryRoots`. An empty list disables the plugin. A `StartDiscovery` command names `showvault.resolume`, an absolute bundle directory beneath an allowed root, and a file limit from 1–100,000.

The plugin recursively inventories regular files without following links, records relative paths, sizes, modification times, and SHA-256 hashes, and stores the result through the existing durable command executor. The standard immutable package, verification, and controlled-restore commands then provide the first full Resolume recovery loop.

## Operator workflow

1. Save the active composition in Resolume.
2. Use Media Manager to collect the composition and media into a dedicated bundle folder.
3. Ask ShowVault to discover that bundle with `showvault.resolume`.
4. Create and cryptographically verify the immutable recovery package.
5. Restore only to an empty, locally allowlisted test target.
6. Open the restored composition in a compatible Resolume installation and confirm media and output behavior before production use.

## Current boundary

This slice protects the portable composition and its collected media. Resolume's broader user Documents tree also contains presets, fixtures, preferences, and shortcuts; those become a second product-specific recovery unit after the portable-bundle pilot proves the basic workflow. Application binaries, licensing data, live output control, and unattended restoration into a running show are excluded.
