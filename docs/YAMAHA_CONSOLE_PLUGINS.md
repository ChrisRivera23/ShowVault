# Yamaha console settings export plugins

The first Yamaha recovery slice supports two separately validated product families:

- `showvault.yamaha-dm7` for DM7 Series `.dm7f` settings exports;
- `showvault.yamaha-rivage` for current `.RIVAGEPM` and legacy `.PM10ALL`, `.PM7ALL`, `.PM10PART`, and `.PM7PART` RIVAGE PM exports.

Yamaha documents that a DM7 `.dm7f` file can contain all internal settings. A RIVAGE `.RIVAGEPM` settings file contains scenes, libraries, system setup, patching, mixing, user controls, preferences, and related system data. ShowVault treats these files as opaque vendor artifacts: it records size and SHA-256 integrity but does not alter or claim to semantically validate their contents.

Official references:

- [DM7 saving settings to USB](https://manual.yamaha.com/pa/mixers/dm7/rm/en-US/9429464075.html)
- [DM7 SAVE/LOAD screen](https://manual.yamaha.com/pa/mixers/dm7/rm/en-US/11107091851.html)
- [RIVAGE PM settings-file contents](https://manual.yamaha.com/pa/mixers/RIVAGE_PM_series/en-US/5399171851.html)
- [RIVAGE PM supported load formats](https://manual.yamaha.com/pa/mixers/RIVAGE_PM_series/en-US/10743329291.html)

## Agent configuration

Configure exact operator-exported directories under `YamahaDm7ExportRoots` and `YamahaRivageExportRoots`. A DM7 root must contain at least one `.dm7f` file. A RIVAGE root must contain at least one current or recognized legacy settings file. Child-only scans and directories without a matching settings artifact are rejected.

The plugin inventories all regular companion files within the validated export directory, without following links, up to the command's 1–100,000 file limit. The existing immutable package, independent hash verification, and controlled test-restore operations provide the recovery path.

## Safe operator workflow

1. Save all console settings to USB using Yamaha's SAVE/LOAD workflow.
2. Wait for the console to finish accessing the device before removal.
3. Copy or mount the export beneath an exact Agent-configured root.
4. Discover, package, and verify the export with ShowVault.
5. Restore to an empty test target and use matching Editor software or non-production hardware for compatibility validation.
6. Before loading settings on production hardware, power down connected equipment and/or reduce output levels. Yamaha explicitly warns that loaded settings may cause the console to emit signals immediately.

## Compatibility boundary

This slice does not yet cover Yamaha CL/QL, TF, Rivage system logs, DSP processors, network switches, amplifiers, or speakers. Those remain mandatory catalog targets but require their own documented file formats, model/version fixtures, and safe recovery workflows. A passing hash verification proves the export was preserved exactly; it does not prove compatibility with another console model, bus configuration, license state, or firmware version.
