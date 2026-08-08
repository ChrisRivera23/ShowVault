# LIV nightclub pilot

LIV nightclub is the first approved real-venue pilot for ShowVault.

## Pilot systems

| System | Initial recovery target |
|---|---|
| Lighting | MA Lighting grandMA2 exports |
| Audio console | Yamaha DM7 Compact all-settings exports and companions |
| Media server | Resolume on macOS, including compositions and user data |
| Sound system | L-Acoustics Soundvision and LA Network Manager recovery material |

The pilot credits only systems for which LIV supplies a real, operator-approved artifact and exact source boundary. Network reachability alone does not count as protection.

## First milestone

1. Identify the pilot Mac model, architecture, macOS version, storage layout, and administrative owner.
2. Build and install the matching self-contained Venue Agent package.
3. Validate dedicated-Keychain access after a forced LaunchDaemon restart and a logged-out reboot.
4. Agree on service-readable staging roots for each system; do not grant blanket home-directory or volume access.
5. Run discovery against copied or exported artifacts before touching production locations.
6. Create and independently verify immutable packages.
7. Restore each package only into a controlled empty target.
8. Have the responsible LIV operator open or inspect the restored artifact using compatible vendor software.
9. Record hashes, versions, prerequisites, screenshots, operator sign-off, and every deviation.

## Information required on site

- Pilot Mac hardware architecture and exact macOS version.
- Resolume Arena/Avenue version, composition locations, user-data location, media paths, and whether media is local or external.
- grandMA2 software version and the exact export workflow/location used by the lighting team.
- Yamaha DM7 Compact firmware and DM7 Editor/console export version, plus the approved `.DM7F` export location.
- Soundvision, LA Network Manager, amplified-controller firmware versions, and the approved project/session export locations.
- Available controlled restore volume and minimum free space.
- Maintenance window, operator names, and rollback contact.

Live device synchronization, console loading, showfile activation, amplifier-controller synchronization, and production output changes are outside the first restore proof. Those actions require explicit operator approval in a later supervised phase.
