# ShowVault active continuation handoff

Read this file, `docs/LOCAL_FIRST_PRODUCT_BIBLE.md`,
`docs/LOCAL_FIRST_MILESTONE_4_EXTRACTION.md`,
`docs/LOCAL_FIRST_MILESTONE_4_RECONSTRUCTION_REVIEW_2026-08-13.md`, and
`docs/LOCAL_FIRST_MILESTONE_4_PLAN_HANDOFF_2026-08-13.md` completely before
continuing work from this branch.

## Current checkpoint — 2026-08-13

- Branch: `codex/local-first-milestone-4-plan`
- Worktree: `/private/tmp/showvault-local-first-m4-plan/worktree`
- Exact completed milestone-3 base:
  `32f1b2c74241f89b6185c90db31a9f508f61739c`
- Milestone-4 extraction/architecture commit:
  `270b60954ebc66cd464df3f3bbb806d95dcec044`
- Product outcome:
  **Sign in → open a local vault → synchronize verified queued recovery points
  or Cancel → verify an immutable hosted receipt → retain durable path-free
  status**

Milestone 4 extraction and architecture planning is complete; implementation
has not begun. The selected historical source is exactly `f016ad1`, `5f05f44`,
and `a7eee0d`. The contract retains verified-only resumable behavior,
tenant/role authorization, exact-offset idempotency, independent hosted
verification, receipt-last completion, retry, cancellation, and UI refresh.

It replaces the historical Dart/JSON/filesystem authorities with the current
.NET/per-vault SQLite authority, a separate network-capable .NET sync host,
database-backed hosted sessions/receipts, and an immutable object abstraction.
The existing Save/inspect/Restore host remains network-free. Customer backup
content and relative filenames require explicit manual consent; tokens are
ephemeral; all planning evidence is synthetic. Production object storage,
cloud operations, account/billing administration, quota enforcement, and
native proof are deferred.

## Authorization boundary

No implementation, external product-system, cloud, or native action is
authorized by this checkpoint.
Do not fetch or push Git state, create or mutate a PR, dispatch a workflow,
retrieve artifacts, modify application/API/engine code or migrations, build or
install a meaningful native package, use equipment, access
personal/customer/venue data, use credentials or cloud resources,
upload/synchronize, release, deploy, or clean up destructively without new
explicit authorization.

No native-platform proof is claimed. macOS/Windows Flutter build, signing,
sandbox/helper behavior, notarization, installation, upgrades, protocol
activation, Gatekeeper, personal-Keychain, privileged mount/reparse behavior,
equipment, and live application/device loading remain unproven.

## Next gated decision

Stop for Product Owner direction. The next bounded action is implementation of
the exact milestone-4 hosted-synchronization contract, using synthetic local
fixtures only. It requires separate explicit implementation authorization.

Account, role, subscription, quota, and billing administration remains a later
independent slice and is not part of milestone 4.

The existing untracked `NEXT_CONVERSATION.md` in the user's primary worktree
is outside this branch and was not added or changed.
