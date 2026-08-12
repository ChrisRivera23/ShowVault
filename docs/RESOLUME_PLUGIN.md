# Resolume assisted recovery plugin

`showvault.resolume` is a legacy Venue Agent compatibility plugin for an
operator-prepared Resolume portable folder. It is **Assisted** support, not the
customer-facing **Scan this computer** implementation and not proof of complete
Resolume recovery.

Resolume documents **Collect Media → Copy Composition** as the workflow that
copies referenced media into one folder and places a rewritten composition copy
with it. ShowVault does not drive Resolume, inspect a live output, or alter an
active composition.

Official reference:

- [Resolume Media Manager and Collect Media](https://resolume.com/support/en/6/media-manager)

## Exact local authority

`ResolumeDiscoveryRoots` contains at most 32 unique absolute bundle roots. Each
entry authorizes only that exact directory; it does not authorize its parent,
siblings, or arbitrary descendants. The empty default disables the plugin.

A bundle must contain at least one regular root-level `.avc` composition. The
plugin inventories at most 128 regular files, rejects links, reparse points,
non-regular entries, overlong paths, and unstable identities or topology, and
returns no truncated inventory.

Full paths, relative filenames, timestamps, sizes, and hashes remain in Agent
local storage. Completion events contain only the existing path-free plugin,
count, and truncation fields.

## Packaging boundary

Before a Resolume inventory becomes a package, ShowVault reopens the exact root,
requires the same complete file set and hashes, copies from retained no-follow
file identities, and repeats file, directory, root, topology, and hash checks.
A late, removed, replaced, linked, or modified file fails without publishing a
package.

The resulting package proves byte integrity for the captured files and can be
restored only through the existing controlled empty-target workflow. Empty
directories are not recovery artifacts.

## Limitations

ShowVault does not yet parse `.avc` dependencies, prove that Collect Media
captured every referenced asset, identify the Resolume version, validate
plugins/codecs/fonts/licenses, reopen the composition, verify output behavior,
or produce a complete Recovery Confidence result. Those require versioned
knowledge and representative synthetic or separately authorized fixtures.
