# Local-first milestone 5 handoff — 2026-08-13

## Checkpoint

- Branch: `codex/local-first-milestone-5`
- Worktree: `/private/tmp/showvault-local-first-m5-implementation`
- Exact planning base: `3ebe9394536b8aabf7e9643be6af8f7de7ebfe6f`
- Source implementation commit: `3cb4452d64c4881d1451e47631cb1a907674c1d3`
- Product outcome: **Sign in as an organization Owner → open Plan and storage
  → review server-derived license/subscription eligibility and logical hosted
  usage → allow or deny each new hosted-sync reservation from the same
  projection → retain path-free audited evidence**

Read `docs/LOCAL_FIRST_MILESTONE_5_EXTRACTION.md`,
`docs/ACCOUNT_BILLING_ADMIN_ARCHITECTURE.md`, and
`docs/LOCAL_FIRST_MILESTONE_5_IMPLEMENTATION_2026-08-13.md` completely before
continuing from this checkpoint.

## Completed implementation

The API database now owns independent license/subscription projections,
deterministic policy evaluation, organization logical usage, exact hosted
reservations, and append-only minimized audit evidence. Owner plan reads are
closed and tenant-scoped. New hosted sessions reserve quota atomically;
idempotent/concurrent begin and commit do not double count. Existing reserved
sessions remain recoverable after later commercial ineligibility.

The synthetic plan catalog is Development/test-only and is never automatically
assigned. Non-Development policy is disabled and denies new sessions. The local
engine maps commercial/quota responses to bounded attention. Flutter Settings
shows the read-only summary to Owners and does not request details for lesser
roles.

Final validation passed: Platform 23; API 39; local engine 67; Flutter 30 plus
clean analysis; Agent 291; contracts 22; EF model gate; zero-warning Release
builds; formatting; diff checks; and changed-file path/secret checks.

## Authorization boundary

No billing provider, checkout, webhook, provider/customer record, price,
invoice, payment method, membership or role mutation, customer portal,
internal staff Admin, cleanup/reclamation, production object-store operation,
deployment, customer data, credential, native action, equipment use, or
external Git action was authorized or performed.

Do not fetch or push Git state, create or mutate a PR, dispatch workflows,
install or contact a billing provider, retrieve artifacts, use credentials or
personal/customer/venue/payment data, mutate cloud resources, build/install a
meaningful native package, deploy, release, or clean up destructively without
new explicit authorization.

## Next gated decision

Stop for Product Owner direction. Per the roadmap, the next bounded slice is
provider-backed billing, signed webhook projection, and customer account portal
policy. Before implementation, select one exact provider outcome, account for
its historical source, define financial data/authorization/lifecycle contracts,
and obtain separate explicit authorization.

Membership/invitation and role administration, internal staff Admin,
production hosted-object storage, and native proof remain separately gated.
