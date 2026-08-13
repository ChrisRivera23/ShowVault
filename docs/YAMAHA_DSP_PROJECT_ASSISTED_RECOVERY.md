# Yamaha project-file Assisted recovery

ShowVault provides two disabled-by-default legacy-Agent Assisted recovery
profiles for operator-created, dedicated staging directories:

- **Yamaha ProVisionaire Design Project** recognizes a root-level `.pvd` file.
- **Yamaha MTX/MRX Editor Project** recognizes a root-level `.mtx` file.

These profiles are compatibility infrastructure, not the customer-facing
**Scan this computer** experience. Configure only explicit absolute project
roots in `YamahaProVisionaireDesignProjectRoots` or
`YamahaMtxMrxProjectRoots`. A root must belong to exactly one Yamaha profile;
same, ancestor, descendant, duplicate, relative, linked, or substituted roots
are rejected.

## What ShowVault preserves

The Agent requires the profile's primary format at the configured root level.
A marker in a child directory cannot authorize the parent tree, and a known
primary format from another Yamaha family makes the capture fail closed. After
that check, ShowVault preserves regular files inside the exact operator-selected
root as opaque companions. Their presence is an operator choice, not evidence
that Yamaha software requires them or that the project is complete.

Capture and packaging retain no-follow filesystem identities and recheck exact
topology, sizes, and hashes. File count, directory count, relative-path length,
per-file bytes, total bytes, time, and cancellation are bounded. A late add,
delete, rename, replacement, identity substitution, or authorization change
prevents publication or reuse of a stale package. Outcomes sent through the
Agent protocol remain path-free.

## Compatibility boundary

Yamaha documents `.pvd` as the general ProVisionaire Design project format.
The extension does not prove that a project contains a DME7 or a PC-D/DI
amplifier. DME7 and PC412-D, PC412-DI, PC406-D, or PC406-DI are therefore only
operator-asserted Assisted use cases of the same generic profile. Yamaha
documents `.mtx` as the MTX-MRX Editor project format, but ShowVault does not
parse either opaque format or infer device identity, semantic validity,
firmware compatibility, editor-version compatibility, external dependencies,
or live-device state.

Restore only into a new empty ShowVault-controlled target. An operator must
then open or import the verified files with compatible Yamaha software and
validate them away from production. ShowVault never writes directly into a
live Yamaha application tree or device.

Primary vendor references:

- [ProVisionaire Design overview](https://manual.yamaha.com/pa/pv/pvd/en/YJ-H0/01_AboutPV_en.html)
- [PC-D/DI ProVisionaire Design workflow](https://manual.yamaha.com/pa/power_amps/pc-d_di/en/01_Introduction_en.html)
- [PC Series in ProVisionaire Design](https://manual.yamaha.com/pa/pv/pvd/en/YJ-H0/17_DeviceSheet_PC_en.html)
- [MTX-MRX Editor User Guide](https://europe.yamaha.com/en/download/files/2099916/)

This slice provides synthetic macOS/Linux-compatible filesystem evidence only.
It is not proof of native Windows reparse behavior, vendor application import,
firmware compatibility, hardware restore, personal-data readiness, or venue
readiness.
