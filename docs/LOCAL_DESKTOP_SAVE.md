# Local desktop Save

The Flutter desktop application implements the first bounded local-first Save path for exact catalog-defined `UserDataRoot` findings.

## Operator flow

1. **Scan this computer** checks only exact catalog locations and reads no file contents.
2. Findings remain available in app memory even when cloud authentication or the API is unavailable.
3. A `UserDataRoot` finding displays **Save**.
4. Save confirmation explains that ShowVault will read that exact root, create a new immutable local recovery point, verify it, and queue it for later cloud synchronization.
5. Only after confirmation does the app resolve the opaque catalog key to its local path and read source contents.
6. The interface reports **Verified locally** separately from **Cloud queued** or **Queue attention**.

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

An empty root, unsupported filesystem entry, mutation, failed verification, cancellation, or limit violation publishes no recovery point and no upload job.

## Privacy boundary

The control plane stores and returns only the opaque allowlisted candidate key and bounded product metadata. It does not receive the local source path from Scan or Save. Local findings remain usable if cloud submission fails.

## Current limitations

- Tests use synthetic fixtures only. No personal Serato or Resolume file content was read.
- The release macOS app builds, but installed-app access to the source and default Documents vault has not been proven through the macOS sandbox permission flow. Permission onboarding must be implemented and tested before a personal-data drill.
- Durable recovery points and queue records survive restart, but the UI does not yet rehydrate their status after app restart.
- The cloud synchronization executor, retry/backoff, resumability, remote checksum verification, and conflict handling are not implemented.
- The manifest records empty dependency and compatibility collections for this first slice. Dependency closure and Recovery Confidence are not claimed.
- The legacy .NET Agent keeps its SQLite queue for compatibility; the customer desktop currently uses atomic JSON queue records. These must be consolidated behind one packaged local-engine contract before production.
