# ShowVault next-chat continuation prompt

Copy everything inside the block below into a new Codex chat.

```text
Continue ShowVault development in:

/Users/infamous/Documents/ChatGPT/showvault

Before planning, researching, editing, or testing, read these files completely:

1. /Users/infamous/Documents/ChatGPT/showvault/CHAT_CONTINUATION_README.md
2. /Users/infamous/Documents/ChatGPT/showvault/README.md
3. /Users/infamous/Documents/ChatGPT/showvault/docs/AUTOMATIC_DISCOVERY.md

Treat the repository documentation, approved contracts, ADRs, tests, migrations, commits, and current implementation as authoritative. Do not rely on conversational memory or repeat completed work unless inspection proves it incomplete.

Product goal

ShowVault is a recovery-first venue-resilience platform. It must tell a venue what is installed, what is protected, whether its backup is usable, and exactly how to recover it:

Scan → Backup → Verify → Restore

The primary discovery goal is to start ShowVault at an unknown nightclub, concert hall, house of worship, or similar venue and recognize supported catalog equipment and software without pre-entered paths, addresses, models, vendors, or computer specifications. Preserve older macOS/Windows compatibility and direct laptop-to-device Ethernet as required test boundaries.

Expected starting state

- Repository: /Users/infamous/Documents/ChatGPT/showvault
- Branch: codex/pixera-catalog-detection
- Latest feature commit: 9282e48 feat: detect versioned PIXERA installations
- Latest handoff: the HEAD documentation commit containing this file
- Worktree clean except the pre-existing untracked NEXT_CONVERSATION.md
- Verified baseline: 2 contract tests, 21 platform tests, 301 Agent tests, 7 API tests, Flutter analysis, and 15 Flutter tests passing
- Changed-file Agent formatting passes; repository-wide Agent formatting reports four pre-existing whitespace findings in unchanged AgentCommandExecutorTests.cs
- EF Core reports no pending model changes

Next bounded objective

Research official Millumin primary sources, then add catalog-driven macOS installed-application and project-root detection only for documented stable standard locations, with synthetic fixtures. If primary sources do not establish dependable paths, record an evidence-backed deferral instead of guessing.

Required boundaries

- Research official Millumin primary sources before defining any application or project location.
- Extend the reusable catalog/provider architecture; do not add another product-specific host scanner.
- Use automated macOS-shaped filesystem fixtures only; do not inspect the Product Owner's installed applications or projects.
- Check only declared standard paths and candidate existence; do not read candidate file contents.
- Keep resolved paths Agent-local and publish only opaque candidate IDs with bounded product/type/evidence metadata.
- Keep installed software, recoverable data, operator approval, validation, protection, verification, and restore state distinct.
- Do not guess undocumented paths or claim Millumin validation, backup, verification, restore, hardware identification, or media support in this detection-only slice.
- Treat `docs/INTEGRATION_CATALOG.md` as the authoritative first prototype testing matrix; implemented products omitted from it remain outside the prototype testing commitment.

Required workflow for every task

1. Read all three authoritative continuation files completely.
2. Inspect the current branch, worktree, recent commits, relevant code, tests, contracts, migrations, and documentation.
3. Preserve unrelated changes and untracked files.
4. Announce one bounded outcome before changing code.
5. Search existing decisions before inventing new ones; research official primary sources autonomously when needed.
6. Create a new codex/ branch for the slice.
7. Implement one small end-to-end vertical slice with automated fixtures.
8. Preserve tenant isolation, authorization, Agent/control-plane separation, correlation, idempotency, evidence, audit, offline behavior, privacy, and failure handling.
9. Run focused tests, then complete relevant regression suites, formatting/static analysis, migration checks when applicable, and git diff --check.
10. Review the final diff.
11. Commit the feature separately with an intentional message.
12. Update README.md and relevant permanent documentation with status, tests, limitations, branch, commits, and the exact next task.
13. Commit the documentation handoff separately.
14. Confirm the final worktree state and list intentionally untouched files.

Communication workflow

- Keep every progress update and final response short and direct, and include an explicitly labeled next step in each one.
- End every user-facing reply with an explicitly labeled honest estimate of conversation-context usage, for example: “Estimated conversation-context usage: ~45% (unmeasured).” Exact usage is not exposed.
- Below approximately 60%, continuing in the current chat is normally fine.
- Around 75–89%, warn the Product Owner and avoid unnecessary scope expansion.
- At approximately 90%, automatically refresh NEXT_CHAT_README.md and the repository handoff, finish and commit the bounded task, warn the Product Owner, and recommend starting a new chat.
- At approximately 95%, automatically refresh NEXT_CHAT_README.md and the repository handoff again even if recently updated, stop starting new work, finish verification and commits, and strongly direct the Product Owner to start a new chat.
- Do not ask the Product Owner to repeat these standing instructions.

Every final response must state:

- What was completed.
- Important safety and architectural boundaries preserved.
- Tests and verification with passing counts.
- Feature and documentation commit hashes.
- Remaining limitations.
- The exact next bounded task.
- Unrelated or untracked files intentionally left untouched.
- Approximate conversation-context usage percentage, explicitly labeled as an estimate.
- Whether to continue, prepare a new chat, or start a new chat now.
```
