# Yamaha CL/QL and TF settings-export Assisted recovery

ShowVault provides two disabled-by-default legacy-Agent Assisted recovery
profiles for operator-prepared Yamaha settings exports:

- `showvault.yamaha-cl-ql-settings-export` requires a root-level `.CLF` file;
- `showvault.yamaha-tf-settings-export` requires a root-level `.TFF` file.

The profiles preserve opaque files from an exact locally authorized export
directory. They do not search disks, USB volumes, user profiles, Yamaha Editor
data, consoles, equipment, or network devices.

## Local authorization and capture boundary

Configure dedicated absolute directories in `YamahaClQlSettingsExportRoots`
or `YamahaTfSettingsExportRoots`. Both lists are empty by default and accept no
more than 32 roots. Yamaha DM7, RIVAGE PM, CL/QL, and TF configured roots must
all be unique and non-overlapping; parents and descendants cannot represent
separate recovery units.

A discovery request must name an authorized directory exactly. The selected
directory must contain its primary settings artifact directly at the root. A
marker in a descendant does not authorize capture. TF `.TFP` preset and `.TFS`
scene files are preserved as companions when present, but neither authorizes a
TF settings-export root.

A directory containing a primary settings format from another supported
Yamaha family is rejected instead of packaging mixed compatibility targets.

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

Yamaha documents `.CLF` as the CL/QL settings-file format. The console save
flow permits selecting data, and some individually saved data does not include
related assignments. Yamaha also documents editor/firmware combinations that
cannot directly load older CL/QL data. A `.CLF` extension therefore does not
identify CL versus QL, the exact source model/version, or export completeness.

TF Editor documents:

- `.TFF` for all mixer settings;
- `.TFP` for one preset;
- `.TFS` for one scene.

Yamaha separately requires a TF Editor version compatible with the console
firmware. ShowVault does not parse these formats and cannot infer semantic
validity, firmware/editor compatibility, save selection, destination
compatibility, licenses, plug-ins, external-device/network/media closure, or
production safety from an extension and matching hash.

Official references:

- [QL settings save and selection](https://usa.yamaha.com/files/download/other_assets/5/392925/ql5_en_rm_a0.pdf)
- [CL/QL Editor compatibility limits](https://usa.yamaha.com/support/updates/ql_edt_win_400.html)
- [QL load selection and output-signal warning](https://usa.yamaha.com/files/download/other_assets/8/331488/ql5_es_rm_a0.pdf)
- [TF Editor file formats](https://usa.yamaha.com/files/download/other_assets/1/392731/tfeditor_en_ug_v45_i0.pdf)
- [TF firmware/editor compatibility](https://usa.yamaha.com/support/updates/tf_edt_300_win.html)

## Attended restore and validation

Restore remains attended and may write only into a new empty
ShowVault-controlled target. ShowVault never writes directly into a live
console or Yamaha Editor tree.

Before production use, an operator must confirm the exact source family/model,
console/editor and firmware/software versions, selected data and completeness,
and destination compatibility, then validate the verified export with a
compatible Editor or non-production console. Yamaha warns that loading settings
can immediately output signals. Power down connected equipment and/or lower all
outputs before loading.

Synthetic tests do not establish Windows-native/reparse behavior, real USB
behavior, Yamaha Editor or console compatibility, firmware compatibility,
representative export completeness, signal safety, equipment/venue behavior,
or production readiness.
