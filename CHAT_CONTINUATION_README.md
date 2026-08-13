# ShowVault active continuation handoff

Read this file, `docs/LOCAL_FIRST_PRODUCT_BIBLE.md`,
`docs/LOCAL_QUEUE_SYNC.md`,
`docs/LOCAL_FIRST_MILESTONE_5_EXTRACTION.md`,
`docs/ACCOUNT_BILLING_ADMIN_ARCHITECTURE.md`,
`docs/LOCAL_FIRST_MILESTONE_5_IMPLEMENTATION_2026-08-13.md`, and
`docs/LOCAL_FIRST_MILESTONE_5_HANDOFF_2026-08-13.md` completely before
continuing work from this branch.

## Current checkpoint — 2026-08-13

- Branch: `codex/local-first-milestone-5`
- Worktree: `/private/tmp/showvault-local-first-m5-implementation`
- Exact authorized planning base:
  `3ebe9394536b8aabf7e9643be6af8f7de7ebfe6f`
- Source implementation commit:
  `3cb4452d64c4881d1451e47631cb1a907674c1d3`
- Product outcome:
  **Sign in as an organization Owner → open Plan and storage → review
  server-derived license/subscription eligibility and logical hosted usage →
  allow or deny each new hosted-sync reservation from the same projection →
  retain path-free audited evidence**

Milestone 5 is complete locally. Separate license/subscription projections,
Development/test-only synthetic policy, organization committed/reserved byte
accounting, hosted-session reservations, append-only minimized audit, Owner
plan reads, bounded local attention, and Flutter Settings exposure are
implemented. Concurrent/idempotent begin and commit do not double count, and
already reserved uploads remain recoverable after later commercial
ineligibility.

Validation passed: Platform 23; API 39; local engine 67; Flutter 30 plus clean
analysis; Agent 291; contracts 22; EF model gate; zero-warning Release builds;
formatting; diff checks; and changed-file secret/path checks.

All fixtures were synthetic. No billing provider was selected, installed, or
contacted. Non-Development commercial policy is disabled. Payment correctness,
provider operations, customer/staff administration, production storage,
deployment, native correctness, and equipment readiness remain unproven.

## Authorization boundary

No external product-system, billing-provider, cloud, administration, or native
action is authorized by this checkpoint.

Do not fetch or push Git state, create or mutate a PR, dispatch a workflow,
install or contact a provider, retrieve artifacts, create checkout/customer
records, use credentials or personal/customer/venue/payment data, mutate cloud
resources, build or install a meaningful native package, use equipment,
upload/synchronize, release, deploy, or clean up destructively without new
explicit authorization.

## Next gated decision

Stop for Product Owner direction. Per the ordered roadmap, the next bounded
slice is provider-backed billing, signed webhook projection, customer portal,
and financial lifecycle policy. Before implementation, select one exact
provider outcome, account for its historical source, write its current
financial-data/authorization/lifecycle contract, and obtain separate explicit
authorization.

Membership/invitation and role administration, internal support Admin,
production hosted-object storage, and native proof remain separate gated
slices.

The existing untracked `NEXT_CONVERSATION.md` in the user's primary worktree is
outside this branch and was not added or changed.
