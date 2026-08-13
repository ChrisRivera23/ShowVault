# Local-first milestone 7 mainline promotion evidence — 2026-08-13

## Candidate and verdict

- Branch: `codex/local-first-m7-mainline-candidate`
- Worktree: `/private/tmp/showvault-local-first-m7-mainline-candidate`
- Pinned local `origin/main` base:
  `32c21cfbd51ea5f16bb5fe84c56f4efb125b1df4`
- Repaired product head:
  `cbffeb3e6e7776fade2424262f0053ae583b092d`
- Scope: 54 linear commits and 192 files.

Verdict: **local mainline candidate approved; no actionable findings.**

The candidate was created from the pinned local `origin/main` tracking ref and
advanced with `git merge --ff-only` to the exact reviewed formatter-repaired
product head. No merge commit or conflict resolution was required.

## Complete validation

- Platform: 30 passed;
- local engine: 67 passed;
- Agent contracts: 22 passed;
- Agent: 291 passed;
- API: 154 passed;
- account portal: 15 passed;
- Flutter: 32 passed; and
- Flutter analysis: no issues.

All 579 .NET tests and all 32 Flutter tests passed with zero failures or skips.
EF Core reported no pending model changes.

Release builds passed with zero warnings and zero errors for the API, account
portal, Agent, local-engine host, and sync-engine host. Formatting verification
passed for every changed C# and Dart file across the complete 54-commit scope;
`git diff --check` was clean.

Checked-in account invitation and portal configuration remains disabled and
secret-free. Live/restricted credential, private-key, browser-storage, and
`offline_access` scans returned no matches. Residual direct membership reads
remain limited to centralized authority, account persistence, and initial owner
creation. No generated `bin`/`obj` status drift appeared.

## Scope and authorization boundary

The promoted product is the six local-first milestones, the disabled Stripe
sandbox adapter/preflight seam, account-portal milestone 7, its reviewed
remediation, and the formatter-only repair. The candidate remains local and
synthetic.

The user's primary worktree remained on `codex/windows-packaging` with its
pre-existing untracked `NEXT_CONVERSATION.md`. Local `main`, local
`origin/main`, existing milestone/repair branches, and external Git state were
not moved.

No fetch, push, pull request, workflow dispatch, release, database migration
application, deployment, production enablement, Auth0/Stripe mutation,
real-person data, native installation, or other external state change occurred.

## Next gate

The candidate is ready for a separately authorized mainline-disposition
preflight. That preflight must reconcile the distinct local `main`, pinned
tracking ref, primary worktree history, and any current remote state before any
branch movement or publication. This approval alone does not authorize moving
`main` or interacting with GitHub.
