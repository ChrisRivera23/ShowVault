# MA Lighting Assisted show-export recovery

## Support boundary

ShowVault provides distinct legacy-Agent Assisted profiles for operator-created
grandMA2 and grandMA3 show exports:

- `showvault.malighting-grandma2-show-export` version `1.0.0`;
- `showvault.malighting-grandma3-show-export` version `1.0.0`.

Both profiles are disabled by default. They scan only an exact absolute export
directory explicitly selected in local Agent configuration. They do not search
disks, user profiles, application installations, live consoles, onPC trees,
network appliances, or FTP endpoints.

This is archive, manifest, and checksum verification. It is not evidence that a
show will load or run correctly on a particular grandMA software or hardware
version. Show files can have forward-only compatibility constraints, so an
operator must confirm the source version and validate the recovered export with
an equal or newer compatible vendor version before production use.

## Exact supported roots

`GrandMa2ShowExportRoots` accepts only exact leaf directories shaped as either:

```text
gma2/shows
gma2/<major.minor-or-patch>/shows
```

For a versioned root, the version directory is recorded as product-version
evidence in the recovery manifest. An unversioned root is retained honestly
without inferred product-version evidence.

`GrandMa3ShowExportRoots` accepts only an exact leaf directory shaped as:

```text
grandMA3/shared/shows
grandMA3/shared/backups
```

The root path does not encode a grandMA3 software version, so the manifest
records that operator confirmation is required. `gma3_library` is excluded.
Certificates, users, plugins, media, netkeys, licenses, credentials, logs,
crash data, screenshots, temporary files, and every sibling outside the exact
authorized leaf remain outside this profile.

Example disabled-by-default configuration:

```json
{
  "Agent": {
    "GrandMa2ShowExportRoots": [],
    "GrandMa3ShowExportRoots": []
  }
}
```

An operator may add at most 32 unique absolute roots per profile. The same root
cannot be configured for both products.

## Filesystem and resource safety

Capture walks the absolute path component-by-component without following links
or reparse points. It retains the identities of the exact root, every opened
directory, and every regular file. It rejects linked roots, linked ancestors,
linked descendants, devices, other non-regular entries, empty directory
topology, and any identity, topology, length, or content change.

Discovery and packaging independently capture the exact authorized leaf.
Packaging rechecks local authorization, requires an exact file-set/size/hash
match, copies from retained file handles, and revalidates the tree before
publishing the immutable package. Added, removed, renamed, replaced, resized,
or rehashed content fails closed; no successful truncated inventory or partial
package is emitted.

Per export, the profile bounds are:

- 4,096 regular files;
- 1,024 directories;
- 1,024 characters per relative path;
- 2 GiB per file;
- 16 GiB total;
- two minutes for discovery; and
- fifteen minutes for packaging.

Cancellation and every safety/bound failure return no partial result. Remote
completion and failure outcomes contain only bounded categories/counts; exact
paths and filenames remain in protected local recovery records.

## Restore procedure and limitations

Restore is permitted only through ShowVault's existing attended verified-package
flow into a new empty controlled target. ShowVault never writes this profile
directly into a live console or onPC tree. After verification, an operator uses
the applicable vendor import or removable-media workflow to place the export.

The profile does not include VPU/media dependencies, software, firmware,
drivers, licenses, secrets, certificates, plugins, user accounts, networking,
or a dependency-closure claim. Those require separately defined and authorized
profiles. Representative vendor-version load/reopen tests, Windows-native
evidence, real removable media, and equipment/venue validation are also
separate gates.

Primary vendor references:

- [grandMA3 folder structure](https://help.malighting.com/grandMA3/2.2/HTML/fm_folder_structure.html)
- [grandMA3 show-file management](https://help.malighting.com/grandMA3/2.2/HTML/show_file_management.html)
- [grandMA2 Backup menu](https://help2.malighting.com/grandMA2/en/help/key_backup_menu.html)
- [grandMA2 FTP and folder structure](https://help2.malighting.com/grandMA2/en/help/key_network_ftp.html)
