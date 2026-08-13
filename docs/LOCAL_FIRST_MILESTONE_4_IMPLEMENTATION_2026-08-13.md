# Local-first milestone 4 implementation — 2026-08-13

## Outcome

Implemented the exact bounded outcome:

**Sign in → open a local vault → synchronize verified queued recovery points or
Cancel → verify an immutable hosted receipt → retain durable path-free status**

Implementation commit:
`4b4e7632c3f349a2a70a528f31d4bad562d105e8`.

No cloud account, production object store, production credential, personal or
customer data, deployment, external Git mutation, meaningful native build, or
account/billing system was used.

## Implemented boundaries

- Per-vault SQLite owns sync attempt count, state, next retry, opaque session,
  remote-manifest digest, receipt digest, and completion time.
- Fresh verification now retains exact no-follow package content handles for
  the complete upload. Local tamper uploads nothing and enters queue attention.
- `showvault-sync-engine` is a separate closed process host. The existing
  Save/inspect/Restore host remains network-free.
- Tokens are single-invocation process input and are absent from durable state
  and path-free process output.
- The hosted manifest excludes absolute paths, product display name, operation
  IDs, local evidence, restore evidence, credentials, and unrestricted local
  metadata.
- API authorization joins the authenticated subject to current tenant
  membership and venue ownership. Manager, Administrator, and Owner may write;
  Viewer, Technician, and outsiders may not.
- Closed schema validation, normalized safe relative paths, explicit count/size
  bounds, 256 KiB chunks, exact offsets, identical replay, conflict rejection,
  database concurrency, immutable object keys, complete-object listing, full
  rehash, and receipt-last completion are implemented.
- Empty files are materialized explicitly. Missing, extra, corrupt, truncated,
  or tenant-conflicting state cannot produce a receipt.
- Cancellation and transient unavailability preserve resumability and use
  exponential retry capped at 30 minutes.
- Flutter presents informed manual consent, progress, Cancel, path-free status,
  and post-result vault refresh only in a signed-in tenant context.
- macOS and Windows build rules package both hosts into the private
  `local-engine` directory.

## Database and provider topology

Migration `20260813063954_AddHostedSyncSessions` adds tenant-bound upload session
and receipt state with a concurrency revision and unique
organization/venue/recovery-point identity. Organization and venue deletion is
restricted rather than cascading hosted receipt evidence.

The object-store interface supports exact length, append, read, and bounded
prefix listing. Development/tests use the synthetic in-memory implementation.
Non-Development uses a disabled implementation, and `begin` fails unavailable
before recording a session. This deliberately prevents a production durability
claim or accidental filesystem fallback.

## Validation

- Local-engine Release tests: 65 passed, including retained-handle upload,
  packaged sync-host closed schema/path-free output, transient retry,
  cancellation/resume, local tamper attention, receipt verification, and token
  and absolute-path absence from SQLite.
- API Release tests: 29 passed, including synthetic local-engine-to-API
  end-to-end completion, owner/manager/administrator authorization,
  viewer/technician/outsider denial, tenant binding, closed schema, unsafe path
  rejection, identical/conflicting replay, zero-byte content, missing-content
  rejection, idempotent commit, and receipt fetch.
- Flutter analyze: clean. Flutter tests: 27 passed, including signed-out hiding,
  content/filename disclosure, Cancel wiring, synchronized refresh, and no path
  rendering.
- Agent contracts: 22 passed; Platform: 15 passed; Agent: 291 passed.
- EF pending-model gate: clean.
- Local host, sync host, Agent, and API Release builds: zero warnings and zero
  errors.
- Changed .NET and Dart formatting: clean.
- Locked Flutter dependency resolution: no repository drift.
- macOS/iOS plists, macOS project file, both shell syntax checks, Windows
  callback portable test, packaging negative guards, and `git diff --check`:
  passed.

## Evidence limits

All content and tenant fixtures were synthetic and local. The Development
object adapter is process-memory-only; it proves protocol behavior but not
restart durability of hosted bytes. No production provider, retention,
replication, IAM, ingress/TLS, readiness, monitoring, migration, cleanup,
account administration, entitlement, quota, billing, remote Restore, or
hosted-copy recoverability is claimed.

No macOS or Windows Flutter application was built, signed, sandbox-tested,
notarized, installed, launched, or upgraded. No Windows runtime, protocol
activation, equipment, venue, or live application/device proof is claimed.
