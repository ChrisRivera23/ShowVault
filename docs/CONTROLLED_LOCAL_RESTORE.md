# Controlled local restore

`StartRestore` restores a previously verified recovery package into a new or empty, locally authorized target.

## Local authority

The venue operator configures absolute restore roots on the Agent machine:

```json
{
  "Agent": {
    "RestoreRoots": [
      "/Users/showvault/RestoreLab"
    ]
  }
}
```

On Windows, use values such as `D:\\ShowVaultRestoreLab`. An empty list disables restore. A command target must be a strict descendant of one configured root; the configured root itself can never be the target. The root and existing target parents must be real directories, not filesystem links.

## Command payload

```json
{
  "backupCommandId": "00000000-0000-0000-0000-000000000000",
  "verificationCommandId": "00000000-0000-0000-0000-000000000000",
  "targetPath": "/Users/showvault/RestoreLab/Console-Recovery-Test"
}
```

The referenced verification must exist, have a valid evidence digest, match the package, and have passed. Arbitrary package paths are not accepted.

## Restore guarantees

1. Validate the local target allowlist and require the target to be absent or empty.
2. Persist a restore intent before copying.
3. Independently re-verify the immutable package immediately before restore.
4. Copy into a unique sibling staging directory without following source links.
5. Recompute every restored file's size and SHA-256 hash.
6. Recheck the target and atomically rename staging into place.
7. Persist write-once restoration evidence and its SHA-256 digest before completing the command.

If the Agent stops after atomic publication but before evidence is written, the durable intent allows only that same command, package, and target to resume. The Agent verifies the complete published target without following links and then records the evidence. Unrelated non-empty targets are never adopted or overwritten.

This slice restores into a controlled test target. It does not overwrite a live production application's files, invoke vendor software, stop services, or apply configuration automatically.
