# Local desktop Save

The Flutter desktop application implements the first bounded local-first Save path for exact catalog-defined `UserDataRoot` findings.

## Operator flow

1. **Scan this computer** checks only exact catalog locations and reads no file contents.
2. Findings remain available in app memory even when cloud authentication or the API is unavailable.
3. A `UserDataRoot` finding displays **Save**.
4. Save confirmation explains the source and vault access before any native picker opens.
5. The native directory picker must return the exact canonical catalog-approved source. A substitution, missing directory, alias, or filesystem link is rejected.
6. The operator selects the configurable local vault in a second native picker. The app retains no persistent bookmark or broad filesystem grant; the vault is selected again after restart.
7. Only after confirmation and both permission checks does the app read source contents.
8. The interface reports **Verified locally** separately from **Cloud queued** or **Queue attention**.
9. After restart, **Open local vault** reads only ShowVault-owned manifests, package manifests, and queue records to restore those statuses. It does not rescan source contents.
10. A locally verified recovery point displays **Restore**. The attended offline workflow reverifies the package and publishes only into an operator-selected empty target; see `docs/LOCAL_ATTENDED_RESTORE.md`.

Installed-application findings remain detection-only and cannot be saved as user data.

## Local recovery-point behavior

The app creates the canonical vault structure, currently defaulting to `Documents/ShowVault Pro`. A successful Save publishes:

```text
Backups/<product>/<UTC timestamp>__<manifest SHA-256>/
├── content/
├── manifest.json
└── summary.txt
```

It also writes an independent manifest copy to `Manifests/<ID>.json` and an atomic durable queue record to `Upload Queue/<ID>.json`. The queue record contains local package identity and status but no source path. The recovery manifest is local and may contain the canonical source path because restore requires it; it is not submitted by the discovery API.

The Save engine enforces:

- exact allowlisted `UserDataRoot` resolution;
- root and descendant link rejection;
- containment and normalized relative logical paths;
- file-count, per-file-size, total-byte, and timeout limits;
- explicit cancellation checks;
- source size and modification checks before and after copying;
- streaming SHA-256 while copying;
- independent streaming SHA-256 verification of staged content;
- staging cleanup on failure;
- atomic immutable publication without overwriting prior recovery points;
- verified-only durable upload-queue creation.

Vault reopening additionally bounds manifest count and record sizes, verifies each independent manifest's SHA-256 identity, requires an identical package manifest, validates package and queue identity, and treats a missing queue record as **Queue attention** without weakening local verification.

An empty root, unsupported filesystem entry, mutation, failed verification, cancellation, or limit violation publishes no recovery point and no upload job.

## Privacy boundary

The control plane stores and returns only the opaque allowlisted candidate key and bounded product metadata. It does not receive the local source path from Scan or Save. Local findings remain usable if cloud submission fails.

## Current limitations

- Save and restore tests use synthetic fixtures only. No personal Serato or Resolume file content was read.
- A compile-time `SHOWVAULT_SYNTHETIC_FIXTURE_HOME` build option isolates attended installed-app drills from personal catalog locations and suppresses installed-application candidates. It is absent from normal builds.
- The installed macOS app completed a synthetic native-picker Save and restored one verified/queued recovery point after process restart. The normal release includes the macOS sandbox user-selected read/write entitlement.
- Windows uses the same tested Dart permission contract and native directory selector, but Windows packaging and installed runtime behavior remain unproven.
- No persistent security-scoped bookmark is stored. Operators explicitly reopen a vault after each process restart; background access across launches is not claimed.
- A resumable, idempotent, checksum-verifying executor is implemented against a controlled filesystem object-store substitute. Production authenticated cloud transport, tenant binding, bandwidth policy, and conflict handling remain unimplemented.
- Attended local restore now verifies exact package and target bytes through sibling staging and records path-safe local evidence. An installed macOS restore drill and Windows runtime proof remain outstanding.
- The manifest records empty dependency and compatibility collections for this first slice. Dependency closure and Recovery Confidence are not claimed.
- The legacy .NET Agent keeps its SQLite queue for compatibility; the customer desktop currently uses atomic JSON queue records. These must be consolidated behind one packaged local-engine contract before production.
