# Support Admin milestone 8 CI coverage repair — 2026-08-13

## Verdict

Verdict: **the bounded local CI coverage repair is complete and passes the
entire existing workflow-equivalent gate. Stop for independent review before
publication.**

The active CI `api` job now restores, runs the 13 Support BFF Release tests, and
builds the Support BFF in Release configuration after the existing account-
portal steps. The repair changes no Support product source and preserves every
existing workflow trigger, job, and command.

## Exact input

- Worktree: `/private/tmp/showvault-milestone-8-support-admin-plan`.
- Branch: `codex/milestone-8-support-admin-plan`.
- Exact publication-preflight input:
  `66d863c82ee75d581b5f976251fd939aabff6cb8`.
- Input tree:
  `11d172eaf0999cc3623a5fab244a699002fc6ea3`.
- Input workflow blob:
  `e5f40987be3ea78e00e42ffc4818f648a44f7c08`.
- Input workflow size: 1,776 bytes/39 lines.

The worktree was clean, had no staged paths, and the branch ref matched the
exact input before the workflow repair.

## Exact repair

Only `.github/workflows/ci.yml` changed before this evidence file was added.
The delta is exactly three insertions and no deletion. Immediately after the
unchanged account-portal Release test/build steps in the existing `api` job it
adds:

```yaml
      - run: dotnet restore apps/support_admin/tests/ShowVault.SupportAdmin.Tests/ShowVault.SupportAdmin.Tests.csproj
      - run: dotnet test apps/support_admin/tests/ShowVault.SupportAdmin.Tests/ShowVault.SupportAdmin.Tests.csproj --configuration Release
      - run: dotnet build apps/support_admin/src/ShowVault.SupportAdmin/ShowVault.SupportAdmin.csproj --configuration Release --no-restore
```

- Repaired workflow blob:
  `a71a56547af4afa68b43a9c28681d6d89ef325f2`.
- Repaired workflow size: 2,172 bytes/42 lines.
- Workflow-only sorted path-list SHA-256:
  `b1e4fcd28055c712644fe84f6a1e30a41018cf387dd808cec348f2e505e33a2a`.
- Workflow-only binary full-index diff SHA-256:
  `adb314d950f6abcdde1860606de5e1bb34018f2db1fb26eab6dc400fd46d4307`.

No application, service, migration, test, dependency, lockfile,
configuration, or other workflow path changed.

## Workflow structure review

- Ruby's installed YAML parser accepts the complete file.
- Trigger inventory remains exactly one `push` and one `pull_request`.
- Job inventory remains exactly one `api` and one `flutter`.
- All preexisting API, EF, contracts, platform, Agent, account-portal, and
  Flutter steps are byte-unchanged.
- The Support test-project path occurs exactly twice: restore and Release test.
- The Support application-project path occurs exactly once: Release build.
- The new steps are ordered after account portal and before the separate
  Flutter job, matching the existing portal restore/test/build pattern.
- Workflow diff whitespace and repository `git diff --check` pass.

No new action, dependency, secret, permission, environment, runner, matrix,
artifact, cache, service, shell, or workflow trigger was added.

## Complete local CI-equivalent validation

The exact commands in the repaired workflow were executed sequentially.

- EF pending-model gate: no changes since the last migration.
- API tests: **170 passed, 0 failed, 0 skipped**.
- Agent contract tests: **22 passed, 0 failed, 0 skipped**.
- Platform tests: **40 passed, 0 failed, 0 skipped**.
- Agent tests: **291 passed, 0 failed, 0 skipped**.
- Agent Release build: **0 warnings, 0 errors**.
- Account portal Release tests: **15 passed, 0 failed, 0 skipped**.
- Account portal Release build: **0 warnings, 0 errors**.
- Support BFF Release tests: **13 passed, 0 failed, 0 skipped**.
- Support BFF Release build: **0 warnings, 0 errors**.
- Flutter dependency resolution: passed without lockfile change.
- Flutter analysis: no issues.
- Flutter tests: **32 passed**.

Flutter reported eight available versions incompatible with the current
constraints. This is the same informational dependency drift and did not
modify `pubspec.lock`, analysis, tests, or this repair scope.

All product-source bytes are identical to the reviewed input. Generated
`bin`/`obj` outputs remain ignored local build artifacts and are not part of the
Git delta.

## Security and operational boundary

The repair only makes hosted CI exercise the already reviewed disabled-by-
default Support application. It does not enable Support configuration, alter
authentication/authorization/session behavior, configure identity, provision
staff or grants, apply a migration, use real data, or touch provider,
production, deployment, release, native, or Keychain state.

No branch, pull request, ref, or hosted workflow was created or mutated. No
fetch, push, dispatch, or rerun occurred.

## Stop boundary and next gate

Stop after one local workflow/evidence commit. The next task requires fresh
explicit authorization and must perform a read-only review of the exact repair
commit, workflow delta, structure, validation evidence, and full candidate
pins. Repair only a proven workflow/evidence defect and stop again before
publication.

Do not publish or push; create or mutate a PR/ref/workflow; fetch; dispatch or
rerun hosted CI; clean branches/worktrees; force-push; revert; deploy; release;
mutate identity/provider/production state; access Keychain values; use real
data; or perform native operations.
