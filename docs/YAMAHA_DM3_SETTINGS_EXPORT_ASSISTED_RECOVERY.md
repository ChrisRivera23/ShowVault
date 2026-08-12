# Yamaha DM3 settings-export Assisted recovery

ShowVault provides a disabled-by-default legacy-Agent Assisted recovery profile
for operator-prepared Yamaha DM3 settings exports:

- `showvault.yamaha-dm3-settings-export` requires a root-level `.DM3F` file.

The profile preserves opaque files from an exact locally authorized export
directory. It does not search disks, USB volumes, user profiles, DM3 Editor
data, consoles, equipment, or network devices.

## Local authorization and capture boundary

Configure dedicated absolute directories in `YamahaDm3SettingsExportRoots`.
The list is empty by default and accepts no more than 32 roots. Yamaha DM7,
RIVAGE PM, CL/QL, TF, and DM3 configured roots must all be unique and
non-overlapping; parents and descendants cannot represent separate recovery
units.

A discovery request must name an authorized directory exactly. The selected
directory must contain a `.DM3F` settings artifact directly at the root. A
marker in a descendant does not authorize capture. `.DM3S` scene and `.DM3P`
preset files are preserved as companions when present, but neither authorizes
a DM3 settings-export root.

A directory containing a primary settings format from another supported
Yamaha family is rejected instead of packaging mixed compatibility targets.
Although a DM3 console can convert certain TF settings during load, that vendor
interoperability does not make a mixed DM3/TF directory one recovery unit.

Capture walks the exact directory without following links or reparse points.
It retains root, directory, and regular-file identities and handles, and fails
closed for linked ancestors/roots/descendants, non-regular entries, empty
topology, more than 4,096 files or 1,024 directories, relative paths over 1,024
characters, files over 2 GiB, total content over 16 GiB, a lower command file
limit, cancellation, or the two-minute discovery deadline. There is no
truncated-success mode.

Package creation has a fifteen-minute deadline. It rechecks local
authorization and family structure, recaptures the exact tree, requires the
same topology, sizes, hashes, and retained identities, copies through retained
handles, and revalidates before immutable publication or package reuse.

## Format and compatibility limits

DM3 Editor documents:

- `.DM3F` for all mixer settings;
- `.DM3S` for one scene;
- `.DM3P` for one preset.

ShowVault does not parse these formats. A recognized extension and matching
hash do not establish semantic validity, DM3 versus DM3 Standard source,
console/editor or firmware/software version, export provenance or
completeness, destination compatibility, TF-conversion behavior, licenses,
external-device/network/media closure, or production safety.

Official references:

- [DM3 Editor file types](https://manual.yamaha.com/pa/mixers/dm3/rm/en-US/6210342155.html)
- [DM3 library and scene management](https://manual.yamaha.com/pa/mixers/dm3/rm/en-US/6350697099.html)
- [DM3 USB handling](https://manual.yamaha.com/pa/mixers/dm3/rm/en-US/6203368971.html)
- [DM3 SAVE/LOAD behavior](https://manual.yamaha.com/pa/mixers/dm3/rm/en-US/6296256011.html)
- [DM3 firmware and Editor compatibility](https://uk.yamaha.com/en/support/updates/dm3-editor-win.html)

## Attended restore and validation

Restore remains attended and may write only into a new empty
ShowVault-controlled target. ShowVault never writes directly into a live
console or DM3 Editor tree.

Before production use, an operator must confirm the exact source model,
console/editor and firmware/software versions, export provenance and
completeness, and destination compatibility, then validate the verified export
with a compatible DM3 Editor or non-production console. Power down connected
equipment and/or lower all outputs before loading settings.

Synthetic tests do not establish Windows-native/reparse behavior, real USB
behavior, DM3 Editor or console compatibility, firmware compatibility,
representative export completeness, signal safety, equipment/venue behavior,
or production readiness.
