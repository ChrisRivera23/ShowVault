# Local-first milestone 7 second PR-CI race repair and review — 2026-08-13

## Verdict

Verdict: **approved for the next separately authorized source-update preflight;
no actionable findings.**

The second repaired product head is
`0e00171f16ae4feca682de916cb29c862fe840ec` on local branch
`codex/local-first-m7-pr-ci-race2-repair`. It is one commit ahead of exact
published PR source `3f2496a41c7f5ec359971b5dc206e6a42159e798`, and its parent is that exact
source.

## Refined race and repair

The first repair covered a loser that reached `SaveChangesAsync`, received a
database update conflict, and temporarily could not observe the winner. The
simultaneous exact-head push workflow exposed an earlier interleaving:

1. the loser reads the invitation while it is pending;
2. the winner commits the accepted invitation and membership;
3. the loser sees that membership in its subsequent query; and
4. the loser pairs the membership with its stale pending invitation object and
   returns `InvitationUnavailable` before reaching the conflict retry.

The second repair routes an already accepted invitation or any membership that
appears after the invitation read through the existing bounded fresh-observation
path. It clears stale tracking first. Success therefore still requires a fresh
accepted invitation whose subject and accepted membership ID exactly match the
observed membership. A different-subject winner remains an immediate denial;
an unrelated existing membership remains fail-closed after the bounded
observation; and the path creates no membership or audit record.

## Deterministic regression and stress proof

A test-only one-shot command interceptor commits the synthetic winner after the
request reads the pending invitation and immediately before its membership
query. The new end-to-end regression failed with HTTP 400 before the product
change and passed afterward. It verifies HTTP 200 plus exactly one matching
membership and exactly one invitation-accept audit event.

All eight focused acceptance cases pass, including the deterministic
interleaving, bounded winner visibility, retry exhaustion, immediate
different-subject denial, all three existing-member lifecycle states, and the
original concurrent same-/different-subject scenario. The original concurrency
test then passed 80/80 repetitions across four concurrent test processes,
deliberately adding scheduler pressure similar to the two simultaneous GitHub
workflows.

## Exact scope and review

The product commit changes exactly three files:

- `services/api/src/ShowVault.Api/Account/AccountAdministrationService.cs`;
- `services/api/tests/ShowVault.Api.Tests/AccountAdversarialTests.cs`; and
- `services/api/tests/ShowVault.Api.Tests/TenantApiFactory.cs`.

There are 103 insertions and 10 deletions. Product tree SHA is
`8e68dc94b9793fdf494fa14f6950fc1f6370956f`. The changed-path SHA-256 is
`c2d17850eb97c05592958a636ca4b1006e8592b48e0c5299bac3a320bd018260`,
and the binary-diff SHA-256 is
`c6eed69fd1614e17c58423e159c1e466ca1b57afb74ea3206e7f59911575ca79`.

Fresh review traced the stale and fresh observations, success and denial paths,
retry bound, cancellation flow, and membership/audit invariants. It also
reviewed the test hook's one-shot locking, callback clearing before nested
database work, and unconditional cleanup. No actionable correctness, security,
tenant-isolation, resource-lifetime, test-reliability, or scope finding remains.
No dependency, migration, endpoint, contract, provider, or configuration value
changed. Formatting, `git diff --check`, and focused credential scans are clean.

## Complete validation

- API: 158 passed, zero failed or skipped;
- Platform: 30 passed;
- local engine: 67 passed;
- Agent contracts: 22 passed;
- Agent: 291 passed;
- account portal: 15 passed;
- Flutter: 32 passed; and
- Flutter analysis: no issues.

All 583 .NET tests and all 32 Flutter tests passed. EF Core reports no pending
model changes. Release builds for the API, account portal, Agent, local-engine
host, and sync-engine host completed sequentially with zero warnings and zero
errors. All changed C# files pass formatter verification.

## Boundary and next gate

Remote `main` remains exact `32c21cfbd51ea5f16bb5fe84c56f4efb125b1df4`,
and remote `codex/local-first-m7-mainline-candidate` plus draft PR #37 remain at
exact published source `3f2496a`. No commit was pushed, no PR metadata changed,
no workflow was dispatched or rerun, and no ready transition, merge, deployment,
provider/production operation, real-person data, or native action occurred.

The next bounded task is a separately authorized read-only source-update
preflight. It must revalidate PR #37, all exact-head checks, both remote refs,
and feedback; compare exact product `0e00171` to source `3f2496a`; and prepare a
truthful replacement body plus lease-protected fast-forward proposal. That
preflight would not itself authorize a push, PR edit, workflow rerun, ready
transition, or merge.
