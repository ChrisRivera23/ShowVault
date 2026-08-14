# Milestone 8 Support origin-isolation repair review — 2026-08-14

## Verdict

The exact local repair commit is approved without further repair. Its
normalized-origin comparison closes the reviewed default-port alias defect,
the focused coverage proves both rejection and allowed distinctness, all
preserved Support boundaries remain intact, and repeated local and remote
no-drift gates pass.

Stop after this documentation-only local review commit. Publication and every
pull-request mutation remain separately gated.

## Exact reviewed input

- Worktree: `/private/tmp/showvault-milestone-8-support-admin-plan`.
- Branch: `codex/milestone-8-support-admin-plan`.
- Reviewed commit:
  `aab47ba349891aeffce81ef1785da3a69b724fb6`.
- Sole parent:
  `9fe24bb6afdbed9fb5db88195e30d26f22dfef84`.
- Tree: `b4ddda1173dd2429a9db8b8a1cbd8dccd8744613`.
- Branch ref: exact reviewed commit.
- Delta: three paths, `+158/-1`.
- Sorted path-list SHA-256:
  `4cbc3eb56fbe9be0345b6c821edc993b391df7acb95ab43c4d96ee6bcd754dda`.
- Binary full-index diff SHA-256:
  `88a3a89150c260c11fc575f4b19f68ad6f1329ae856bfd6fed5b96abc78eae4a`.

The three paths are exactly the options class, its focused security tests, and
`docs/SUPPORT_ADMIN_MILESTONE_8_SUPPORT_ORIGIN_REPAIR_2026-08-14.md`. That
repair evidence is 5,758 bytes/124 lines with SHA-256
`1394954037a460368324803dddc7018d083f136f55ee8e5ae10dc73ea8f0d4ff`.
All recorded input, blob, delta, and evidence pins reproduced.

## Independent code and boundary review

`IsComplete` still performs every preexisting fail-closed check before the new
comparison: enabled state, Development environment, three exact HTTPS roots,
bounded OIDC values, exact five-minute session lifetime, API timeout bounds,
and response-size bounds. Only a fully valid non-null portal/API pair reaches
`SameOrigin`.

`SameOrigin` creates absolute `Uri` instances from those validated roots and
compares:

- scheme case-insensitively;
- `IdnHost` case-insensitively, preserving canonical IDN host comparison; and
- effective `Port`, for which implicit HTTPS and explicit 443 both resolve to
  443.

The method therefore rejects the proven
`https://support.showvault.test/`/`https://support.showvault.test:443/` alias
and hostname-case aliases. It correctly permits a different hostname or a
different effective port. Exact-root validation continues to deny non-HTTPS,
userinfo, path, query, and fragment variants.

The focused tests exercise two equivalent-origin cases and two genuinely
distinct-origin cases in addition to the existing exact-equality,
Development-only, and complete-configuration assertions. The implementation
is symmetric and introduces no parsing path reachable after failed
validation.

The repair adds no route, authentication or authorization registration,
issuer/audience/scope change, cookie or session change, API contract or client
change, persistence or migration change, response field, log field, package,
lockfile, workflow, configuration enablement, provider call, or native
operation. Checked-in Support remains disabled and non-Development enablement
continues to fail closed.

Code-only added-line scans found no credential/private-key literal, privacy
field, or route/scheme/scope registration. The new URL literals use the
reserved synthetic `.test` namespace.

## Independently repeated local validation

- Focused configuration cases: **5 passed, 0 failed, 0 skipped**.
- Complete Support BFF Release suite: **17 passed, 0 failed, 0 skipped**.
- Complete account portal Release suite: **15 passed, 0 failed, 0 skipped**.
- Complete platform Release suite: **40 passed, 0 failed, 0 skipped**.
- Complete API Release suite: **170 passed, 0 failed, 0 skipped**.
- Support BFF Release build: **0 warnings, 0 errors**.
- Account portal Release build: **0 warnings, 0 errors**.
- API Release build: **0 warnings, 0 errors**.
- EF `migrations has-pending-model-changes`: **no pending model changes**.
- Formatting verification: **passed** for all eight relevant Support, account
  portal, platform, and API source/test projects.
- Commit whitespace, exact path, privacy/secret, and route/scheme/scope
  inventories: **passed**.

The scoped worktree remained clean throughout the read-only review until this
single evidence document was added.

## Fresh remote X4 no-drift review

Connector, raw GitHub API, generated-merge commit, and `git ls-remote`
readbacks agree:

- pull request: https://github.com/ChrisRivera23/ShowVault/pull/40;
- state: open, draft, unmerged, mergeable, and clean;
- exact base and current remote `main`:
  `577bbba00206f9e60a2e3c70d759a34af591106a`;
- exact remote source and PR head:
  `cc27f9ef5fa5c8028ee9d0332fe03d40744b0a81`;
- comparison: diverged, 16 ahead/one behind, 16 commits, 53 paths,
  `+6581/-14`;
- generated merge:
  `a530a455b9c3536b42b781c7f83d774c502f8599`, with ordered parents exact base
  then exact remote source and tree
  `3dc68f7dae304b5ec5bead5e2c70ff15224b7f97`;
- exact title and body remain unchanged;
- issue comments, reviews, and review threads remain empty;
- permission remains admin; auto-merge remains disabled; normal, squash, and
  rebase merge methods remain enabled; and
- automatic push run `31767741253` and pull-request run `31767770175` remain
  completed successfully at the exact remote source head.

The reviewed repair remains intentionally local and beyond the remote source.
No fetch, push, ref/PR/workflow mutation, body proposal, ready transition,
merge, cleanup, deployment, release, provider/identity/production, Keychain,
native, or real-data operation occurred.

## Next gate

A fresh explicit authorization should perform a final read-only repair-
publication preflight from the exact review commit created with this document.
It should verify the complete local candidate and remote fast-forward lease,
reproduce all path/stat/hash and X4 pins, and prepare an exact PR-body proposal
that accurately incorporates successful hosted CI and the approved 17-test
Support repair. Record local evidence and stop before push or PR mutation.
