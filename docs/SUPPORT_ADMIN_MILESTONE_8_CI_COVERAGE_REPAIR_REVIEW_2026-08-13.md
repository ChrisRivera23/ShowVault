# Support Admin milestone 8 CI coverage repair review — 2026-08-13

## Verdict

Verdict: **approve with no further repair. The Support BFF CI coverage blocker
is closed locally; stop before publication.**

The exact workflow/evidence commit was independently reviewed against its
publication-preflight parent, the recorded defect, the active workflow
contract, the Support project paths, the contingent pull-request body, and the
complete validation evidence. The three added commands are correctly placed,
executable, and sufficient to make the existing hosted `api` job restore, test,
and build the new Support BFF. No workflow or evidence defect remains.

This review adds documentation only.

## Exact review input

- Worktree: `/private/tmp/showvault-milestone-8-support-admin-plan`.
- Branch: `codex/milestone-8-support-admin-plan`.
- Publication-preflight parent:
  `66d863c82ee75d581b5f976251fd939aabff6cb8`.
- CI repair/evidence commit:
  `e53d04bade47bd1744d1b5c1b6a69494978ea274`.
- Repair tree:
  `4769b83807cc1acc88bd47847625eb8a13a9fe33`.
- Repair delta: two files, `+120/-0`.
- Sorted path-list SHA-256:
  `b098758675ce0e9948292334cf18613800a911ea02a9fe90503a61cf92a4925c`.
- Binary full-index diff SHA-256:
  `4cf818a9a922e9d4658ffaf025972d0435a6521d8500cc0fccc8d8218e847a16`.

The scoped worktree was clean with no staged paths, and both `HEAD` and the
branch ref matched the exact repair commit before this review document was
added. Commit, sole parent, tree, path list, statistics, and hashes all
reproduced from immutable objects.

## Workflow delta review

The parent workflow is exact blob
`e5f40987be3ea78e00e42ffc4818f648a44f7c08`, 1,776 bytes/39 lines. The repaired
workflow is exact blob `a71a56547af4afa68b43a9c28681d6d89ef325f2`,
2,172 bytes/42 lines.

The workflow-only delta is one path, three insertions, no deletion, sorted
path-list SHA-256
`b1e4fcd28055c712644fe84f6a1e30a41018cf387dd808cec348f2e505e33a2a`,
and binary full-index diff SHA-256
`adb314d950f6abcdde1860606de5e1bb34018f2db1fb26eab6dc400fd46d4307`.

The exact inserted commands are:

```yaml
      - run: dotnet restore apps/support_admin/tests/ShowVault.SupportAdmin.Tests/ShowVault.SupportAdmin.Tests.csproj
      - run: dotnet test apps/support_admin/tests/ShowVault.SupportAdmin.Tests/ShowVault.SupportAdmin.Tests.csproj --configuration Release
      - run: dotnet build apps/support_admin/src/ShowVault.SupportAdmin/ShowVault.SupportAdmin.csproj --configuration Release --no-restore
```

Removing lines containing `apps/support_admin` from the repaired workflow
produces the parent workflow byte-for-byte. This independently proves that all
preexisting actions, EF/API/contracts/platform/Agent/account-portal commands,
Flutter commands, runners, ordering, and whitespace are unchanged.

## Structure and CI-semantics review

- The installed YAML parser accepts the complete repaired file.
- Trigger inventory remains exactly `push` and `pull_request`.
- Job inventory remains exactly `api` and `flutter`.
- Support restore/test/build is inside the existing Ubuntu `api` job, after the
  account-portal sequence and before the separate Flutter job.
- The Support test project path occurs exactly twice, for restore and Release
  test; the application project path occurs exactly once, for Release build.
- Restore precedes test, and the test-project restore covers its application
  project reference before the application `--no-restore` build.
- Exact project path case and `.csproj` names match the repository.
- Release configuration aligns with the reviewed local Support gate and the
  existing account-portal CI pattern.
- No permission, secret, token, environment, service, matrix, cache, artifact,
  dependency, third-party action, shell, runner, or trigger was added.
- No application/service source, migration, test, configuration, dependency,
  or lockfile path changed.

The change closes the precise publication blocker without widening workflow or
product scope.

## Independent focused execution

The review reran the exact three added commands sequentially:

- Support test-project restore: passed;
- Support BFF Release tests: **13 passed, 0 failed, 0 skipped**; and
- Support BFF Release build: **0 warnings, 0 errors**.

The worktree remained clean after execution, confirming that restore/build
outputs are ignored and no lockfile or generated tracked file drifted.

The repair evidence's complete unchanged workflow-equivalent run was also
checked for internal consistency:

- EF pending model: no changes;
- API 170/170, contracts 22/22, platform 40/40, Agent 291/291;
- account portal Release 15/15 and Support BFF Release 13/13;
- Flutter 32/32 and analysis clean; and
- Agent, account portal, and Support BFF Release builds with zero warnings and
  errors.

No count, command, ordering, or scope contradiction was found.

## Complete candidate and body pins

The reviewed product base
`2dfb4cd82b6ca3cf1ef3928f73c8fe00e194b0a5` through repair commit is 14 exact
linear commits, 51 paths, and `+6190/-14`, with sorted path-list SHA-256
`a6b872c4da89a92b44c13248c3199bcb7b6589158299f14138af6327aed61c77`
and binary full-index diff SHA-256
`e2bda39deecac388eca194dfb697fb09a773726cb3e8532b960d9ae5a8bfdf06`.
All values reproduce.

The contingent PR body remains 3,553 bytes/74 newline-terminated lines with
SHA-256
`dfd3ff8eac67fbdc8434bd5a369c8d9718fb1c33971f21f307d93532315a6d45`.
Its statement that Support CI coverage must exist before draft publication is
now satisfied locally, but publication still requires a fresh complete
no-drift preflight and explicit authorization.

## Security and operational boundary

The repair and review do not enable Support, change its trust/privacy/session
behavior, configure identity, provision assignments/grants, apply a migration,
or touch real data. No GitHub branch, pull request, ref, workflow run, provider,
production system, deployment, release, native system, or Keychain value was
created, read beyond the authorized local evidence, or mutated. No fetch, push,
dispatch, rerun, or cleanup occurred.

## Stop boundary and next gate

Stop after this documentation-only local review commit. The next task requires
fresh explicit authorization for a complete read-only publication preflight
from the resulting exact review head. It must repeat all local candidate and
corrected-hash pins and freshly read remote main, source-ref/PR absence,
permissions, policy, feedback, workflow, and current CI state before proposing
any exact push/PR action.

Do not publish/push, create or mutate a PR/ref/workflow, fetch, dispatch or
rerun hosted CI, clean branches/worktrees, force-push, revert, deploy, release,
mutate identity/provider/production state, access Keychain values, use real
data, or perform native operations.
