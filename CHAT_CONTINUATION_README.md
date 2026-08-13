# ShowVault active continuation handoff

Read this file, `docs/LOCAL_FIRST_PRODUCT_BIBLE.md`,
`docs/LOCAL_FIRST_MILESTONE_3_EXTRACTION.md`, and
`docs/LOCAL_FIRST_MILESTONE_3_RECONSTRUCTION_REVIEW_2026-08-13.md` completely
before continuing work from this branch.

## Current checkpoint — 2026-08-13

- Branch: `codex/local-first-milestone-3-plan`
- Worktree: `/private/tmp/showvault-local-first-m3-plan/worktree`
- Exact milestone-2 foundation:
  `e6121aa5481db6d195d0e7a0584433c2d392bd81`
- Extraction/review commit:
  `fc62f0ca72e780da01be6af6e5e9b5513ef065ac`
- Selected outcome:
  **Open local vault → select a verified point → Restore or Cancel → verify the
  restored copy → retain path-free local evidence**

The bounded milestone-3 extraction and architecture plan is complete. The
historical containing range is exact `c172e49..fff4434`: ten commits, 31 net
paths, `+5,387/-76`, binary-diff SHA-256
`7cb9d0c81ac5646353c9645eefd86844afa9706c8569fb2595afa241d188a317`,
and sorted path-list SHA-256
`751bd1a7eaceee71b89fd1a798ea4514acba92586c1e5479ebdae55a346ae0eb`.

The historical Restore disposition is **replace/narrow**. Retain the attended,
signed-out/offline, verified-point-only flow and fixed `ShowVault Restored
Files` sandbox child. Replace the Dart filesystem engine by extending the
packaged .NET local engine with retained package/target identities, durable
path-free Restore state/evidence, atomic non-overwriting publication, and
restart repair. Flutter remains native consent/status only. Hosted
synchronization is the following roadmap slice, not part of milestone 3.

## Authorization boundary

This checkpoint is documentation and planning only. It does not authorize or
contain Restore product implementation. Obtain new explicit local
implementation authorization before changing product source or tests.

No external or native action is authorized. Do not fetch, push, create or
mutate a PR, dispatch a workflow, retrieve artifacts, build or install a
meaningful native package, use equipment, access personal/customer/venue data,
use cloud resources, upload/synchronize, release, deploy, or clean up
destructively without separate explicit authorization.

No native-platform proof is claimed. macOS/Windows Flutter build, signing,
sandbox/helper behavior, notarization, installation, upgrades, protocol
activation, Gatekeeper, personal-Keychain, and end-to-end login remain
unproven. Application/device loading, Recovery Confidence, dependency
completeness, compatibility, and license portability remain out of scope.

## Next gated decision

Stop for Product Owner authorization. If local Restore implementation is
authorized, follow `docs/LOCAL_FIRST_MILESTONE_3_EXTRACTION.md` in its stated
reconstruction order and keep every external/native/data/cloud gate closed.
Do not transplant the historical Dart implementation or widen the target to
hosted synchronization.

The existing untracked `NEXT_CONVERSATION.md` in the user's primary worktree
is outside this branch and was not added or changed.
