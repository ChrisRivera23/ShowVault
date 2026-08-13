# ShowVault local-first product bible

This document is the current authority for the customer product direction. It
supersedes older descriptions of direct-to-cloud backup and customer-managed
Venue Agent installation.

## Product promise

ShowVault is a recovery-first venue-resilience product:

**Scan → Backup → Verify → Restore**

The customer desktop journey stays simple:

**Install → Scan this computer → Save locally → Sign in for cloud service**

A detection means only that an exact closed-catalog location exists. A local
recovery point becomes protected only after independent structural and SHA-256
verification. Cloud synchronization is a separate state. No Recovery
Confidence, dependency completeness, license portability, compatibility, or
recoverability claim may be shown without its own recorded evidence.

## Customer boundary

- The Flutter desktop app is the customer interface and native folder-consent
  surface.
- One packaged, venue-neutral .NET local engine performs bounded capture,
  verification, immutable publication, and SQLite queue persistence.
- The packaged host accepts only Save, vault inspection, Restore, and
  in-process Cancel records. It
  is not installed, enrolled, or operated as a customer-facing Agent.
- Scan and Save work while signed out and offline. Network or API failure never
  blocks access to verified local recovery points.
- Installed-application findings are detection-only. Only exact closed-catalog
  `UserDataRoot` findings can be saved.
- Local paths are process-local inputs. They do not enter UI errors, results,
  logs, queue records, or cloud-facing requests.
- Folder grants are explicit and session-scoped. ShowVault creates no persistent
  bookmark or broad filesystem grant.
- Restore is attended, signed-out/offline, freshly verified-point-only, and
  publishes one fixed child into an independently selected empty sandbox. It
  never loads a running application or device.

## Local protection model

ShowVault retains source and destination identities without following links,
enforces topology and byte/time bounds, rejects source/vault overlap and
multiply-linked content, and copies into same-filesystem staging. It verifies
the staged package before a non-overwriting atomic publication, persists
matching independent evidence, reverifies published bytes, and only then
transitions SQLite state from `staging` to `verified` to `queued`.

Failure or cancellation cannot replace a prior recovery point. An unqueued
package published immediately before a failure is moved to `Quarantine`; failed
durable state is surfaced as Queue attention rather than success.

Restore retains the verified package and selected sandbox identities, stages
and rehashes a complete copy, and atomically publishes only `ShowVault Restored
Files`. Durable path-free Restore state and evidence are completed only after
post-publication verification. Ambiguous or unowned content is preserved and
surfaced as Restore attention.

## Honest milestone boundary

Milestone 3 implements controlled local Restore and path-free evidence. It does
not implement an upload executor, application/device loading, dependency
discovery, compatibility
assessment, Recovery Confidence, release distribution, or venue/equipment
proof. The legacy Agent remains an internal compatibility and controlled
recovery subsystem; it is not the customer desktop lifecycle.
