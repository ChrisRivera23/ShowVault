# ShowVault active continuation handoff

Read this file, `docs/LOCAL_FIRST_PRODUCT_BIBLE.md`,
`docs/LOCAL_QUEUE_SYNC.md`,
`docs/LOCAL_FIRST_MILESTONE_5_EXTRACTION.md`,
`docs/LOCAL_FIRST_MILESTONE_5_RECONSTRUCTION_REVIEW_2026-08-13.md`,
`docs/ACCOUNT_BILLING_ADMIN_ARCHITECTURE.md`, and
`docs/LOCAL_FIRST_MILESTONE_5_PLAN_HANDOFF_2026-08-13.md` completely before
continuing work from this branch.

## Current checkpoint — 2026-08-13

- Branch: `codex/local-first-milestone-5-plan`
- Worktree: `/private/tmp/showvault-local-first-m5-plan/worktree`
- Exact milestone-4 base:
  `80fb1092df254f2f6bd3d11b209634b52beb7a15`
- Extraction/architecture commit:
  `a541c88b815e99ffcb64d00f0c9b15ffe11499b6`
- Product outcome:
  **Sign in as an organization Owner → open Plan and storage → review
  server-derived license/subscription eligibility and logical hosted usage →
  allow or deny each new hosted-sync reservation from the same projection →
  retain path-free audited evidence**

Milestone 5 extraction and architecture planning are complete. The plan uses
separate license/subscription projections, an Owner-only minimized read model,
and the same server evaluator for new hosted-sync eligibility. Organization-wide
logical bytes are reserved atomically with session creation and transferred to
committed usage exactly once. Existing reserved sessions can still resume,
commit, and recover receipts after a later commercial state change.

No payment provider was selected, installed, or contacted. There is no
checkout, webhook, price, invoice, payment method, customer portal,
membership/role mutation, internal staff Admin, deletion/reclamation,
production object-storage change, cloud operation, or native proof in this
checkpoint. Tests and Development implementation, if later authorized, use
explicit synthetic commercial records; missing or unsupported production state
denies new sessions.

## Authorization boundary

This checkpoint authorizes no implementation, migration, external product
system, account/billing action, credential, customer/venue/payment data, cloud
resource, native action, external Git action, deployment, or destructive
cleanup.

Do not modify application/API/engine source or migrations; install or contact a
billing provider; fetch or push Git state; create or mutate a PR; dispatch a
workflow; retrieve artifacts; build or install a meaningful native package;
use equipment; access personal/customer/venue/payment data; use credentials or
cloud resources; upload/synchronize; release; deploy; or clean up destructively
without new explicit authorization.

## Next gated decision

Stop for Product Owner direction. The next bounded action is implementation of
the exact milestone-5 provider-independent contract, using synthetic local
fixtures only. It requires separate explicit implementation authorization.

Provider-backed billing and webhooks, customer account portal,
membership/invitation and role administration, internal staff Admin,
production hosted-object storage, and native proof remain separately gated.

The existing untracked `NEXT_CONVERSATION.md` in the user's primary worktree is
outside this branch and was not added or changed.
