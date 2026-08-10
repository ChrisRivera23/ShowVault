# Upgrade preservation and local support diagnostics

ShowVault treats the operator-selected local vault as durable customer recovery data, independent of the application bundle. Replacing or reinstalling the application must not delete or migrate that vault. A newly installed app reopens the vault only after the operator selects it and then rehydrates status from validated ShowVault-owned metadata without scanning recovery sources.

## Operator diagnostic flow

After opening a local vault, the operator can choose **Create support diagnostic**. ShowVault displays the exact data boundary and requires a second explicit confirmation. It then validates bounded records under `Manifests`, `Upload Queue`, and `Reports/Restores`, and creates a JSON report plus SHA-256 sidecar under `Reports/Diagnostics`.

The diagnostic is local. ShowVault does not upload or transmit it. Any later transfer to support is a separate operator action outside this workflow.

The versioned `showvault.support-diagnostic.v1` document contains:

- app, vault-schema, and diagnostic-schema versions;
- UTC generation time and the fixed operator-authorized scope label;
- recovery-point and restore-evidence counts;
- bounded cloud workflow counts;
- opaque package ID, approved candidate key, product label, creation time, file count, total bytes, local/cloud status, queue attempt count, queue event count, and a bounded error category;
- integrity results stating which metadata was validated and that package contents and recovery sources were not read; and
- a SHA-256 digest of the report core.

The bounded error categories are `none`, `queueIntentMissing`, `cancelled`, `transientUnavailable`, `retryExhausted`, `integrityFailure`, and `workflowFailure`. Raw error text is never copied into a diagnostic.

The document excludes credentials, tokens, package contents, filenames inside packages, exact local paths, source roots, restore destinations, host/user identity, network inventory, installed-application inventory, and unrestricted logs. The checksum sidecar contains only the digest and diagnostic filename.

## Validation and containment

Diagnostic generation reuses the local vault inspector. It verifies independent and package manifest equality and identity, bounded manifest metadata, queue identity/status/attempts, and every append-only queue-state event. Restore evidence must use the exact versioned key set, refer to a currently validated package, use fixed path-free target and verification labels, remain within its size/count limits, and pass its own SHA-256 check.

Directory and entry types are checked without following links. Linked, substituted, malformed, oversized, wrongly identified, unbounded, or checksum-invalid records stop the operation before a report is published. A linked `Reports/Diagnostics` destination is rejected and cannot redirect a write outside the selected vault. The generator never opens recovery package content or a recorded source path.

Current limits are 10,000 restore-evidence files, 64 KiB per restore-evidence or queue-state record, 1,000 queue events per package, and 2 MiB for the generated diagnostic. Existing recovery manifest limits remain authoritative.

## Upgrade, reinstall, rollback, and removal

Application replacement and attended reinstall preserve the selected vault in full:

- `Backups` immutable recovery points;
- `Manifests` independent manifests;
- `Upload Queue` intent and append-only state journals;
- `Reports` restore and diagnostic evidence; and
- `Digital Twin` ShowVault-owned local state.

The replacement application does not need the original source, endpoint identity, scan history, or a persisted login session to reopen and validate the vault. Authentication remains separate and in memory for the current desktop beta.

Rollback uses the same rule: replace only the application bundle, then explicitly reopen the existing vault. A rollback is acceptable only if the older application supports the vault and report schema versions present; otherwise it must fail without changing the vault. The current controlled proof checks forward replacement, not rollback compatibility.

Ordinary app removal deletes only the application bundle. It retains the entire selected vault by default. ShowVault currently provides no destructive in-app removal control. If the operator separately requests full local-data removal, that attended procedure must identify the exact selected vault, preserve or export any required recovery data first, and delete only the five ShowVault-owned top-level directories listed above. It must not delete source data, unrelated neighboring files, credentials, or hosted copies. Hosted-data deletion is a separate authenticated control-plane operation and is not implied by local uninstall.

## Installed macOS evidence — 2026-08-10

The versioned runner `apps/showvault_app/tool/run-upgrade-diagnostic-proof.sh` compiled two release applications with different generation defines. It copied the before build to a fixed installed-app location, created and verified an immutable two-file synthetic recovery point, recorded retry and synchronization, restored it, generated a diagnostic, and deleted the synthetic source. It then replaced the installed app with the independently compiled after build. The after build reopened the unchanged vault and verified the package, independent manifest, synchronized attempt 2, four append-only state events, one restore-evidence record, and source-free rehydration.

Final evidence directory: `/private/tmp/showvault-upgrade-diagnostic-final-20260810`

| Artifact | SHA-256 |
|---|---|
| `ShowVault-before-macos.zip` | `47b22ad7d022f405e856ddd55e3c4d5c2d12139a5450d88dd2813d00b7020971` |
| `ShowVault-after-macos.zip` | `2f9ae2078bb31235505dd89931932aa21fc6f9cb465138cb73baae82ec3a1788` |
| `upgrade-diagnostic-report.json` | `780f50a6dd924f7349fe34dd65114e26b420ca048a4dcc50a5be10a00a665db6` |
| Report core | `e9a8d46207dd299d6f79ec9dc761422af02f5ad9b2f3bee59b57359f4ca1eb51` |

The before and after executable hashes were distinct. Both copied apps passed strict deep code-signature validation with ad hoc personal-test signing. The report is path-free and records the unexecuted boundaries explicitly.

This proves a controlled attended macOS application-replacement boundary. It does not prove a clean-machine installer, rollback, host reboot, Windows behavior, Apple notarization, production-provider outage behavior, quota exhaustion, expired commercial sessions, personal-data recovery, or venue readiness.
