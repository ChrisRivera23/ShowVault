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
- The packaged host accepts only Save, Cancel, and vault inspection records. It
  is not installed, enrolled, or operated as a customer-facing Agent.
- Scan and Save work while signed out and offline. Network or API failure never
  blocks access to verified local recovery points.
- Installed-application findings are detection-only. Only exact closed-catalog
  `UserDataRoot` findings can be saved.
- Local paths are process-local inputs. They do not enter UI errors, results,
  logs, queue records, or cloud-facing requests.
- Folder grants are explicit and session-scoped. ShowVault creates no persistent
  bookmark or broad filesystem grant.

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

## Honest milestone boundary

Milestone 2 implements local Save and verified-queue creation. It does not
implement an upload executor, restore UI, dependency discovery, compatibility
assessment, Recovery Confidence, release distribution, or venue/equipment
proof. The legacy Agent remains an internal compatibility and controlled
recovery subsystem; it is not the customer desktop lifecycle.
