# Automatic venue discovery

ShowVault must be installable at an unknown venue without requiring the operator to pre-enter application paths, computer specifications, device addresses, or vendor inventory.

## Intended workflow

1. Install the native client and Venue Agent.
2. Enroll the Agent and grant clearly explained operating-system permissions.
3. ShowVault inventories the host and identifies candidate applications, standard data locations, mounted storage, and local network interfaces.
4. The operator approves a bounded local recovery scope and, separately, the venue subnet to scan.
5. Product-specific plugins validate candidates before ShowVault credits them as protected systems.
6. The operator reviews findings and chooses what to protect.

Manual path and endpoint entry remains an advanced fallback, not the primary onboarding experience.

## Security boundary

Automatic discovery does not authorize backup or restore by itself. Candidate discovery is read-only and limited to documented standard locations. Each candidate records why it was found and remains marked as requiring operator approval. Approved paths are then converted into the Agent's exact local allowlist.

Network discovery must derive candidate interfaces and subnets locally, exclude loopback, link-local, tunnel, and obviously non-venue interfaces by default, and require a single explicit operator approval before bounded non-invasive discovery. ShowVault must not sweep arbitrary routed networks or treat generic reachability as product identification.

## Current slice

System inventory now includes bounded local recovery candidates. The first provider checks standard macOS and Windows Resolume Arena/Avenue application and user-data locations across local user profiles. Missing and inaccessible locations are ignored. Detected paths remain only in Agent SQLite. The Agent publishes opaque IDs plus bounded product/type/evidence metadata; the control plane persists tenant-scoped decisions, and the native dashboard supports manager approve/reject review without receiving paths. Protocol 1.3 delivers path-free decisions to the originating Agent, which resolves only locally known IDs and idempotently adds or removes durable exact scopes. Protocol 1.4 validates an approved Resolume user-data candidate through the real plugin, stores its hashed discovery result locally, and emits only candidate/plugin identity, file count, and truncation status. Unknown IDs fail without granting access, and installed applications are not accepted as recovery roots.

## Next slices

1. Persist and surface path-free validation outcomes, then start backup from the validated discovery command without manual path entry.
2. Detect network interfaces and propose bounded venue subnets for approval.
3. Add protocol-aware MA Lighting, Yamaha, and L-Acoustics discovery without representing an open TCP port as product support.
4. Expand standard-location providers for supported workstation applications and export workflows.
