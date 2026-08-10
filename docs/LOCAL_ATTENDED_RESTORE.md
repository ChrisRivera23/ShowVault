# Attended local restore

The Flutter desktop application can restore a verified local recovery point while signed out and offline. The first bounded workflow restores into an absent directory supplied programmatically or publishes a `ShowVault Restored Files` child inside an existing empty regular directory selected by the operator. It does not load data into a running application or device.

## Operator flow

1. Open an authorized ShowVault Pro vault.
2. Choose **Restore** beside a locally verified recovery point.
3. Read the explanation that the target must be new or empty and that existing content is never overwritten.
4. Select an empty target with the native directory picker.
5. ShowVault reverifies the independent manifest, package manifest, exact package file set, sizes, and SHA-256 values.
6. ShowVault copies into a private staging directory, verifies the complete staged tree, atomically publishes it, and verifies the published tree again. For a picker-selected existing target, both staging and the published `ShowVault Restored Files` directory remain inside the authorized target so the macOS sandbox grant is sufficient.
7. A successful restore reports the verified file and byte counts. Cloud login, network availability, and upload status are not consulted.

## Safety and restart behavior

The restore engine rejects:

- a recovery-point ID not present in the authorized vault;
- a changed, malformed, oversized, linked, incomplete, or extra package entry;
- absolute, traversing, duplicate, or otherwise unsafe logical paths;
- a target that is a file, link, non-empty directory, inside the vault, or contains the vault;
- a target that appears or changes identity while copying;
- recovery-point mutation during copying; and
- an unsafe or unowned interrupted staging directory.

Staging names are derived from the package ID and target name. An ownership marker must match both before ShowVault removes interrupted staging, so unrelated data is not treated as disposable. The native access coordinator permits only an empty target or bounded internal staging directories left by an interrupted ShowVault restore; the restore engine then accepts only the one exact expected owned stage. Cancellation and timeout checks run throughout copying and verification. Failure before publication removes only owned incomplete staging and preserves both the immutable recovery point and the operator's selected target.

For an existing selected target, the verified staged content is renamed to the fixed child `ShowVault Restored Files` without removing or renaming the selected directory. An absent programmatic target retains direct atomic publication. Both modes require a same-volume rename within their staging boundary. Cross-volume publication is not part of this slice.

## Restore evidence

A successful restore writes a bounded JSON record under `Reports/Restores`. The evidence records the package ID, opaque candidate key, bounded product name, completion time, restored counts, generic target kind, verification method, and a digest of the evidence fields.

It intentionally excludes source paths, target paths, credentials, private contents, and cloud-facing local paths. The evidence location is checked before any target mutation and the record is written atomically after final verification.

## Verification evidence and limitations

Synthetic automated tests cover absent and existing-empty targets, non-empty and linked targets, package tamper and mutation, cancellation cleanup, internal restart cleanup, unowned staging preservation, unsafe evidence storage, target containment, native-picker behavior, and signed-out UI wiring. The complete Flutter suite passes with 77 tests, and the normal universal macOS release bundle builds and validates with sandboxed user-selected read/write access.

An installed release-mode macOS synthetic drill restored two files (185 bytes) through the native target picker. The installed app reported verified completion; the source, local package, and restored SHA-256 values matched; no staging directory remained; and the path-free evidence record was written under the authorized vault. The drill exposed the original sibling-staging sandbox failure and directly validated the internal-stage correction in a rebuilt app.

This slice does not claim Windows installed behavior, in-place application restore, live-device loading, dependency closure, cloud restore, personal-data readiness, notarization, or Recovery Confidence.
