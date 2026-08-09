# ShowVault active continuation handoff

Read this file completely at the start of a new ShowVault development chat. It is the concise authority for the current state and next task.

Do not automatically read `README.md` or other long records in full. Use the reference map below to inspect only the sections relevant to the active task. Repository contracts, ADRs, tests, migrations, commits, and implementation remain authoritative when they are more specific than this summary.

## Product goal

ShowVault is a recovery-first venue-resilience platform:

**Scan → Backup → Verify → Restore**

It must recognize supported venue equipment and software at an unknown venue without pre-entered paths, addresses, models, vendors, or computer specifications. Preserve documented older macOS/Windows compatibility and direct laptop-to-device Ethernet as product boundaries.

## Current repository state

- Repository: `/Users/infamous/Documents/ChatGPT/showvault`
- Branch: `codex/sony-broadcast-identification`
- Latest completed research decision: `5ebca61 docs: defer unsafe Sony broadcast discovery`
- Latest product handoff: the HEAD documentation commit containing this file
- Expected worktree: clean except intentionally untracked `NEXT_CONVERSATION.md`
- Verified baseline: 7 focused Blackmagic Videohub protocol fixtures and all 336 Agent tests pass; Agent Release build passes with 0 warnings and 0 errors
- The complete baseline also has 2 contract tests, 22 platform tests, 7 API tests, Flutter analysis, and 16 Flutter tests passing; API Release build passes with 0 warnings and 0 errors, and EF Core reports no pending model changes
- Blackmagic's official protocol PDF resolves with HTTP 206, was rendered, and its complete zero-byte initial status example was inspected
- `git diff --check` passes; migration `20260809101018_AddBlackmagicVideohubIdentificationResults` exactly matches the current model
- One full parallel Agent run had a transient failure in the duplicate-field TCP fixture; the isolated fixture passed immediately, the full 336-test Agent suite passed on rerun, and the complete 7-fixture Blackmagic class passed again after the final validation change
- Sony projector research rendered the official common protocol manual and inspected its relevant PJLink and SDAP pages; `git diff --check` passed, and no implementation, test, contract, API, migration, or control-plane schema changed
- During Sony projector research, Sony's CDN returned HTTP 403 to a command-line range request even though the official PDF remained readable through the indexed web document; source validation used the rendered primary-source pages rather than treating the CDN response as missing evidence
- Runtime tests were not rerun for the Sony projector decision because only documentation changed
- Sony broadcast-device research inspected the official LMD-1951MD SDCP/SDAP protocol and XVS-9000 product material. The published bounded TCP protocol has no exact model-identity exchange, while the identity-bearing alternative is a privacy-bearing periodic UDP broadcast; XVS material documents NMOS and optional SNMP capability without an exact bounded product-identity contract
- `git diff --check` passes for the Sony broadcast decision. Both official Sony URLs returned HTTP 403 to command-line requests while remaining readable through Sony's indexed web documents; no runtime tests were rerun because only documentation changed
- Digital Projection was a documentation-only research decision: `git diff --check` passed and both official source links resolved; runtime tests were not rerun because no implementation, test, contract, migration, or build input changed
- Repository-wide Agent formatting has four pre-existing whitespace findings in unchanged assertions in `AgentCommandExecutorTests.cs` at lines 997, 1003, 1009, and 1015

## Current discovery position

