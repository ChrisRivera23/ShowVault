# Installed synthetic resilience matrix

This harness exercises ShowVault's installed macOS release artifact against the deployable Development API, disposable PostgreSQL, and disposable S3-compatible storage. It uses generated fixture bytes inside ShowVault's application sandbox. It does not inspect personal application data, enumerate the host, contact venue equipment, reboot the host, or establish production-provider durability.

## Run

The output directory must be absolute and absent:

```sh
apps/showvault_app/tool/run-resilience-matrix.sh \
  /private/tmp/showvault-resilience-matrix
```

The runner allocates loopback ports, creates a uniquely named Compose project, applies database migrations, starts a guarded Development personal-beta API, and builds a release app with an explicit compile-time resilience flag. Every matrix phase launches the copied `ShowVault.app/Contents/MacOS/ShowVault` executable as a new process. It never uses `flutter run` and does not install a separate customer Agent.

The command mode requires all of these gates:

- `SHOWVAULT_RESILIENCE_HARNESS=true` at compilation;
- an opaque `showvault-resilience-*` fixture identity;
- a loopback HTTP API;
- Development-only personal-beta API authentication;
- an exact command and allowlisted phase.

Normal builds compile the flag as false and keep command mode disabled. The generated matrix app contains disposable loopback configuration and must never be distributed to customers or installed at a venue.

## Executed phases

1. `prepare` creates two synthetic Serato-shaped sources, saves two immutable recovery points, verifies four files, and persists two queue records.
2. `api-unavailable` stops the API, confirms a path-free retry event, preserves the verified package, and publishes no receipt.
3. `interrupt-upload` restarts the API, cancels after the first accepted 8-byte chunk, and confirms durable remote bytes with no receipt.
4. `resume-upload` launches a fresh app process, resumes from the remote offset, verifies and commits once, accepts duplicate synchronization idempotently, and restores the exact two-file package.
5. `storage-unavailable` stops MinIO while leaving the API running, confirms failed readiness and a retryable queue state, and publishes no receipt.
6. `storage-resume` restarts storage and completes the same durable queued job.
7. `failure-matrix` proves safe handling for source mutation during Save, local-package tamper, corrupted remote bytes, an incomplete remote write, a conflicting duplicate chunk, a non-empty restore target, and interrupted Restore.
8. `finalize` hashes the installed executable and emits a bounded path-free report. An owned cleanup phase removes the synthetic sandbox workspace and Compose volumes.

Every failure assertion requires that the immutable local recovery point remains available when one was published, no invalid remote receipt exists, and no partial restore is published.

## Evidence artifact

The output contains only:

```text
ShowVault.app/
ShowVault-macos.zip
resilience-report.json
SHA256SUMS
```

`resilience-report.json` uses `showvault.resilience-matrix.v1`. It records the app version, installed executable SHA-256, platform, phase outcomes, elapsed milliseconds, health state, queue attempts, durable partial byte count, receipt/publication state, restore counts, and explicit limitation flags. It contains no access token, credential, file content, source path, vault path, restore path, host identity, or unrestricted inventory. The report includes a SHA-256 over its core fields, and `SHA256SUMS` covers both the ZIP and report.

## Current limitations

- This is macOS evidence only. Windows installed execution remains a separate gate.
- Process restart is executed; host reboot is explicitly not executed.
- MinIO validates S3-compatible behavior but is not the selected production provider.
- The build is ad hoc signed and not notarized or clean-machine validated.
- Expired commercial Auth0 sessions, storage quota exhaustion, unreadable real sources, upgrade/reinstall behavior, and a production-provider outage remain separate bounded scenarios.
- The harness calls the same recovery, queue, hosted transport, and restore services as the customer app, but it does not automate native picker UI. Earlier attended installed drills remain the picker/sandbox evidence.

## Recorded controlled execution — 2026-08-10

- Artifact directory: `/private/tmp/showvault-resilience-matrix-final-20260810`
- App: version `0.1.0 (1)`, bundle ID `com.showvault.app`, universal `x86_64` + `arm64`, ad hoc signed, sandboxed with network-client and user-selected read/write entitlements
- Installed executable SHA-256: `c6f3f59d4b94c525d2798955887a31124e681d2cf83e72a1256b9f3a7c8a03cb`
- ZIP SHA-256: `cd3a51917bfd4471d7c86738cf4ea1a130510279ed5038bdc8699cbc43fb6c7f`
- Report file SHA-256: `6f7a4fb018891faee17c396cde51ce0970479fe62de50e14361e535662a7d122`
- Report core evidence SHA-256: `18bc1c6cc2b3b15ccac4e773d35df71d61ec72da5cf9e7061e7f7756cf7bee73`
- Seven recorded phases passed. Preparation verified four files and 128 bytes across two packages. API loss recorded retry attempt 1. Interruption recorded retry attempt 2 with exactly 8 durable remote bytes and no receipt. A new app process resumed and synchronized on attempt 3, accepted duplicate completion idempotently, and restored two files/64 bytes.
- Storage loss recorded unavailable readiness, retry attempt 1, preserved local verification, and no receipt. Storage restart synchronized the same job on attempt 2.
- All seven failure cases passed: source mutation published no package; local tamper, corrupt remote bytes, incomplete remote bytes, and a conflicting duplicate chunk published no receipt; the non-empty target was unchanged; interrupted Restore published nothing.
- ZIP/report checksum verification, report-core checksum verification, strict deep code-signature validation, normal-build command-mode-disable test, path/credential scan, sandbox cleanup, and disposable container/network/volume cleanup passed.
- Host reboot and a production storage provider were explicitly not executed.
