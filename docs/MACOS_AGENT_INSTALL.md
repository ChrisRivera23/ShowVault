# macOS Venue Agent installation

The production-style macOS package runs ShowVault as the hidden `_showvault` service account under the `com.showvault.venue-agent` LaunchDaemon. It does not depend on a logged-in operator or that operator's login Keychain.

## Security model

- The self-contained Agent payload and LaunchDaemon definition are root-owned and not group-writable.
- Runtime state, packages, logs, and secrets are owned by `_showvault`.
- A dedicated Keychain stores the durable Agent identity.
- The Keychain uses a random installer-generated password stored in a `_showvault`-owned `0600` file. This bootstrap secret never appears in the LaunchDaemon plist or application configuration.
- The one-time enrollment code exists only in the environment of the installer's enrollment process and is not written to disk.
- The Agent opens and unlocks the dedicated Keychain for each credential operation and fails closed on any Keychain error.

The bootstrap password file remains a root/service-account security boundary. Production hosts must use FileVault, restrict administrative access, and protect system backups. Code signing, notarization, installer signing, and rotation of the Keychain bootstrap secret remain release-hardening work.

## Build

From `services/agent` on Apple silicon:

```bash
./packaging/macos/build-package.sh osx-arm64 /tmp/showvault-agent-osx-arm64
```

Use `osx-x64` for Intel Macs. The staged directory contains the self-contained payload, installer, validator, and LaunchDaemon plist.

## Configure

Create an `appsettings.json` outside the package. It must contain the real HTTPS control-plane URI, Agent name, and only explicitly approved recovery roots. Do not place an enrollment code or durable credential in this file.

For the LIV pilot, configure separate roots for:

- Resolume compositions and user data on the macOS media server.
- grandMA2 exports copied to an approved location reachable by this Agent, if that workflow is hosted on the same Mac.
- Yamaha DM7 Compact all-settings exports copied to an approved location reachable by this Agent.
- L-Acoustics Soundvision/LA Network Manager recovery material copied to an approved location reachable by this Agent.
- Recovery packages and controlled restore targets owned or deliberately readable/writable by `_showvault`.

The LaunchDaemon cannot read an operator's private folders merely because the operator can. Stage exports into service-readable, venue-approved directories and grant the narrowest required ACLs.

## Install and enroll

Generate a fresh venue-scoped enrollment code immediately before installation, then run:

```bash
cd /tmp/showvault-agent-osx-arm64
sudo ./install.sh \
  --payload ./payload \
  --config /path/to/liv-agent-appsettings.json \
  --enrollment-code 'one-time-code'
```

The installer creates the service identity and directories, provisions the dedicated Keychain, performs one-shot enrollment, installs the plist, and starts the LaunchDaemon. A consumed enrollment code cannot be reused.

## Validate

```bash
sudo ./validate.sh
```

Validation confirms:

1. launchd has loaded the system service.
2. The Keychain bootstrap file is owned by `_showvault` with mode `0600`.
3. The dedicated Keychain unlocks as `_showvault` and contains the Agent identity.
4. A forced LaunchDaemon restart returns to the running state.

Then reboot with no interactive login, confirm the Agent becomes active in ShowVault, and run a controlled Scan → Backup → Verify → Restore. Do not perform the first restore into a live production directory.

## Installed locations

| Purpose | Location |
|---|---|
| Agent | `/Library/Application Support/ShowVault/Agent` |
| Configuration | `/Library/Application Support/ShowVault/Configuration` |
| SQLite state | `/Library/Application Support/ShowVault/State` |
| Recovery packages | `/Library/Application Support/ShowVault/Packages` |
| Dedicated Keychain | `/Library/Application Support/ShowVault/Secrets` |
| Logs | `/Library/Logs/ShowVault` |
| LaunchDaemon | `/Library/LaunchDaemons/com.showvault.venue-agent.plist` |

## Current limitation

This slice deliberately does not include destructive uninstall automation. Until package signing and upgrade semantics are finalized, removal should be treated as an attended administrative procedure that preserves state and recovery packages by default.
