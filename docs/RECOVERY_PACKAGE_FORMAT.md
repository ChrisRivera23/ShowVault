# Recovery package format 1.0

ShowVault recovery packages are immutable, content-addressed local directories that can be inspected without the control plane.

## Layout

```text
<PackageDirectory>/
  <64-character-package-id>/
    manifest.json
    content/
      <source-relative files>
```

The package ID is the lowercase SHA-256 digest of the exact UTF-8 `manifest.json` bytes. Files are published read-only. The Agent writes into a unique sibling staging directory and atomically renames it only after every source file has been copied and re-hashed successfully. Published packages are never overwritten.

If `Agent:PackageDirectory` is unset, packages are stored below `<DataDirectory>/packages`. A configured package directory must be absolute. This slice intentionally supports local disk only; NAS and S3-compatible targets remain deferred pending product-owner approval.

## Manifest

Format `1.0` records:

- format version, Agent ID, source discovery command ID, and creation timestamp;
- source identity plus plugin, product, and firmware versions when known;
- a path-sorted file inventory with byte sizes and SHA-256 hashes;
- dependencies and relationship snapshot;
- restore prerequisites and compatibility rules;
- immutable verification records.

The generic filesystem plugin initially leaves product/firmware versions and the domain-specific collections empty. Later plugin and verification slices can populate those fields without changing the package's top-level schema.

Paths in the manifest always use `/` separators and must remain beneath the discovered root. Absolute paths, traversal, filesystem links, changed sizes, changed hashes, missing files, and truncated discovery inventories fail package creation before publication.

## CreateBackup command

```json
{
  "discoveryCommandId": "00000000-0000-0000-0000-000000000000"
}
```

The referenced `StartDiscovery` result must exist in the Agent's durable SQLite state. A successful command stores the package ID, local path, and manifest in SQLite and emits a compact `JobCompleted` event. Replaying a running command uses its stable issued timestamp and resolves to the same package ID.

Before an existing or concurrently published package is accepted, the Agent checks the exact directory and file set, rejects filesystem links, compares the manifest bytes, and re-hashes every declared content file. Altered, missing, extra, or linked entries fail the command instead of recording damaged recovery material as a completed backup.

## Trust boundary

Read-only flags discourage accidental modification but are not a cryptographic access-control mechanism. `VerifyBackup` independently verifies the configured package-store boundary, a bounded regular manifest, complete required manifest shape, manifest digest, exact layout, regular content files, content sizes, and content hashes and emits immutable verification evidence. Unix verification uses no-follow, nonblocking file opens so sockets and FIFOs cannot stall hashing, and the inspected handle is the one hashed. Format 1.0 is not digitally signed, so it provides integrity but not creator authenticity.
