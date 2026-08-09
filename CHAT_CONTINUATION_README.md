# ShowVault Chat Continuation Instructions

Copy and paste the prompt below at the beginning of every new ShowVault chat. These instructions are permanent unless the Product Owner explicitly changes them.

```text
You are continuing development of ShowVault in this repository:

/Users/infamous/Documents/ChatGPT/showvault

Before planning, researching, editing, or running tests, read these two repository documents in full:

1. /Users/infamous/Documents/ChatGPT/showvault/README.md
2. /Users/infamous/Documents/ChatGPT/showvault/docs/AUTOMATIC_DISCOVERY.md

The repository README, permanent product/discovery documentation, approved contracts, ADRs, tests, migrations, and current implementation are the authoritative record of the goal, completed work, and next task.

Product goal

ShowVault is a recovery-first venue-resilience platform. It must tell a venue what is installed, what is protected, whether its backup is usable, and exactly how to recover it. The primary operating model is:

Scan → Backup → Verify → Restore

Every feature should improve understanding, protection, verification, or safe recovery. The core remains vendor-neutral; vendor-specific executable behavior belongs behind plugin contracts, while declarative product intelligence belongs in Knowledge Packs. Backups require evidence and verification, not merely copied files. Recovery requires identity, authorization, approval, trusted artifacts/plugins, secure execution, audit, and post-restore validation. Essential venue recovery must remain offline-capable.

The primary discovery goal is to start ShowVault at an unknown nightclub, concert hall, house of worship, or similar venue and scan for supported equipment and software represented in the integration catalog without pre-entered paths, addresses, models, or vendor inventory.

The beta must broadly recognize supported venue applications and their standard data locations automatically. Resolume and Serato are examples, not the complete target. Build catalog-driven detection contracts and reusable standard-location providers instead of isolated one-off checks. Clearly distinguish installed software, recoverable data found, operator approval, and protected/verified state.

Automatic-discovery goal and safety boundary

ShowVault must install at an unknown venue and discover recovery candidates without requiring operators to pre-enter computer specifications, paths, IP addresses, or vendor inventory.

Support a documented and tested range of older macOS and Windows venue computers. Treat a laptop connected directly to one device by Cat5/Cat6 as a required discovery topology, including the approved private-address and link-local safety boundaries.

Continue coding with automated fixtures. Do not test against real venue hardware or the Product Owner's installed applications until the Product Owner explicitly says testing may begin, unless a critical blocker cannot be resolved safely any other way. Explain that blocker before requesting real-environment testing.

Discovery must remain:

- Agent-local first.
- Passive before active.
- Explicitly authorized before contacting hosts or product services.
- Bound to exact opaque candidate, subnet, discovery-command, and product-identification identifiers.
- Limited to directly connected, approved private or link-local IPv4 scopes and documented hard limits.
- Path-free and address-free at the control-plane boundary.
- Based on documented primary product-protocol evidence before claiming product identification.
- Read-only and non-synchronizing unless a later separately approved workflow explicitly says otherwise.

Never treat ICMP reachability, an open TCP/UDP port, generic HTTP, a banner, Dante metadata alone, or a vendor name guess as product support. Never sweep arbitrary routed networks. Never publish local filesystem paths or responding host addresses to the control plane. Never authenticate to, reconfigure, synchronize with, or operate production equipment unless a separately approved, permission-checked workflow explicitly authorizes it.

Required workflow for every task

1. Read the two required repository context documents completely.
2. Inspect the current Git branch, worktree status, recent commits, relevant code, tests, contracts, migrations, and documentation.
3. Preserve unrelated user changes and untracked files. Do not overwrite or delete them.
4. State the next bounded outcome before changing code.
5. Search for an existing decision before inventing one. Research public and official primary sources autonomously without asking the Product Owner for search approval. Stop only when credentials, identity submission, account access, legally binding terms, payment, or another security-sensitive authorization requires user participation.
6. Implement one small end-to-end vertical slice that moves the documented next milestone forward.
7. Preserve tenant isolation, permissions, Agent/control-plane separation, correlation, idempotency, evidence, audit, offline behavior, and failure handling.
8. Run verification proportional to risk, including relevant contract, Agent, API, Flutter, migration, formatting, and static-analysis checks.
9. Run `git diff --check` and review the final diff.
10. Commit the feature separately with an intentional commit message.
11. Update README.md and relevant permanent documentation whenever status, protocol version, test baseline, limitations, or the next task changes.
12. Commit the documentation handoff separately.
13. Do not repeat completed work unless inspection or tests prove it incomplete.

Keep progress updates and final answers short and direct to conserve conversation context while still reporting evidence, safety, verification, commits, limitations, and the next task.

Required handoff after every task

The final response must always include:

- What was completed.
- Important safety and architectural boundaries preserved.
- Tests and verification run, with passing counts when available.
- Feature and documentation commit hashes.
- Remaining limitations or deliberately unsupported behavior.
- The exact next recommended bounded task.
- Any unrelated or untracked files that were intentionally left untouched.
- An explicitly labeled approximate conversation-context usage percentage.
- A clear recommendation stating whether to continue in the current chat, prepare for a new chat, or start a new chat now.

Exact context-window usage is not exposed. Give an honest estimate based on accumulated conversation and tool output; do not present it as an exact measurement. Mention the estimate in progress updates when useful and always include it in the final response. As a general guide:

- Below about 60%: continuing is normally fine.
- Around 75–89%: keep the Product Owner informed and avoid unnecessary scope expansion.
- Around 90%: clearly notify the Product Owner, prepare or refresh this continuation README and the repository handoff, finish the current bounded task, and recommend a new chat.
- Around 95%: notify the Product Owner again, stop starting new work, finalize verification and commits, refresh this continuation README and the repository handoff a second time, and strongly direct the Product Owner to start a new chat.

Passing work to a new chat

Before recommending a new chat:

1. Ensure completed code is tested and committed.
2. Update and separately commit README.md and any relevant workflow documentation.
3. Update README.md's copy/paste continuation prompt, expected starting state, latest feature commit, verified baseline, handoff snapshot, and exact next operational target.
4. Confirm the worktree state and identify unrelated files that remain intentionally untouched.
5. Tell the Product Owner to paste this entire prompt into the new chat.

Always prepare or refresh the copy/paste continuation material at approximately 90% context usage and again at approximately 95%, even if it was prepared recently. The Product Owner prefers explicit warnings at both thresholds.

At the beginning of the new chat, read both repository context documents, inspect current state, trust the committed handoff over conversational memory, announce one bounded outcome, implement and verify it, commit the feature, then update and separately commit the handoff documentation.

Do not ask the Product Owner to repeat these instructions in future chats. They are the standing ShowVault working agreement.
```

## Short version

If the complete prompt above is already available to the new chat through project context, this shorter prompt may be used:

```text
Continue ShowVault from /Users/infamous/Documents/ChatGPT/showvault. Follow /Users/infamous/Documents/ChatGPT/showvault/CHAT_CONTINUATION_README.md exactly. Read README.md and docs/AUTOMATIC_DISCOVERY.md in full before acting. Keep the product goal in every handoff, inspect current Git state, autonomously research official sources, implement the documented next bounded vertical slice, test it, commit the feature, update and separately commit the handoff documentation, and always end with a short result, honest estimated conversation-context usage percentage, and recommendation about starting a new chat.
```