- Local application discovery is catalog-driven and checks candidate existence only.
- Resolved paths remain Agent-local. The control plane receives opaque candidate IDs and bounded product/type/evidence metadata.
- Installed, recoverable-data, approved, validated, protected, verified, and restored states remain distinct.
- Existing local catalog coverage includes Resolume, supported DJ applications, disguise Designer, WATCHOUT, Hippotizer, PIXERA, Christie Pandoras Box, TouchDesigner, MadMapper 6, Isadora 4, and bounded Engine OS removable roots.
- HeavyM, Millumin, and Ventuz automatic detection are deferred because official primary sources do not publish dependable standard application/project roots. Ventana is separately deferred because the catalog label does not resolve to a unique professional playback product.
- Real venue hardware, installed applications, projects, and removable media remain uninspected unless the Product Owner explicitly authorizes testing.
- Protocol 1.13 adds a manager-authorized generic projector endpoint and bounded protocol probes. It identifies exact official Christie LX41/LW41, Panasonic PT-DZ770/PT-VW431DEA/PT-RZ470/PT-RW430, Epson QB1000B/QB1000W, and NEC NP-PH3501QL/NP-PH2601QL/NP-PX2000UL/NP-PX2201UL signatures; addresses and raw responses remain Agent-local. Historical PJLink-named Agent storage remains in use for all projector families, and projector-specific completion persistence plus a dashboard action remain unimplemented.
- Barco PJLink identification is deferred because official documentation does not publish literal manufacturer/model response strings and advises against disabling authentication; generic PJLink support is not treated as Barco identity.
- Epson's official QB1000 PJLink documentation publishes exact manufacturer response `EPSON` and model responses `EPSON QB1000B` or `EPSON QB1000W`. Only those two case-sensitive pairs are allowlisted; other Epson models and guessed casing remain safe false negatives.
- Digital Projection identification is deferred. Its official E-Vision 8000i/10000i control workbook gives only `<string>` for the read-only `model.name ?` response, while its UDP discovery example broadcasts privacy-bearing fields and names an unrelated `HIGHLite 660`; neither establishes a target-bounded exact signature for the covered models.
- NEC identification extends the same manager-authorized projector operation with the fixed read-only Base Model Type request on TCP 7142. Exact checksummed signatures identify only NP-PH3501QL, NP-PH2601QL, NP-PX2000UL, or NP-PX2201UL. The NEC and PJLink probes run concurrently within the existing 100–500 ms per-host timeout against the same maximum 32 authorized responders; no broadcast or separate discovery path is added.
- Sony projector model identification is deferred. Sony's official common protocol manual fixes the normal PJLink `INF1` response as `SONY`, leaves `INF2` as an unspecified model name, and enables PJLink authentication by default. Its alternative SDAP identity service periodically broadcasts product name, serial number, location, community, and power status. No arbitrary model acceptance, authentication weakening, broadcast listener, scanner change, real-device contact, or support credit was added.
- Protocol 1.14 adds separately authorized Blackmagic Smart Videohub 16x16 identification against the responders retained by one exact completed discovery. The Agent connects to TCP 9990, sends zero bytes, reads at most 4,096 bytes with a 100-500 ms per-host timeout, and requires the official version 2.3 preamble plus exact model and 16x16 capacity fields. At most 32 hosts are attempted. Addresses remain Agent-local; only counts and `Blackmagic Smart Videohub 16x16` reach the control plane.
- Blackmagic Videohub state has independent tenant-scoped pending/completed/failed persistence, exact command/discovery correlation, an owner-authorized API endpoint, and a native dashboard action. Other Videohub models, HyperDeck, ATEM hardware, generic port reachability, control, configuration, backup, verification, and restore remain unsupported.
- The pre-existing `showvault.blackmagic-atem` plugin still validates only ATEM XML state beneath operator-configured local roots and is not network identity evidence.
- Sony broadcast-device automatic identification is deferred. The official LMD-1951MD SDCP transport exposes target-bounded monitor status/control but no exact model query and literal response; SDAP broadcasts product name with serial, location, community, power, and network fields. Official XVS-9000 material identifies NMOS and optional SNMP support without publishing an exact bounded identity exchange. Projector and PTZ/camera evidence is not reused, and no generic-protocol inference, broadcast listener, scanner, implementation, real-hardware contact, or support credit was added.
- `docs/INTEGRATION_CATALOG.md` is the authoritative first-prototype testing matrix.

## Next bounded objective

Research official NewTek primary sources for an exact, read-only network identity contract for the broadcast-device catalog. Extend an existing manager-authorized bounded responder architecture only if a documented signature fits its authorization and privacy boundary. If primary sources do not establish a safe identity contract, record an evidence-backed deferral.

Required boundaries:

- Start from the NewTek row under `Broadcast` in `docs/INTEGRATION_CATALOG.md`; NDI protocol participation alone does not establish an exact NewTek product identity.
- Reuse an existing manager-authorized bounded responder set only when its prior authorization covers the documented protocol; do not add an unbounded NewTek or NDI scanner.
- Require a documented read-only request and exact response signature; generic reachability, HTTP titles, open ports, mDNS, multicast/broadcast advertisements, or mutable control endpoints alone are insufficient.
- Keep addresses, interface details, and raw responses Agent-local; publish only the existing bounded path-free identification metadata.
- Use synthetic protocol fixtures for any implementation. Do not contact real NewTek broadcast hardware without explicit Product Owner authorization or claim configuration, backup, verification, or restore support in this identification-only slice.

## Required workflow

1. Inspect Git status, recent commits, and task-relevant code, tests, contracts, migrations, and documentation.
2. Preserve unrelated changes and untracked files.
3. Announce one bounded outcome before changing files.
4. Research official primary sources autonomously; use Chrome only when the Product Owner or current task explicitly requires Chrome.
5. Create a new `codex/` branch for the slice.
6. Implement the smallest safe vertical slice or document the evidence-backed deferral.
7. Run focused verification and the relevant regression baseline. Avoid noisy output when a quiet equivalent is sufficient.
8. Review the final diff for privacy, bounded behavior, tenant isolation, and accidental file-content or network access.
9. Commit the feature or research decision separately.
10. Update this file with current branch, commits, verification, limitations, and the exact next task; commit that handoff separately.

## Communication and context policy

- Give concise progress updates only at meaningful milestones, each with an explicit next step.
- Exact context-window usage is unavailable. Do not invent escalating percentages or end a healthy chat solely because of an estimate.
- In the final response, state that context is unmeasured and recommend a new chat only when the platform signals pressure, automatic compaction has materially degraded working state, or the thread has become demonstrably unwieldy.
- Let automatic compaction preserve continuity when it occurs. Refresh this handoff once per completed bounded task, not repeatedly at guessed thresholds.
- Final responses must report outcome, safety boundaries, exact verification, feature/research and handoff commits, limitations, next task, and intentionally untouched files.

## Reference map

Consult these only as needed:

- `README.md` — long-form product architecture, completed milestones, and historical branch ledger.
- `docs/AUTOMATIC_DISCOVERY.md` — detailed discovery design, protocol behavior, and official-source decisions.
- `docs/INTEGRATION_CATALOG.md` — authoritative prototype product/testing matrix.
- `docs/adr/` — approved architectural decisions.
- `services/contracts/` — Agent/control-plane protocol authority.
- Relevant implementation, tests, migrations, and Git history — most specific behavioral authority.
