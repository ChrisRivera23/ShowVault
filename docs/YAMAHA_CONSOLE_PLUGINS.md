# Yamaha console settings export plugins

The Yamaha console recovery track currently supports five separately validated product families:

- `showvault.yamaha-dm7` for DM7 Series `.dm7f` settings exports;
- `showvault.yamaha-rivage` for current `.RIVAGEPM` and legacy `.PM10ALL`, `.PM7ALL`, `.PM10PART`, and `.PM7PART` RIVAGE PM exports.
- `showvault.yamaha-cl-ql` for the shared CL/QL `.CLF` settings format;
- `showvault.yamaha-tf` for TF Series `.TFF` console data, with companion `.TFP` presets preserved when they are in the same export directory.
- `showvault.yamaha-dm3` for DM3 `.DM3F` all-settings exports, with companion `.DM3S` scenes and `.DM3P` presets preserved in the same export directory.

Yamaha documents that a DM7 `.dm7f` file can contain all internal settings. A RIVAGE `.RIVAGEPM` settings file contains scenes, libraries, system setup, patching, mixing, user controls, preferences, and related system data. ShowVault treats these files as opaque vendor artifacts: it records size and SHA-256 integrity but does not alter or claim to semantically validate their contents.

Official references:

- [DM7 saving settings to USB](https://manual.yamaha.com/pa/mixers/dm7/rm/en-US/9429464075.html)
- [DM7 SAVE/LOAD screen](https://manual.yamaha.com/pa/mixers/dm7/rm/en-US/11107091851.html)
- [RIVAGE PM settings-file contents](https://manual.yamaha.com/pa/mixers/RIVAGE_PM_series/en-US/5399171851.html)
- [RIVAGE PM supported load formats](https://manual.yamaha.com/pa/mixers/RIVAGE_PM_series/en-US/10743329291.html)
- [QL settings export (`.CLF`)](https://usa.yamaha.com/files/download/other_assets/5/392925/ql5_en_rm_a0.pdf)
- [CL settings and load warning](https://usa.yamaha.com/files/download/other_assets/8/329238/cl5_en_rm_c0.pdf)
- [TF console data and presets](https://usa.yamaha.com/products/proaudio/mixers/tf/presets.html)
- [DM3 Editor file types](https://manual.yamaha.com/pa/mixers/dm3/rm/en-US/6210342155.html)
- [DM3 USB handling](https://manual.yamaha.com/pa/mixers/dm3/rm/en-US/6203368971.html)

## Agent configuration

Configure exact operator-exported directories under `YamahaDm7ExportRoots`, `YamahaRivageExportRoots`, `YamahaClQlExportRoots`, `YamahaTfExportRoots`, and `YamahaDm3ExportRoots`. Each root must contain its matching complete-settings artifact. A DM3 scene or preset without a `.DM3F` all-settings file is intentionally rejected as an incomplete recovery unit. Child-only scans, cross-family formats, and directories without a recognized settings file are rejected.

The plugin inventories all regular companion files within the validated export directory, without following links, up to the command's 1–100,000 file limit. The existing immutable package, independent hash verification, and controlled test-restore operations provide the recovery path.

## Safe operator workflow

1. Save all console settings to USB using Yamaha's SAVE/LOAD workflow.
2. Wait for the console to finish accessing the device before removal.
3. Copy or mount the export beneath an exact Agent-configured root.
4. Discover, package, and verify the export with ShowVault.
5. Restore to an empty test target and use matching Editor software or non-production hardware for compatibility validation.
6. Before loading settings on production hardware, power down connected equipment and/or reduce output levels. Yamaha explicitly warns that loaded settings may cause the console to emit signals immediately.

## Compatibility boundary

This slice does not yet cover Rivage system logs, Yamaha DSP processors, network switches, amplifiers, or speakers. Those remain mandatory catalog targets but require their own documented file formats, model/version fixtures, and safe recovery workflows. A passing hash verification proves the export was preserved exactly; it does not prove compatibility with another console model, bus configuration, license state, or firmware version. TF firmware/editor combinations in particular must be checked against Yamaha's compatibility information before loading.
