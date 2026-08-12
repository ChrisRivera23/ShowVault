# Resolume assisted recovery plugin

ShowVault has two distinct legacy Venue Agent compatibility profiles:

- `showvault.resolume` captures an operator-prepared portable folder; and
- `showvault.resolume-user-data` version `1.0.0` captures a bounded selection
  of documented user-data categories.

Both profiles are **Assisted** support, not the customer-facing **Scan this
computer** implementation and not proof of complete Resolume recovery.

Resolume documents **Collect Media → Copy Composition** as the workflow that
copies referenced media into one folder and places a rewritten composition copy
with it. ShowVault does not drive Resolume, inspect a live output, or alter an
active composition.

Official reference:

- [Resolume Media Manager and Collect Media](https://resolume.com/support/en/6/media-manager)
- [Resolume Directory List](https://resolume.com/support/en/directory-list)

## Exact local authority

`ResolumeDiscoveryRoots` contains at most 32 unique absolute bundle roots. Each
entry authorizes only that exact directory; it does not authorize its parent,
siblings, or arbitrary descendants. The empty default disables the plugin.

`ResolumeUserDataRoots` independently contains at most 32 unique absolute
user-data roots and is also disabled by default. A root cannot appear in both
lists. The user-data profile recognizes these exact English, case-sensitive
top-level directory names only:

- `Compositions`
- `Fixture Library`
- `Preferences`
- `Presets`
- `Shortcuts`

Unknown siblings are not opened, inventoried, packaged, logged, or included in
outcomes. `Extra Effects` and `Recorded` are intentionally excluded pending
separate licensing, compatibility, and large-media policies. Resolume notes
that directory names can be translated on non-English operating systems; an
unrecognized or differently cased name therefore fails safely rather than
claiming localized coverage.

A bundle must contain at least one regular root-level `.avc` composition. The
plugin inventories at most 128 regular files, rejects links, reparse points,
non-regular entries, overlong paths, and unstable identities or topology, and
returns no truncated inventory.

The user-data profile accepts only regular files within the selected categories
and rejects empty selected content. It is bounded to 2,048 files, 256
directories, 1,024-character relative paths, 16 MiB per file, 128 MiB total,
30 seconds for discovery, and two minutes for package creation. Cancellation
fails without returning a partial inventory or publishing a partial package.

Full paths, relative filenames, timestamps, sizes, and hashes remain in Agent
local storage. Completion events contain only the existing path-free plugin,
count, and truncation fields.

## Packaging boundary

Before either Resolume inventory becomes a package, ShowVault reopens the exact
root under the same profile, requires the same complete selected file set and
hashes, copies from retained no-follow file identities, and repeats file,
directory, root, selected-topology, and hash checks. A late, removed, replaced,
linked, or modified selected file fails without publishing a package. Changes
to unknown user-data siblings remain outside authority and do not enter the
package.

The resulting package proves byte integrity for the captured files and can be
restored only through the existing controlled empty-target workflow. Empty
directories are not recovery artifacts.

## Limitations

ShowVault does not yet parse `.avc` dependencies, prove that Collect Media
captured every referenced asset, identify the Resolume or operating-system
version, validate plugins/codecs/fonts/licenses, preserve undocumented state,
reopen the application or composition, verify output behavior, or produce a
complete Recovery Confidence result. User-data restore remains limited to the
existing attended empty-test-target workflow; ShowVault does not write into a
live Resolume Documents tree or claim that operator placement is safe. Those
claims require versioned knowledge and representative synthetic or separately
authorized fixtures.
