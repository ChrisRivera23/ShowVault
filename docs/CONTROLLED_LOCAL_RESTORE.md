# Controlled local Restore

## Customer desktop flow

1. Select **Open local vault** and choose an existing ShowVault Pro vault.
2. The packaged local engine independently reverifies every available recovery
   point from immutable package bytes and independent evidence.
3. Select **Restore** on one freshly verified point.
4. Confirm that Restore copies files only; it does not load a running
   application or device.
5. Select an existing empty sandbox with the native directory picker.
6. ShowVault reports bounded, path-free progress. **Cancel Restore** remains
   available through the final pre-publication boundary.
7. Success appears as **Restored locally** only after the published copy is
   rehashed and durable path-free evidence is committed.

Restore works while signed out and offline. It does not upload, contact the
control plane, require a cloud receipt, calculate Recovery Confidence, replace
application data, or invoke vendor software.

## Sandbox and publication contract

The selected sandbox must already exist and be empty. It must be a regular,
unlinked directory with a retained parent identity, on its parent's filesystem,
and must neither equal, contain, nor be contained by the selected vault.

The engine creates one deterministic hidden stage for the recovery-point ID.
Its bounded `intent.json` contains only format version, full recovery-point ID,
and the fixed publication child `ShowVault Restored Files`. Files are copied
from retained package handles into a retained `restored/` tree. Every file,
directory, byte, size, SHA-256 value, link count, topology, source identity,
target identity, and operation bound is checked again before publication.

Publication is one non-overwriting same-filesystem rename of `restored/` to the
fixed child. The selected sandbox itself is never removed or replaced.
Cancellation after publication becomes bounded finalization: ShowVault either
records verified durable success or rolls back only a completely revalidated
owned child. Any unknown, linked, swapped, malformed, or late entry is
preserved and surfaced as Restore attention.

## Evidence and restart behavior

`Upload Queue/local-engine.db` also contains a path-free Restore state machine:

```text
staging → published → verified → completed
                    ↘ failed
staging → cancelled
```

No target, source, or vault path is stored. A completed result contains only
the recovery-point ID, Restore evidence ID, counts, bytes, completion time, and
closed local status. Evidence under `Reports/Restores/<evidence ID>.json` binds
the manifest digest and restored counts without claiming application loading,
dependency completeness, compatibility, license transfer, production
readiness, or Recovery Confidence.

After interruption, reselect the same sandbox. A matching retained intent may
authorize bounded partial-stage cleanup or full verification of the fixed
published child. Unknown content is never adopted or overwritten. Matching
completed bytes, evidence, and SQLite state are returned idempotently.

## Legacy Agent boundary

The internal Venue Agent retains its separately configured `StartRestore`
command flow beneath allowlisted Agent restore roots. That compatibility path
is not installed, enrolled, configured, or exposed by the customer desktop
journey. The desktop host accepts only Save, inspect, Restore, and in-process
Cancel records; it has no Agent identity, commands, queue, service lifecycle,
network, arbitrary output name, or unrestricted operation surface.

Automated proof uses synthetic roots only. Native macOS/Windows packaging,
sandbox/helper behavior, signing, installation, equipment, and live
application/device loading remain separately gated.
