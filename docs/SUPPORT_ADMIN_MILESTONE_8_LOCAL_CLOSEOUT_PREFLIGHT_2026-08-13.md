# Support Admin milestone 8 local closeout preflight — 2026-08-13

## Verdict

Verdict: **the complete local milestone 8 chain is linear, internally
consistent after two historical hash corrections, and ready for a separately
authorized publication-review gate. Stop before publication or operations.**

The repaired plan's smallest Support slice is complete: a separately
authenticated active `SupportReader` with one explicit organization grant can
request one minimized read-only organization overview through a distinct
disabled-by-default Support BFF, and the API commits append-only access evidence
before disclosure. This closeout changes only this evidence file and the
roadmap reconciliation. It changes no product source, migration, test, or
configuration file.

## Exact input and tree relationship

- Worktree: `/private/tmp/showvault-milestone-8-support-admin-plan`.
- Branch: `codex/milestone-8-support-admin-plan`.
- Reviewed product base:
  `2dfb4cd82b6ca3cf1ef3928f73c8fe00e194b0a5`.
- Product-base tree:
  `fea87b4dc7492a5187dcd60cc618ddff77b067db`.
- Final reviewed head:
  `e1e6db12860dc7140e8738c9787a3fb8c8d5704b`.
- Final reviewed tree:
  `0d9749422612818b9ca155c68df4c615d293e31c`.
- Range: 11 commits, all single-parent and exact linear descendants of the
  reviewed product base.
- Aggregate delta: 46 files, `+5525/-14`.
- Sorted unique-path SHA-256:
  `032e94a69d5c389e62d17d42b21a751f0b493d1a2c20cbf69f22fb68955f09f7`.
- Binary full-index diff SHA-256:
  `b9597d5c2299f6d8b6ad2f209b7b0b2e774e25ae022f126aa72b2b586507858c`.
- Binary path inventory: empty.

The product base retains the exact tree recorded for reviewed PR #39 product
head `2dfb4cd`. The recorded normal-merge commit
`577bbba00206f9e60a2e3c70d759a34af591106a` is not present in this local
object database, so its object was not re-read or fetched. The previously
reviewed relationship remains: that remote-main merge had the same
`fea87b4d` tree as this product base. Local `origin/main` remains the stale
`ffbb3902717fa02c56e7a66b5635f3e7d63981bb`; it was not fetched or mutated.

## Exact linear commit inventory

| Commit | Sole parent | Tree | Delta | Purpose |
| --- | --- | --- | --- | --- |
| `7237f0f` | `2dfb4cd` | `06bd603` | 2 files, `+321/-7` | Plan milestone 8 support administration |
| `095fc81` | `7237f0f` | `a354fd6` | 2 files, `+229/-47` | Review and repair milestone 8 plan |
| `8384557` | `095fc81` | `10a0ea7` | 11 files, `+2331/-6` | Implement staff authority, grants, immutable audit, and migration |
| `fc59c26` | `8384557` | `fe85f6f` | 1 file, `+101/-0` | Approve stage 1 review |
| `990d384` | `fc59c26` | `ba28388` | 11 files, `+802/-1` | Implement Support authentication and overview API |
| `a4106e0` | `990d384` | `47e51ea` | 6 files, `+182/-9` | Repair stage 2 review findings |
| `90f9a8a` | `a4106e0` | `9d624b2` | 3 files, `+133/-13` | Close residual limiter prune/accounting race |
| `9a54607` | `90f9a8a` | `6702354` | 1 file, `+99/-0` | Approve final stage 2 review |
| `be23a1f` | `9a54607` | `fc33ef4` | 17 files, `+1053/-0` | Add isolated Support BFF |
| `82b8b5b` | `be23a1f` | `f4e9b12` | 3 files, `+232/-5` | Suppress handled-exception diagnostics and review stage 3 |
| `e1e6db1` | `82b8b5b` | `0d97494` | 1 file, `+116/-0` | Approve final stage 3 review |

