# Local-first milestone 4 handoff — 2026-08-13

## Checkpoint

- Branch: `codex/local-first-milestone-4`
- Worktree: `/private/tmp/showvault-local-first-m4-implementation`
- Exact authorized planning base:
  `d1d0cdb6c9c3eba3675ff41640d7117da55b7508`
- Source implementation commit:
  `4b4e7632c3f349a2a70a528f31d4bad562d105e8`
- Documentation/evidence commit:
  `7c748fe15c027a8b914d00951910d5ba94ac1190`
- Product outcome: **Sign in → open a local vault → synchronize verified
  queued recovery points or Cancel → verify an immutable hosted receipt →
  retain durable path-free status**

Read `docs/LOCAL_QUEUE_SYNC.md`,
`docs/LOCAL_FIRST_MILESTONE_4_IMPLEMENTATION_2026-08-13.md`, and the original
extraction/reconstruction review completely before continuing.

## Completed implementation

Milestone 4 is complete locally. A separate packaged sync host consumes only
freshly reverified SQLite queue records, retains exact package file handles,
uploads bounded resumable chunks, verifies a tenant-bound immutable receipt,
and atomically records Synchronized. The existing Save/inspect/Restore host is
still network-free.

The API enforces Manager/Administrator/Owner organization and venue membership,
a closed privacy-filtered manifest, normalized safe relative paths, exact
offsets and digest-checked replay, immutable tenant-derived object keys,
complete object-set verification, database concurrency, and receipt-last
completion. Viewer, Technician, outsider, unsafe metadata, extra fields,
missing/extra/corrupt content, and conflicting replay fail closed.

Flutter shows informed manual consent only when signed in with a tenant venue
and an opened vault with queued points. It states that content and relative
filenames will upload, supports Cancel, renders path-free status, and refreshes
from durable local state.

Validation passed: local engine 65; API 29; Flutter 27 plus clean analysis;
Agent 291; contracts 22; platform 15; EF model gate; zero-warning Release builds
for local host, sync host, Agent, and API; formatting, locked dependency drift,
plist/project, shell, portable Windows callback, packaging-negative,
`git diff --check`, path/token, and complete changed-path checks.

## Evidence and authorization boundary

All data and tenants were synthetic. Development/tests use an in-memory object
adapter. Non-Development uses a disabled adapter and cannot begin a session.
Production hosted-byte durability, provider retention, IAM, migration,
readiness, monitoring, and hosted-copy Restore are not claimed.

No external product-system, cloud, account/billing, or native action is
authorized by this checkpoint. Do not fetch or push Git state, create or mutate
a PR, dispatch a workflow, use credentials or personal/customer/venue data,
provision cloud resources, select a production provider, deploy, build/install
a meaningful native artifact, use equipment, or clean up destructively without
new explicit authorization.

## Next gated decision

Stop for Product Owner direction. Per the ordered roadmap, the next bounded
slice is account, role, subscription, quota, and billing administration. It
requires selection of one exact outcome, complete historical accounting, a
current authorization/data/provider contract, and separate explicit
implementation authorization.

Production hosted-object storage and operational durability proof remains the
following independent slice. Native proof remains separately gated after that.
