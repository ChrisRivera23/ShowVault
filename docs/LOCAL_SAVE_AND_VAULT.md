# Local Save and ShowVault Pro vault

## User flow

1. Select **Scan**. ShowVault checks only exact catalog locations.
2. A detected user-data finding exposes **Save**; an application finding does
   not.
3. Confirm the local-only operation.
4. Select the exact detected source folder, then independently select a local
   vault folder.
5. ShowVault reports bounded, path-free progress. **Cancel** remains available.
6. Success is reported separately as **Verified locally** and **Cloud queued**.
7. After restart, select **Open local vault**. ShowVault reads only the vault,
   rehashes every queued package, compares independent evidence, and does not
   rescan the original source.
8. A freshly verified point exposes **Restore**. Confirm the copy-only warning,
   choose an existing empty sandbox independently, then Restore or Cancel.
9. ShowVault publishes only `ShowVault Restored Files`, verifies it again, and
   reports **Restored locally** or **Restore attention** without displaying a
   filesystem path.

No upload occurs in this milestone. `Cloud queued` means that a locally
verified item is durably eligible for a future upload executor.

## Canonical layout

```text
ShowVault Pro/
├── Backups/<product>/<UTC timestamp>__<manifest SHA-256>/
│   ├── content/
│   ├── manifest.json
│   ├── verification.json
│   └── summary.txt
├── Manifests/<manifest SHA-256>/
│   ├── manifest.json
│   └── verification.json
├── Device Exports/
├── Upload Queue/local-engine.db
├── Reports/Restores/<Restore evidence SHA-256>.json
├── Logs/
└── Quarantine/
```

The SQLite queue enables foreign keys, WAL, `synchronous=FULL`, and a bounded
busy timeout. Schema initialization is transactional and idempotent. Package
identities are vault-relative; source paths, vault paths, content, credentials,
and access tokens are excluded.

## Evidence and immutability

The deterministic manifest records the opaque candidate key, product/plugin
identity, relative file names, sizes, SHA-256 values, and honest empty
dependency/compatibility collections. `verification.json` binds the recovery
point ID, manifest digest, exact file count and bytes, verification time,
passing result, and evidence digest.

The engine rejects existing identities rather than overwriting them. It checks
the staged package, atomically publishes it, compares the package and
independent evidence byte-for-byte, rehashes content again, and only then queues
it. Cancellation or failure removes staging. A newly published but unqueued
package is moved to `Quarantine`. Existing verified points are never deleted by
Save failure.

## Limits and unsupported content

Capture is bounded by file count, directory count, relative-path length,
per-file bytes, aggregate bytes, duration, and recovery-point count. Empty
sources/directories, links, reparse points, device/socket entries, mounted
subtrees, multiply-linked files, topology changes, identity swaps, late or
removed entries, and changed content fail closed.

The same contract is used on macOS and Windows. Automated tests use synthetic
roots only. Native packaging, sandbox/signing behavior, installation, and real
equipment remain separate proof gates.

See `docs/CONTROLLED_LOCAL_RESTORE.md` for the attended Restore, evidence,
rollback, cancellation, and restart-reselect contract.
