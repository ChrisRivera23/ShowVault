# Yamaha ProVisionaire Control PLUS Assisted recovery

ShowVault provides a disabled-by-default legacy-Agent Assisted recovery profile
for an operator-created, dedicated ProVisionaire Control PLUS staging
directory. Configure only explicit absolute project roots in
`YamahaProVisionaireControlProjectRoots`.

This compatibility profile is not the customer-facing **Scan this computer**
experience. A configured root must belong to exactly one Yamaha profile; same,
ancestor, descendant, duplicate, relative, linked, or substituted roots are
rejected.

## What ShowVault preserves

The Agent requires an editable `.pvcppj` project at the configured root level.
A project marker in a child directory cannot authorize the parent tree, and a
known primary format owned by another Yamaha profile makes capture fail closed.
A `.pvksk` file cannot authorize capture by itself.

After the primary-project check, ShowVault preserves regular files inside the
exact operator-selected staging root. It recognizes `.pvksk` files as opaque
ProVisionaire Kiosk controller exports. Other regular files are preserved only
as operator-selected opaque companions; their presence does not prove that
Yamaha software requires them or that the project is complete.

Capture and packaging retain no-follow filesystem identities and recheck exact
topology, sizes, and hashes. File count, directory count, relative-path length,
per-file bytes, total bytes, time, and cancellation are bounded. A late add,
delete, rename, replacement, identity substitution, or authorization change
prevents publication or reuse of a stale package. Agent protocol outcomes are
path-free.

## Compatibility boundary

Yamaha documents `.pvcppj` as the project file containing all settings for a
ProVisionaire Control PLUS project, including multiple controllers, pages,
images, and controlled devices. Yamaha separately documents `.pvksk` as the
controller file loaded into ProVisionaire Kiosk, containing one controller and
its pages, images, and controlled-device settings.

ShowVault does not parse either opaque format. A recognized extension and
matching hash do not prove semantic validity, export completeness, device
identity, software or firmware compatibility, network configuration, external
dependencies, or live-device state. A Kiosk controller export does not replace
the editable Control PLUS project.

Restore only into a new empty ShowVault-controlled target. An operator must
then open the verified project with compatible Yamaha software, confirm the
source and destination versions and dependency closure, and validate away from
production. ShowVault never writes directly into a live Yamaha application
tree or device.

Primary vendor references:

- [ProVisionaire Control PLUS introduction and file formats](https://manual.yamaha.com/pa/pv/pvcp/en/01_AboutPV_en.html)
- [Export Controller File dialog](https://manual.yamaha.com/pa/pv/pvcp/en/16_DialogWindow_en.html#export_controller_file)
- [ProVisionaire Kiosk controller-file startup](https://manual.yamaha.com/pa/pv/pvk/en/05_Boot_en.html#enabling_kiosk_auto_start)

This slice provides synthetic macOS/Linux-compatible filesystem evidence only.
It is not proof of native Windows reparse behavior, Yamaha application import,
software or firmware compatibility, hardware restore, personal-data readiness,
or venue readiness.
