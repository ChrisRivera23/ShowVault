# Milestone 2 bounded reconstruction review — 2026-08-13

## Result

The historical local Save/vault slice was reviewed locally from immutable Git
objects. Its disposition is **replace/narrow**, not replay. The controlling
contract is `docs/LOCAL_FIRST_MILESTONE_2_EXTRACTION.md`.

The source boundary is exact `ce5be25..c172e49`: six commits, 36 net paths,
`+2,677/-220`, 12 milestone-1 overlaps, binary diff SHA-256
`8159a89c6ec60da7637c763833937245a51bb1b8dde7a166aeb74b850ad3f9c1`,
and path-list SHA-256
`efa196632c912c61d674a71a2bfc592880d7fa674b6b3eaf11a2a6fe7d800daa`.

## Retained product evidence

- configurable `ShowVault Pro` vault layout;
- explicit exact-source and independent vault consent;
- Save/Cancel, same-filesystem staging, immutable named recovery points,
  manifest copies, local verification, verified-only queue intent, and vault
  reopening;
- signed-out/offline operation and independent local/cloud status; and
- one macOS/Windows Flutter access contract with no persistent bookmark.

## Blocking historical behaviors

Read-only source inspection found pathname reopen races, missing source/vault
separation, linked vault-component escape, queue-after-manifest-failure,
absolute package paths in queue JSON, incomplete rehydration verification, and
duplicated Flutter/Agent local-engine behavior. These are architectural
blockers, not documentation nits.

The replacement uses one packaged .NET local engine, retained no-follow
filesystem identities, a transactionally durable SQLite state machine,
path-free manifest/queue identities, full pre-queue re-verification, and a thin
Flutter consent/status surface.

## Authorization record

The Product Owner authorized the next task after milestone 1. Per the prior
handoff, this task was interpreted as the bounded milestone-2
extraction/architecture decision required before implementation. No milestone-2
product source was implemented. No external or native action occurred.