Every stage-1-through-final-review tree, parent, sorted-path hash, and binary
full-index diff hash reproduces exactly. The two planning-era corrections are
recorded below.

## Historical hash corrections

Two binary-diff values repeated in the handoff do not reproduce from the
immutable commit objects. Four equivalent commands—commit-to-commit `git diff`,
`git diff` with `--no-ext-diff --no-textconv`, patch-only `git show`, and
`git diff-tree -p`—produce the same corrected value in each case.

- Plan commit `7237f0f`: recorded
  `d6a9f71f751dceeb1566e87ae1d522782f40ff8d8a0887201ee68ae67ce5a4df`;
  reproducible binary full-index diff SHA-256
  `0d61fa05600864d87c6434aadd8fdbf31fcc205e18b5c7d4b567a081a1a86362`.
- Plan-review commit `095fc81`: recorded
  `0c5213e0c39032687bbc19ee2c1b1ce6b5f5f7ec9f144c89668d670e22e175a4`;
  reproducible binary full-index diff SHA-256
  `20b25f8b2cf408fb2e4fa4b4b08cb20ba48620e6e6f1c93f580de60462d32644`.

Their commit, parent, tree, delta, and sorted-path hashes are correct. These are
documentation-only hash corrections; they do not identify product drift. The
historical evidence documents remain unchanged, while this closeout is the
authoritative correction for later range review.

## Path and statistics inventory

The 46 unique paths separate cleanly into 34 product/migration/test/
configuration paths (`+4041/-7`) and 12 documentation paths (`+1484/-7`).

Product sorted-path SHA-256:
`97fd3fae118f80a32be818c46e12fa9554620bbdeeb523aefef655ebaadf3959`.
Product binary full-index diff SHA-256:
`47359e2d6c65afd37416ac6a0a906e211618e8378791f7c1681df7c199706d1d`.

### Product, migration, test, and configuration paths (34)

```text
apps/support_admin/src/ShowVault.SupportAdmin/Clients/ShowVaultSupportClient.cs
apps/support_admin/src/ShowVault.SupportAdmin/Clients/SupportApiModels.cs
apps/support_admin/src/ShowVault.SupportAdmin/Configuration/SupportAdminPortalOptions.cs
apps/support_admin/src/ShowVault.SupportAdmin/Pages/Index.cshtml
apps/support_admin/src/ShowVault.SupportAdmin/Pages/Index.cshtml.cs
apps/support_admin/src/ShowVault.SupportAdmin/Pages/Shared/_Layout.cshtml
apps/support_admin/src/ShowVault.SupportAdmin/Pages/_ViewImports.cshtml
apps/support_admin/src/ShowVault.SupportAdmin/Pages/_ViewStart.cshtml
apps/support_admin/src/ShowVault.SupportAdmin/Program.cs
apps/support_admin/src/ShowVault.SupportAdmin/Security/SupportOriginMiddleware.cs
apps/support_admin/src/ShowVault.SupportAdmin/Security/SupportSecurityHeadersMiddleware.cs
apps/support_admin/src/ShowVault.SupportAdmin/Security/SupportServerSideTicketStore.cs
apps/support_admin/src/ShowVault.SupportAdmin/ShowVault.SupportAdmin.csproj
apps/support_admin/src/ShowVault.SupportAdmin/appsettings.json
apps/support_admin/tests/ShowVault.SupportAdmin.Tests/ShowVault.SupportAdmin.Tests.csproj
apps/support_admin/tests/ShowVault.SupportAdmin.Tests/SupportAdminSecurityTests.cs
services/api/src/ShowVault.Api/Contracts/SupportContracts.cs
services/api/src/ShowVault.Api/Data/Migrations/20260814010158_AddSupportAdministrationFoundation.Designer.cs
services/api/src/ShowVault.Api/Data/Migrations/20260814010158_AddSupportAdministrationFoundation.cs
services/api/src/ShowVault.Api/Data/Migrations/PlatformDbContextModelSnapshot.cs
services/api/src/ShowVault.Api/Data/PlatformDbContext.cs
services/api/src/ShowVault.Api/Endpoints/SupportEndpoints.cs
services/api/src/ShowVault.Api/Program.cs
services/api/src/ShowVault.Api/Support/SupportAdminOptions.cs
services/api/src/ShowVault.Api/Support/SupportAuthorizationService.cs
services/api/src/ShowVault.Api/Support/SupportOrganizationOverviewService.cs
services/api/src/ShowVault.Api/Support/SupportRequestRateLimiter.cs
services/api/src/ShowVault.Api/Support/SupportStepUpAuthorization.cs
services/api/src/ShowVault.Api/appsettings.json
services/api/tests/ShowVault.Api.Tests/SupportAdministrationPersistenceTests.cs
services/api/tests/ShowVault.Api.Tests/SupportStage2Tests.cs
services/api/tests/ShowVault.Api.Tests/TenantApiFactory.cs
services/platform/src/ShowVault.Platform/Support/SupportAdministration.cs
services/platform/tests/ShowVault.Platform.Tests/SupportAdministrationFoundationTests.cs
```

