# Recovery package verification

`VerifyBackup` independently evaluates a published local recovery package without trusting the SQLite copy of its manifest.

## Command payload

```json
{
  "backupCommandId": "00000000-0000-0000-0000-000000000000"
}
```

The ID references a completed local `CreateBackup` command. Arbitrary remote filesystem paths are not accepted.

## Evidence levels

Structural verification checks:

- package path is the exact package-ID child of the configured package store, and neither root is a link;
- `manifest.json` and `content/` exist and are not links;
- manifest format and Agent identity are supported;
- discovery identity, creation time, source identity/plugin metadata, and all required manifest collections and nested values are present;
- file paths are safe, unique, and ordinally sorted;
- file sizes and SHA-256 values are well formed;
- the package has no missing, unexpected, or linked content.

Cryptographic verification checks:

- SHA-256 of the exact manifest bytes equals the package ID;
- every content file has the manifest-recorded byte size;
- independently recomputed SHA-256 content hashes match the manifest.

The verifier never follows package links. If structure is unsafe, content hashing is not attempted through that structure.

## Durable result

A verification that successfully detects corruption completes with `passed: false`; it is not treated as an executor failure. The first execution records its actual verification time. The full result is serialized, hashed with SHA-256, and inserted once into SQLite under the verification command ID. Retries reuse that immutable evidence and its original timestamp. The completion event contains only the package ID, overall result, per-level pass/fail status, and evidence digest.

The evidence digest detects accidental alteration of the stored result but is not a digital signature. Package authenticity, key management, and signed verification attestations remain future security work. Format 1.0 currently proves structural and content integrity, not who created the package.
