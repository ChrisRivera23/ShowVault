# ShowVault active continuation handoff

Read this file and
`docs/LOCAL_FIRST_MILESTONE_1_IMPLEMENTATION_2026-08-13.md` before continuing
work from this branch.

## Current checkpoint — 2026-08-13

- Branch: `codex/local-first-milestone-1`
- Worktree: `/private/tmp/showvault-local-first-m1/worktree`
- Exact foundation: `32c21cfbd51ea5f16bb5fe84c56f4efb125b1df4`
- Implementation commits: `17d4410`, `323fe7c`, `ffdd40b`, `805a96c`
- Evidence commit: `2058aba2e2682c66e53af43c784138116dec48df`
- Product outcome: **Install → Scan this computer → Sign in for cloud service**

Milestone 1 is complete locally. Direct Scan checks only exact catalog
candidates, keeps paths transient, submits only opaque server-allowlisted keys,
stores empty scans, returns only the newest direct detections, and keeps those
detections outside Agent decision/backup/verification/restore controls. The
guarded personal beta requires all client, origin, server-environment,
server-flag, identity, and remote-loopback conditions.

Validation passed: Flutter analysis and 21 tests; contracts 22; platform 15;
Agent 291; API 19; EF no-pending-model gate; zero-warning API Release build;
format, shell syntax, packaging negative guards, privacy/security review, and
diff checks.

## Authorization boundary

No external or native action is authorized by this checkpoint. Do not fetch,
push, create or mutate a PR, dispatch a workflow, retrieve artifacts, build or
install a meaningful native package, use equipment, access personal/customer/
venue data, use cloud resources, release, deploy, or clean up destructively
without new explicit authorization.

No native-platform proof is claimed. macOS/Windows build, signing,
notarization, installation, protocol activation, Gatekeeper, personal-Keychain,
and end-to-end login remain unproven.

## Next gated decision

Stop for Product Owner direction. The likely next product slice is local-first
milestone 2 (local Save, immutable recovery points, manifests, verification
evidence, and the durable upload queue), but no milestone-2 extraction contract
or implementation authorization exists on this branch yet.