Documentation sorted-path SHA-256:
`a3a2818d3ac629df4a1e51de7d580a9ee276e2d37b2854a4255d84f9c3c27e94`.
Documentation binary full-index diff SHA-256:
`82e7426e5b1083529775c86d97611bc08f1ecdd14749819abeff58aa2b0caa82`.

### Documentation paths (12)

```text
docs/ROADMAP.md
docs/SUPPORT_ADMIN_MILESTONE_8_ADVERSARIAL_REVIEW_2026-08-13.md
docs/SUPPORT_ADMIN_MILESTONE_8_EXTRACTION_AND_PLAN_2026-08-13.md
docs/SUPPORT_ADMIN_MILESTONE_8_STAGE_1_ADVERSARIAL_REVIEW_2026-08-13.md
docs/SUPPORT_ADMIN_MILESTONE_8_STAGE_1_IMPLEMENTATION_EVIDENCE_2026-08-13.md
docs/SUPPORT_ADMIN_MILESTONE_8_STAGE_2_ADVERSARIAL_REVIEW_2026-08-13.md
docs/SUPPORT_ADMIN_MILESTONE_8_STAGE_2_FINAL_REVIEW_2026-08-13.md
docs/SUPPORT_ADMIN_MILESTONE_8_STAGE_2_IMPLEMENTATION_EVIDENCE_2026-08-13.md
docs/SUPPORT_ADMIN_MILESTONE_8_STAGE_2_REPAIR_REVIEW_2026-08-13.md
docs/SUPPORT_ADMIN_MILESTONE_8_STAGE_3_ADVERSARIAL_REVIEW_2026-08-13.md
docs/SUPPORT_ADMIN_MILESTONE_8_STAGE_3_FINAL_REVIEW_2026-08-13.md
docs/SUPPORT_ADMIN_MILESTONE_8_STAGE_3_IMPLEMENTATION_EVIDENCE_2026-08-13.md
```

## Repaired-plan and roadmap reconciliation

The implemented range closes all four repaired-plan stages:

1. closed staff assignment/grant authority, restrictive persistence,
   append-only audit, one generated migration, and disabled API options;
2. exact distinct Support bearer scheme, fresh scope/MFA authorization,
   bounded direct-peer limiter, strict 4-KiB POST, uniform joined target/grant
   resolution, fixed minimized projection, and serializable audit-before-
   disclosure;
3. a separate server-rendered BFF with exact origin, Code + PKCE, isolated
   cookies and session namespace, antiforgery, strict typed API validation,
   same-response rendering, generic bounded errors, and no result persistence;
   and
