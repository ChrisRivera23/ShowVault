# Local-first milestone 3 planning handoff — 2026-08-13

The bounded milestone-3 extraction and architecture plan is complete on
`codex/local-first-milestone-3-plan`, based exactly on completed milestone-2
head `e6121aa5481db6d195d0e7a0584433c2d392bd81`.

The extraction/review commit is
`fc62f0ca72e780da01be6af6e5e9b5513ef065ac`. The controlling contract is
`docs/LOCAL_FIRST_MILESTONE_3_EXTRACTION.md`; the supporting audit is
`docs/LOCAL_FIRST_MILESTONE_3_RECONSTRUCTION_REVIEW_2026-08-13.md`.

## Selected outcome

**Open local vault → select a verified point → Restore or Cancel → verify the
restored copy → retain path-free local evidence**

This is controlled local Restore only. Hosted synchronization remains the
next roadmap slice. No upload, cloud receipt, application/device loading,
Recovery Confidence, Agent customer lifecycle, or native-platform proof is
included.

## Historical disposition

The exact containing range `c172e49..fff4434` has ten commits, 31 net paths,
`+5,387/-76`, binary-diff SHA-256
`7cb9d0c81ac5646353c9645eefd86844afa9706c8569fb2595afa241d188a317`,
and sorted path-list SHA-256
`751bd1a7eaceee71b89fd1a798ea4514acba92586c1e5479ebdae55a346ae0eb`.

The two Restore-bearing commits are `36fcda9` and `a62649f`. Preserve their
final product behavior, but do not replay their Dart engine. The approved
disposition is **replace/narrow**: extend the packaged .NET local engine,
retain filesystem identities through verification/copy/publication, persist
path-free durable Restore evidence, publish one fixed child without replacing
the selected sandbox, and keep Flutter limited to picker consent and status.

## Resume instructions

1. Read the continuation handoff, product bible, extraction contract, and
   reconstruction review completely.
2. Confirm this branch, worktree, foundation, and local commits without
   fetching or mutating external state.
3. Obtain explicit Product Owner authorization for local Restore
   implementation; planning authorization does not carry forward.
4. If authorized, implement in the contract's reconstruction order and run
   its complete adversarial and verification gates.
5. Preserve the milestone-1 Scan/Auth and milestone-2 Save/vault behavior, the
   user's primary-worktree files, and every external/native/data/cloud gate.

No product source or tests were changed by this planning checkpoint. No
external/native action or personal/customer/venue data access was performed.

The existing untracked `NEXT_CONVERSATION.md` in the user's primary worktree
is outside this branch and was not added or changed.
