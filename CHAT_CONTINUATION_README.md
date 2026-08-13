# ShowVault active continuation handoff

Read this file, `docs/LOCAL_FIRST_PRODUCT_BIBLE.md`,
`docs/LOCAL_QUEUE_SYNC.md`,
`docs/LOCAL_FIRST_MILESTONE_4_IMPLEMENTATION_2026-08-13.md`, and
`docs/LOCAL_FIRST_MILESTONE_4_HANDOFF_2026-08-13.md` completely before
continuing work from this branch.

## Current checkpoint — 2026-08-13

- Branch: `codex/local-first-milestone-4`
- Worktree: `/private/tmp/showvault-local-first-m4-implementation`
- Exact authorized planning base:
  `d1d0cdb6c9c3eba3675ff41640d7117da55b7508`
- Source implementation commit:
  `4b4e7632c3f349a2a70a528f31d4bad562d105e8`
- Documentation/evidence commit:
  `7c748fe15c027a8b914d00951910d5ba94ac1190`
- Product outcome:
  **Sign in → open a local vault → synchronize verified queued recovery points
  or Cancel → verify an immutable hosted receipt → retain durable path-free
  status**

Milestone 4 is complete locally. The separate packaged sync host takes only
freshly verified SQLite queue records, retains exact local file handles,
resumes bounded chunks, verifies an immutable tenant receipt, and atomically
records path-free status. The existing local host remains network-free.

The API has database-backed sessions/receipts, role and venue authorization,
closed privacy-filtered manifests, immutable object keys, exact offset/replay,
independent complete-object hashing, concurrency control, and receipt-last
completion. Flutter provides signed-in informed consent, Cancel, path-free
states, and durable refresh. Tokens are ephemeral and absent from SQLite and
process output.

Validation passed: local engine 65; API 29; Flutter 27 plus clean analysis;
Agent 291; contracts 22; platform 15; EF model gate; zero-warning Release
builds; and repository/packaging/security checks.

All fixtures were synthetic. Development/test object bytes are in memory and
not restart-durable. Non-Development synchronization is disabled. Production
storage durability, cloud operations, account/billing, and native proof remain
unproven and deferred.

## Authorization boundary

No external product-system, cloud, account/billing, or native action is
authorized by this checkpoint.
Do not fetch or push Git state, create or mutate a PR, dispatch a workflow,
retrieve artifacts, build or install a meaningful native package, use
equipment, access personal/customer/venue data, use credentials or cloud
resources, upload/synchronize, release, deploy, or clean up destructively
without new explicit authorization.

No native-platform proof is claimed. macOS/Windows Flutter build, signing,
sandbox/helper behavior, notarization, installation, upgrades, protocol
activation, Gatekeeper, personal-Keychain, privileged mount/reparse behavior,
equipment, and live application/device loading remain unproven.

## Next gated decision

Stop for Product Owner direction. Per the ordered roadmap, the next bounded
slice is account, role, subscription, quota, and billing administration. Before
implementation, select one exact outcome, account for its historical source,
write its current authorization/data/provider contract, and obtain separate
explicit authorization.

Production hosted-object storage and operational durability proof is the
following independent slice; native proof remains separately gated.

The existing untracked `NEXT_CONVERSATION.md` in the user's primary worktree
is outside this branch and was not added or changed.
