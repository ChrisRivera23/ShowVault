# Stripe sandbox mainline integration plan — 2026-08-13

## Decision

Prepare a new current-main candidate from the three reviewed Stripe
code/evidence commits. Do not publish the historical
`codex/stripe-sandbox-proof` branch: it diverges before milestone 7 and contains
branch-specific interruption/handoff history that is not product source.

This task performed only local Git reconstruction and validation. It did not
push, open or mutate a pull request, dispatch or rerun a workflow, change a
provider or Keychain resource, deploy, release, or clean any branch/worktree.

## Exact inputs

- repository: `ChrisRivera23/ShowVault`;
- current remote `main`:
  `ffbb3902717fa02c56e7a66b5635f3e7d63981bb`;
- Stripe branch merge base with current `main`:
  `d468f38588d7ee760bbb2926b80d4e24268a7abd`;
- historical source branch: `codex/stripe-sandbox-proof`;
- historical source head:
  `4a7e439ef48796efef47f8c666f5d01e9cbca28f`;
- local candidate branch: `codex/stripe-sandbox-mainline-candidate`;
- candidate worktree: `/private/tmp/showvault-stripe-main-integration-plan`.

The initial adapter and local checkpoint commits `fd9a7d2` and `d468f38` are
already exact ancestors of current `main`; replaying either would be incorrect.

## Source classification

Carry these reviewed code/evidence commits in order:

| Historical source | Current-main candidate | Purpose |
| --- | --- | --- |
| `9f360e1` | `aa20c65` | Account provisioning evidence and card-only Checkout request. |
| `6819bf8` | `e81da88` | Dedicated Dahlia Invoice Payments retrieval. |
| `72bab23` | `b505e55` | Scheduled-cancellation compatibility and operational proof. |

Regenerate or omit these branch-specific handoff commits:

- `ad1867b` — interrupted-state handoff;
- `87ad5df` — compatibility-review handoff; and
- `4a7e439` — historical-branch operational handoff.

`git range-diff` reports each of the three carried commits as patch-identical to
its historical source and shows only the three excluded handoff commits as
absent. The cherry-picks applied without conflict.

## Exact reviewed candidate before this plan document

- head: `b505e5556673b34be68b9fafbe8720805a6b31a0`;
- tree: `48854930df79419fb485d3c6fb3a64be033f83c2`;
- commits ahead of exact base: 3;
- scope: 6 paths, `+300/-11`;
- path-list SHA-256:
  `9eac4e2ccf19853a75bbebcf4eb219275d9c979f3df80458053b1925e70a29f9`;
- binary full-index diff SHA-256:
  `53430bd0b3e97808992e75608a9c9bb98af48fadb5a82718b17ce8191f9076c4`.

The six paths are:

- `docs/STRIPE_DAHLIA_INVOICE_PAYMENTS_COMPATIBILITY_2026-08-13.md`;
- `docs/STRIPE_SANDBOX_ACCOUNT_PROVISIONING_2026-08-13.md`;
- `docs/STRIPE_SANDBOX_OPERATIONAL_PREFLIGHT_2026-08-13.md`;
- `docs/STRIPE_SANDBOX_OPERATIONAL_PROOF_2026-08-13.md`;
- `services/api/src/ShowVault.Api/Billing/StripeBillingProvider.cs`; and
- `services/api/tests/ShowVault.Api.Tests/StripeBillingProviderTests.cs`.

Checked-in billing remains disabled and contains no key or signing secret.

## Current-main validation

The reconstructed candidate passed the exact current CI-equivalent commands:

- Entity Framework: no pending model changes;
- API: 158 passed;
- Agent contracts: 22 passed;
- Platform: 30 passed;
- Agent: 291 passed;
- Agent Release build: zero warnings/errors;
- Account portal: 15 passed and Release build zero warnings/errors;
- Flutter: 32 passed and analysis reported no issues;
- focused .NET whitespace verification: passed;
- exact-base candidate `git diff --check`: passed; and
- exact-base candidate Stripe secret-value scan: passed.

Eight Flutter dependencies reported newer incompatible major/minor versions;
this is informational dependency drift and did not alter the lockfile or fail
analysis/tests.

## Future publication gate

Publication requires fresh explicit authorization. Immediately before any
remote write:

1. fetch and require remote `main` still equals exact
   `ffbb3902717fa02c56e7a66b5635f3e7d63981bb`;
2. require the local candidate branch is clean and descends from that exact
   base with only the three carried commits plus this planning evidence;
3. reproduce the six-path product/evidence delta and the three patch-identical
   range-diff mappings above, accounting separately for this plan document;
4. rerun `git diff --check`, the secret-value scan, focused adapter tests, and
   the complete current CI-equivalent gate if source or base changed;
5. push only `codex/stripe-sandbox-mainline-candidate` using an exact
   new-branch/no-overwrite guard;
6. open a draft pull request targeting `main` with an honest sandbox-only body;
7. wait for automatic push and pull-request API/Flutter CI without rerunning;
   and
8. stop for complete source review, metadata/body verification, and a separate
   ready/merge authorization.

Do not publish `codex/stripe-sandbox-proof`, expose Keychain values, enable
billing in checked-in settings, mutate Stripe resources, delete local secrets,
deploy, mark a PR ready, merge, release, or clean worktrees under publication
authorization alone.
