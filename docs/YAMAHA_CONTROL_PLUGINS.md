# Yamaha control-panel project plugin

`showvault.yamaha-provisionaire-control` protects editable ProVisionaire Control PLUS projects and their exported ProVisionaire Kiosk controllers as one recovery unit.

Yamaha documents these native files:

- `.pvcppj` is the editable ProVisionaire Control PLUS project containing controllers, pages, images, and registered devices;
- `.pvksk` is an exported controller file loaded by ProVisionaire Kiosk.

ShowVault requires the editable `.pvcppj` project. A `.pvksk` file without its source project is intentionally rejected as an incomplete recovery unit, although exported controllers and all other regular companion assets are preserved when stored inside the validated project root.

Official references:

- [ProVisionaire Control PLUS file types](https://manual.yamaha.com/pa/pv/pvcp/en/01_AboutPV_en.html)
- [Exporting controller files](https://manual.yamaha.com/pa/pv/pvcp/en/16_DialogWindow_en.html)
- [ProVisionaire Kiosk controller startup](https://manual.yamaha.com/pa/pv/pvk/en/05_Boot_en.html)

## Agent configuration

Configure exact operator-managed directories under `YamahaProVisionaireControlProjectRoots`. Each directory must contain at least one `.pvcppj` project. Child-only scans and arbitrary directories are rejected.

The plugin recursively inventories regular files without following links, subject to the command's 1–100,000 file limit. The existing immutable package, independent SHA-256 verification, and controlled restore flow provide the protection path.

## Safe operator workflow

1. Save the current editable project in ProVisionaire Control PLUS.
2. Export each deployed Kiosk controller into a companion directory under the project root.
3. Include custom images and venue restore notes required to reproduce the panels.
4. Discover, package, verify, and perform a controlled test restore with ShowVault.
5. Open the restored project in a compatible Control PLUS version, verify device identifiers and network bindings, and test in Control Mode before transferring controllers to production Kiosk devices.

## Compatibility boundary

Cryptographic verification proves exact preservation, not that registered device identifiers, IP addressing, product firmware, Kiosk versions, passcodes, or external assets remain valid. The files may contain sensitive operational layout and device information; access control and future package encryption remain separate platform concerns.
