# Local authorization readiness matrix

## Purpose and authority

This matrix is the local decision aid for determining whether the next ShowVault
operation may proceed. It consolidates the active handoff and integration
checklist; it does not grant authorization, replace an explicit Product Owner
instruction, or claim current remote state.

The default is **stop before the operation** when the exact target, scope,
prerequisite, or approval is absent or ambiguous. Authorization for one row does
not authorize any later row.

From `apps/showvault_app`, verify the closed operation set and current local-only
decision before relying on this matrix:

```bash
dart run tool/verify_local_authorization_readiness.dart
```

The verifier reads only this bounded regular local file and emits no operation
details, paths, or inferred permission. It fails if an operation or approval is
removed, reordered, weakened, or if the locally ready set expands beyond L1/L2.

## Current readiness snapshot

| Track | Current state | Safe next action | Blocking gate |
| --- | --- | --- | --- |
| Local planning verification | Ready | Run both local Git preflights, inspect source/docs, run non-destructive tests, and create scoped local commits requested by the Product Owner | None while work stays local and non-destructive |
| PR #3–#24 foundation | Blocked before live inspection | Preserve the local 22-ref ledger and review its static results | Explicit remote-state review authorization, then current ref/PR/CI revalidation |
| Six product milestones | Blocked before implementation | Review manifests locally | PR #3–#24 integrated on `main`, followed by the exact predecessor base for each milestone |
| Provenance-protected source | Locally prepared, blocked before publication | Continue local verification and preserve the candidate head | Explicit authorization for the exact branch/head push |
| Windows evidence bridge | Blocked before refresh/publication | Inspect the preparer/verifier and current local workflow | Exact published green source SHA, then explicit bridge update/push authorization |
| Manual Windows CI evidence | Blocked | Review the local workflow and evidence verifiers | Reviewed bridge merged to default branch, then separate one-run dispatch authorization |
| Controlled Windows evidence | Blocked | Review the equipment runbook locally | Explicit equipment authorization plus scoped execution approval for the exact synthetic proof |
| Installed/Docker/cloud proof | Blocked unless separately requested | Run source-level and unit checks only | Exact scoped local-execution or external-resource approval |
| Personal or venue use | Blocked | Use synthetic fixtures only | New explicit data, equipment, network, and venue-scope authorization |
| Destructive cleanup/rollback | Blocked | Preserve artifacts and external vaults | Exact destructive target and separate destructive approval |

## Operation-to-approval matrix

| ID | Operation | Minimum prerequisite | Required explicit authorization | Mandatory stop after completion |
| --- | --- | --- | --- | --- |
| L1 | Read local files/Git objects and run both bounded preflights | Existing local worktree and refs | None | Stop if a ref, count, SHA, path total, ancestry edge, or boundary differs |
| L2 | Edit source/docs, run non-destructive tests/analyzers/builds, create local commits | Product Owner requested the local task; valid local base; unrelated work preserved | No additional approval within the requested scope | Stop before push, PR mutation, installed proof, external resource use, or destructive cleanup |
| L3 | Create a local branch/worktree for a reconstruction milestone | Required predecessor exists on the intended local `main` | Product Owner request for that implementation slice | Stop if the integrated predecessor/base is absent or stale |
| X1 | Fetch or otherwise inspect current remote PR/default-branch state | Static ledger/preflights pass | Remote-state review authorization for the named repository and PR range | Record observed SHAs; stop on any mismatch before mutation |
| X2 | Push the current provenance-protected product branch | Exact local branch and full head SHA are identified; local checks pass | Push authorization naming that branch/head | Wait for current CI; do not update another PR or mark ready/merge |
| X3 | Create, update, retarget, or force a PR-visible branch | Live base/head revalidated; resulting diff reviewed locally | PR mutation authorization naming the PR/branch and operation | Stop for review and current CI before ready/merge request |
| X4 | Mark ready or merge a PR | Exact reviewed diff, current green checks, approvals, and mergeability | Separate ready/merge authorization for that PR revision | Record resulting `main` SHA; do not start the dependent external stage automatically |
| X5 | Dispatch the Windows workflow | Reviewed bridge exists on default branch and pins the approved immutable green source | Separate authorization for exactly one manual run | Do not rerun on failure; preserve evidence and request new authorization |
| X6 | Download/attest the named run artifact | Authorized run completed successfully; absent bounded output directory | Authorization covering run-artifact retrieval/verification | Verify provenance/checksums; do not claim attended or clean-machine readiness |
| W1 | Use controlled Windows build/test equipment | Equipment identity/scope is approved; synthetic plan and ownership-marked paths are ready | Equipment authorization and scoped execution approval | Stop before attended UX, different equipment, broader cleanup, or personal/venue access |
| W2 | Perform attended picker/Auth0/installed Windows proof | W1 passes; callback precheck is clean; exact attended scope is documented | Separate attended-equipment authorization | Record bounded evidence and limitations; do not infer venue readiness |
| M1 | Run an installed macOS harness or disposable Docker stack | Exact fixture, owned directories/volumes, cleanup plan, and resource limits are defined | Scoped local execution approval | Remove only ownership-marked disposable state; retain external vaults |
| C1 | Create/use cloud buckets, IAM, deployments, releases, paid services, or production providers | Provider, account, region, cost, retention, credentials, and cleanup are specified | External-resource/deployment authorization | Stop before expansion, deletion, rollback, or production-data use |
| V1 | Access personal data or LIV nightclub equipment/network/data | Exact people, equipment, data, time window, and privacy plan are named | New explicit personal/venue authorization | Do not reuse authorization for another device, dataset, network, or visit |
| D1 | Delete an object prefix, external vault, production resource, evidence, or non-marker-scoped data | Exact target is resolved read-only; recovery/retention impact is documented | Separate destructive approval naming the exact target | Report what was removed and whether recovery remains possible |

## Dependency sequence

The product and native-evidence tracks have separate approval chains:

```text
Product: X1 review PRs #3–#24 -> X3/X4 integrate each in order
         -> L3 milestone 1 -> X2/X3/X4 -> ... -> milestone 6

Native:  X2 publish exact protected source -> green CI
         -> X3 refresh bridge -> X4 merge bridge
         -> X5 one dispatch -> X6 attest artifact

Attended Windows: W1 controlled equipment -> W2 attended proof
```

No arrow is implicit authorization. Each external node needs its own approval,
and a failed or changed node invalidates dependent approvals until re-reviewed.

## Approval record

Before an authorized operation, record only bounded operational facts:

```text
Matrix ID:
Exact operation:
Repository/branch/PR/run/equipment target:
Approved SHA or revision where applicable:
Scope and exclusions:
Approval received:
Prerequisites verified:
Required stop point:
Result and limitations:
Next approval required:
```

Never record credentials, tokens, personal paths, host identity, customer
content, or venue topology. Preserve `NEXT_CONVERSATION.md` as intentionally
untracked local handoff material.

## Current decision

Only L1 and requested, bounded L2 work are currently ready. The next product
external gate is X1 for PR #3–#24 live-state review. The next native-evidence
external gate is X2 for the exact provenance-protected source branch/head. No
fetch, push, PR mutation, merge, dispatch, artifact retrieval, installed proof,
equipment use, cloud action, venue access, or destructive cleanup is authorized
by this matrix.
