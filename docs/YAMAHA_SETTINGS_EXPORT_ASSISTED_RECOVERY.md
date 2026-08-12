# Yamaha settings-export Assisted recovery

ShowVault provides two disabled-by-default legacy-Agent discovery profiles for
operator-prepared Yamaha settings exports:

- `showvault.yamaha-dm7-settings-export` recognizes a root-level `.dm7f` file.
- `showvault.yamaha-rivage-settings-export` recognizes root-level `.RIVAGEPM`,
  `.PM10ALL`, `.PM7ALL`, `.PM10PART`, and `.PM7PART` files.

These profiles preserve opaque files from an exact, locally authorized export
directory. They do not search disks or USB volumes, inspect Yamaha Editor data,
connect to a console, or communicate with equipment or network devices.

## Local authorization and capture boundary

Configure dedicated absolute directories in `YamahaDm7SettingsExportRoots` or
`YamahaRivageSettingsExportRoots`. Both lists are empty by default. Each list
accepts no more than 32 unique paths, and the DM7 and RIVAGE lists may not
overlap. A discovery request must name an authorized directory exactly; a
parent or child is not authorized implicitly.

The selected directory must contain at least one recognized artifact directly
at its root. ShowVault captures that exact directory, including companion files
and non-empty descendant directories. Unrelated parents and siblings remain out
of scope.

Capture fails closed when it encounters a linked or reparse ancestor, root, or
descendant; a non-regular entry; empty directory topology that cannot be
represented; more than 4,096 files or 1,024 directories; a relative path over
1,024 characters; a file over 2 GiB; aggregate content over 16 GiB; the lower
file limit requested by the command; or the two-minute discovery deadline.
There is no truncated-success mode.

Package creation has a fifteen-minute deadline. It independently rechecks local
authorization and root-level format recognition, recaptures the tree, requires
the exact discovered file set, and verifies topology, size, hash, and retained
filesystem identity while copying from held handles. A late addition, removal,
rename, replacement, resize, content change, or root swap prevents publication.

## Evidence and compatibility limits

The package manifest records the exact ShowVault profile, Yamaha family, and
recognized file format. The settings files remain opaque to ShowVault.

Yamaha documents that DM7 internal data can be saved to a `.dm7f` file, while
the save flow lets the operator select which data to save. Yamaha also documents
that a `.RIVAGEPM` file can contain all or selected data. Legacy `.PM10ALL` and
`.PM7ALL` are pre-V3.05 all-data formats, while `.PM10PART` and `.PM7PART` are
pre-V3.05 selected-data formats. See Yamaha's
[DM7 save procedure](https://manual.yamaha.com/pa/mixers/dm7/rm/en-US/9429464075.html),
[RIVAGE settings-file contents](https://manual.yamaha.com/pa/mixers/RIVAGE_PM_series/en-US/5399171851.html),
and [RIVAGE load formats and safety notice](https://manual.yamaha.com/pa/mixers/RIVAGE_PM_series/en-US/10743329291.html).

Consequently, a recognized extension and matching hash do not prove:

- which source model, software version, or firmware version created the file;
- that the operator exported all required settings;
- semantic validity or destination compatibility;
- license, plug-in, external-device, network, media, or dependency closure; or
- that loading the file is safe in production.

The operator must record and confirm those facts independently using vendor
documentation and a known compatible environment.

## Attended restore and validation

ShowVault restore remains attended and may write only to a new, empty,
ShowVault-controlled target. It never writes directly into a live console or
Yamaha Editor tree.

After package verification and controlled extraction, an operator must validate
the export with a compatible Yamaha Editor or non-production hardware before
production use. Yamaha warns that loading settings can cause signals to be
output immediately. Before loading, follow Yamaha's procedure to power down
connected equipment and/or lower all outputs. Do not use synthetic tests as
evidence of Windows-native behavior, real USB behavior, Editor compatibility,
console compatibility, firmware compatibility, or production readiness.
