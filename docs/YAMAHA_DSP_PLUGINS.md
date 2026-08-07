# Yamaha DSP project plugins

ShowVault currently protects two Yamaha installed-sound DSP project families as separate recovery targets:

- `showvault.yamaha-dme7` for DME7 systems designed in ProVisionaire Design (`.pvd`);
- `showvault.yamaha-mtx-mrx` for MTX/MRX systems designed in MTX-MRX Editor (`.mtx`).

Yamaha identifies `.pvd` as the ProVisionaire Design project format containing all ProVisionaire Design settings, and documents ProVisionaire Design as the configuration application for DME7. Yamaha's MTX-MRX Editor guide shows `.mtx` project files and describes the editor as the setup and management application for MTX and MRX processors. ShowVault preserves these opaque vendor files and every regular companion file within the selected recovery root; it does not parse, modify, or claim to validate their internal settings.

Official references:

- [ProVisionaire Design file types](https://manual.yamaha.com/pa/pv/pvd/en/YJ-H0/01_AboutPV_en.html)
- [DME7 reference manual](https://usa.yamaha.com/files/download/other_assets/4/1624254/DME7_reference_manual_En_C0.pdf)
- [MTX-MRX Editor user guide](https://usa.yamaha.com/files/download/other_assets/5/446335/mtx-mrx_editor_en_ug_m0.pdf)
- [MTX-MRX Editor overview](https://usa.yamaha.com/products/proaudio/software/mtx_editor/index.html)

## Agent configuration

Configure exact operator-managed directories under `YamahaDme7ProjectRoots` and `YamahaMtxMrxProjectRoots`. A DME7 root must contain at least one `.pvd` project; an MTX/MRX root must contain at least one `.mtx` project. Child-only scans, arbitrary folders, and the other Yamaha family's project format are rejected.

The plugin recursively inventories regular files without following links, subject to the command's 1–100,000 file limit. The existing immutable package, independent SHA-256 verification, and controlled restore flow provide the protection path.

## Safe operator workflow

1. Synchronize the system using the matching Yamaha editor and save the current project locally.
2. Include any external media, documentation, controller files, and restore notes needed by the venue in the same exact configured directory.
3. Discover, package, and verify that directory with ShowVault.
4. Restore into an empty test target and open the project with a compatible editor version before any production-device synchronization.
5. Validate device models, Unit IDs, firmware, Dante configuration, licenses, and signal-flow prerequisites before applying the project to hardware.

## Compatibility boundary

Cryptographic verification proves that ShowVault preserved the files exactly; it does not prove that the project is compatible with different hardware, firmware, licenses, device topology, or I/O inventory. `.pvd` projects may include supported products beyond DME7, so this first compatibility target is credited specifically to a DME7 recovery workflow and requires real DME7 fixtures before Version 1 production-readiness sign-off. MTX/MRX remains separate because its `.mtx` format and editor lifecycle differ.
