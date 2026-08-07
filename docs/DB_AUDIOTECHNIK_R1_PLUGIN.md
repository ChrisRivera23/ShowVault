# d&b audiotechnik R1 project recovery

ShowVault discovers a current d&b audiotechnik R1/ArrayCalc project only inside an exact operator-approved root containing a non-empty `.dbpr` project file. Empty `.dbpr` lookalikes, companion files without a current project, and unapproved parent or child paths do not qualify.

The complete approved root is recovery content. Keep the shared R1/ArrayCalc project together with legacy `.r1p` and `.dbac2` revisions, workspace graphics, exported `.rcs` R1 Control settings and `.rss` R1 System settings, logs, system-check evidence, equipment inventories, and restore instructions. d&b documents that current R1 and ArrayCalc share `.dbpr`, while `.r1p` and `.dbac2` are older formats.

## Configuration

Add absolute project-folder paths to `Agent:DbAudiotechnikR1ProjectRoots`.

```json
{
  "Agent": {
    "DbAudiotechnikR1ProjectRoots": [
      "/Users/operator/Documents/d&b/Venue A"
    ]
  }
}
```

The plugin ID is `showvault.db-audiotechnik-r1`.

## Supervised restore prerequisites

Record and confirm:

- the exact R1 and ArrayCalc versions and a compatible operating system;
- amplifier, processor, interface, and loudspeaker models plus firmware versions;
- OCA/AES70, Ethernet, CAN-Bus, remote-ID, address, and network topology;
- amplifier channel assignments, loudspeaker configurations, routing, EQ, delay, and protection state;
- ArrayProcessing, System check, ArrayVerification, DS100/Soundscape, Dante, and clocking state where applicable;
- project password availability and the deliberate online synchronization procedure.

d&b release notes warn that projects saved by newer R1 releases may not open in older R1 or ArrayCalc versions. Legacy `.r1p`, `.dbac2`, `.rcs`, and `.rss` files alone do not qualify as the current recovery anchor. Restoring files does not authorize connection or synchronization with live equipment.

## Official references

- [R1 Remote control software](https://www.dbaudio.com/global/en/products/software/r1/)
- [R1 software and licenses quick guide](https://www.dbaudio.com/assets/products/downloads/software/r1/dbaudio-quick-guide-r1-software-and-licenses-1.2-en.pdf)
- [TI 391: Effective use of R1](https://www.dbaudio.com/assets/products/downloads/ti/dbaudio-technical-information-ti391-1.2-en.pdf)
- [R1 V3 release notes](https://www.dbaudio.com/assets/products/downloads/software/r1/dbaudio-r1-v3.42.4-release-notes-en.pdf)
- [TI 501: d&b Soundscape system design and operation](https://www.dbsoundscape.com/assets/products/downloads/ti/dbaudio-technical-information-ti501-1.12-en.pdf)