4. implementation, adversarial-repair, final-review, validation, and evidence
   gates for every stage.

The completed range retains exactly one Support API route and one Support BFF
page. It adds no staff-provisioning surface, organization search/list/export,
impersonation, write action, customer-route authority, provider lookup,
payment detail, secret access, or backup/path/content access. Checked-in API
configuration remains `Enabled: false` with null Support authority/audience;
checked-in BFF configuration remains `Enabled: false`. Enabled non-Development
BFF startup remains fail-closed because the implemented session store is
Development-only.

The roadmap's former Support-administration `Next` item is therefore moved to
`Completed locally`, explicitly retaining the independent production gates.
Production hosted-object durability and native/equipment proof remain the next
product objectives.

## Validation authority

The exact final review already independently passed the focused handled-
exception proof, 13/13 Support BFF tests, 15/15 account-portal tests, 40/40
platform tests, 170/170 API tests, three zero-warning Release builds,
formatting, EF no-drift, and boundary inventories. This closeout did not rerun
those unchanged suites. It independently passed exact ancestry/ref/tree/hash
extraction, committed-range `git diff --check`, disabled-configuration and
route/startup inventory, and clean scoped-worktree checks using local objects
only.

## Remaining independent gates

Completion of the local implementation does not make it operational. Every
item below requires its own reviewed plan and explicit authorization.

### Publication and integration

- A fresh no-drift publication preflight must pin the exact local candidate,
  intended remote base, complete range, title/body, permissions, policies,
  feedback, and expected CI behavior.
- Branch publication, pull-request creation or update, workflow execution,
  review disposition, ready transition, and merge are separate mutations.
- Any publication candidate must include this closeout/roadmap commit without
  changing the reviewed product range, and must revalidate the corrected
  planning hashes.

### Durable sessions

- Replace the bounded Development-only in-memory ticket store with a reviewed
  durable/distributed encrypted server-side store.
- Freeze encryption/key rotation, five-minute expiry, capacity, revocation,
  logout/removal, multi-instance consistency, outage/fail-closed behavior,
  retention, backup, and disaster-recovery semantics.

### Support identity and provisioning

- Create a separate Support identity application/client and exact distinct API
  audience, with the exact read scope, Code + PKCE, MFA challenge, issuer, and
  callback/logout origins.
- Define synthetic-only staff assignment and organization-grant provisioning,
  suspension/revocation, separation of duties, access review, and break-glass
  policy before any real-person onboarding.
- Prove token claims, key rotation, session revocation, assignment/grant races,
  and denial behavior against a non-production identity tenant first.

### Production, deployment, and operations

- Review and apply the database migration with backup/rollback and compatible
  deployment ordering; configure secrets without checking them in.
- Review deployment topology, DNS, TLS, trusted-proxy policy if any, network
  boundaries, database permissions, health checks, and zero-downtime behavior.
- Establish privacy-safe monitoring and alerts, bounded security telemetry,
  append-only audit retention/access/export policy, clock synchronization,
  limiter behavior across instances, incident response, and periodic staff/
  grant recertification and revocation drills.
- Complete production safety, threat-model, privacy/legal/retention, load,
  availability, recovery, and rollback review before enablement. Use no real
  staff, customer, venue, organization, payment, path, or backup-content data
  during pre-production proof.

### Later capabilities

Organization directories, search/list/export, impersonation, staff
provisioning UI, any Support write action, provider/dashboard access, billing
or subscription mutation, refund/dispute action, quota change, identity reset,
backup/path/content inspection, download, deletion, and retention/legal-hold
features remain outside milestone 8 and independently unauthorized.

## Stop boundary

Stop after one documentation-only local closeout commit. Do not publish or
mutate a branch, pull request, ref, workflow, identity provider, Stripe or
other provider, production system, deployment, release, Keychain value, native
package, or real data. Do not fetch, force-push, revert, rerun workflows, or
clean/delete branches or worktrees.
