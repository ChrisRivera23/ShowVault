# MA Lighting grandMA2 and grandMA3 export plugins

ShowVault treats grandMA2 and grandMA3 as distinct compatibility targets:

- `showvault.malighting-grandma2`
- `showvault.malighting-grandma3`

Both initial recovery units are operator-created USB/export trees. ShowVault reads exact locally configured roots, hashes their regular files without following links, and feeds the inventories into the existing immutable package, independent verification, and controlled test-restore flow. It never writes to a console, connects over FTP/SFTP, invokes console commands, or edits MA Lighting files.

## Official product boundaries

MA Lighting documents that grandMA3 USB show files and backups live under `grandMA3/shared/shows` and `grandMA3/shared/backups`; version-independent exported objects live under `grandMA3/gma3_library`. The grandMA3 plugin requires an exact `grandMA3` root with at least one of those structures.

grandMA2 exports use a `gma2` tree. Show files are version-specific, and exported objects commonly live in `importexport`, `macro`, or fixture-related folders. The grandMA2 plugin requires an exact `gma2` root containing a direct or version-directory `shows` folder.

References:

- [grandMA3 folder structure](https://help.malighting.com/grandMA3/2.2/HTML/fm_folder_structure.html)
- [grandMA3 show-file handling](https://help.malighting.com/grandMA3/2.2/HTML/show_file_management.html)
- [grandMA2 Backup menu](https://help2.malighting.com/grandMA2/en/help/key_backup_menu.html)
- [grandMA2 folders and exports](https://help2.malighting.com/grandMA2/en/help/key_network_ftp.html)

## Network identification

grandMA3 and grandMA2 use separate, independently authorized probes. The grandMA2 probe follows MA Lighting's official [Telnet Remote documentation](https://help2.malighting.com/grandMA2/en/help/key_remote_control_telnet.html): it connects to TCP 30000, sends zero bytes, reads at most 4,096 bytes, and requires the documented guest/login prompt. Telnet Remote must already be enabled. ShowVault never logs in, sends a command, retains the greeting centrally, or treats an open port as a match.

## Agent configuration

Configure exact exported roots under `GrandMa2ExportRoots` and `GrandMa3ExportRoots`. Empty lists disable the corresponding integration. A `StartDiscovery` command chooses the matching plugin ID, exact root, and a file limit from 1–100,000.

## Operator workflow

1. Save the show from the console/onPC Backup menu to removable media. For grandMA3, enable the documented media export option when the show depends on media-pool assets.
2. Preserve the original export and software version; do not re-save it in newer software under the same name.
3. Attach or copy the export to an Agent host and configure its exact root.
4. Discover, package, and verify it with ShowVault.
5. Restore only into an empty, allowlisted test target.
6. Validate loading on matching MA Lighting software/hardware before production use.

## Compatibility warning

MA Lighting documents that show files move forward between software versions but cannot be moved back after being saved by a newer version. Structural and SHA-256 verification proves package integrity, not console/software compatibility. Semantic version extraction and a controlled load test remain required before ShowVault can claim higher verification levels.
