# Local-first milestone 1 implementation evidence — 2026-08-13

## Boundary

This local-only reconstruction starts at exact foundation
`32c21cfbd51ea5f16bb5fe84c56f4efb125b1df4` on branch
`codex/local-first-milestone-1`. No fetch, push, PR mutation, workflow dispatch,
artifact retrieval, native-equipment access, personal/customer/venue data,
cloud resource, release, deployment, or destructive cleanup occurred.

## Reviewable implementation commits

1. `17d4410` — tenant-scoped desktop scan persistence, generated EF migration,
   independently allowlisted API submission, newest-scan reads, and API tests.
2. `323fe7c` — exact macOS/Windows catalog scanner, synthetic-home seam,
   signed-out/offline Scan UI, opaque-key API client, and Flutter tests.
3. `ffdd40b` — guarded personal-beta authentication, in-memory desktop sessions,
   loopback packaging guard, server Development/flag/identity/client guards, and
   focused tests.
4. `805a96c` — current milestone product/readiness documentation.

Agent protocol compatibility was conditional in the extraction manifest. The
foundation is protocol 1.1 rather than the historical 1.21 target, so no
`CollectCatalogApplications` compatibility command or customer-facing Agent
surface was reconstructed.

Implementation head `805a96c` has tree
`57301d99f89a1787c38ed861890b056169317a6e`, 32 changed paths, binary diff
SHA-256 `42bc33700efd56efbd99c47f55ce56b912947a91a58ce1504235162eeb39e863`,
and path-list SHA-256
`0d04c2dfc525cd81a99111efdee6361840e3f0d206e680c304d0cb674b41efa9`.

## Behavior proven in source and synthetic tests

- The desktop scanner evaluates only eight closed catalog candidates across
  macOS and Windows and never recursively enumerates applications or files.
- Synthetic-home mode suppresses real application candidates and inspects only
  its explicit synthetic user-data locations.
- Exact paths remain transient in Flutter memory. The API client serializes
  only `candidateKeys`.
- The server independently rejects null, unknown, path-like, oversized, and
  cross-tenant input; writes require Manager, Administrator, or Owner access to
  the exact organization/venue.
- Every scan stores a header, including an empty scan, and reads expose only
  candidates from the newest UUIDv7 scan.
- Direct results are labeled only `detected` and have no decision, validation,
  backup, verification, or restore endpoint.
- Scan remains available while signed out. A cloud submission failure retains
  the local findings in the desktop UI.
- Personal-beta bypass requires the compile-time client flag, loopback HTTP
  origin, server Development environment, explicit server flag, bounded
  identity, and a loopback request. Normal builds continue to use Auth0.
- macOS and Windows sessions remain in memory; no personal-Keychain call was
  added.

## Validation

- Flutter analysis: clean.
- Focused Flutter scanner/API/app tests: 12 passed.
- Complete Flutter suite: 21 passed.
- Agent contracts: 22 passed.
- Platform: 15 passed.
- Agent: 291 passed.
- API: 19 passed.
- EF `migrations has-pending-model-changes`: no changes.
- API Release build: zero warnings and zero errors.
- Changed-project .NET format verification: passed.
- `bash -n` packaging script verification: passed.
- Packaging negative guards: relative output exited 73; non-loopback no-login
  origin exited 64 before any build.
- `git diff --check`: passed.
- Final source/diff privacy and security inspection: passed.

## Evidence limits

No native macOS package was built, signed, notarized, installed, or opened. No
Windows build, installer, protocol activation, Gatekeeper behavior, Keychain
behavior, or end-to-end Auth0 login was exercised. Automated source and
synthetic tests are not native-platform or distribution proof.
