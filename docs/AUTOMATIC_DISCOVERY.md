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

System inventory now includes bounded local recovery candidates and read-only local subnet proposals. The first recovery provider checks standard macOS and Windows Resolume Arena/Avenue application and user-data locations across local user profiles. Missing and inaccessible locations are ignored. Detected paths remain only in Agent SQLite. The Agent publishes opaque IDs plus bounded product/type/evidence metadata; the control plane persists tenant-scoped decisions, and the native dashboard supports manager approve/reject review without receiving paths. Protocol 1.3 delivers path-free decisions to the originating Agent, which resolves only locally known IDs and idempotently adds or removes durable exact scopes. Protocol 1.4 validates an approved Resolume user-data candidate through the real plugin and stores its hashed discovery result locally. The control plane correlates path-free outcomes to the unique validation command, native onboarding displays pending/passed/failed state and file count, and only the latest passing result can start backup without a path. Unknown IDs fail without granting access, installed applications are not accepted as recovery roots, and a new decision clears stale validation evidence.

Subnet derivation considers only active physical Ethernet and Wi-Fi interfaces with private IPv4 unicast addresses and contiguous usable masks. It excludes loopback, link-local, tunnel, PPP, VPN/virtual/container/bridge-like interfaces, public addresses, /31-/32 prefixes, and network or broadcast addresses. A directly assigned network broader than /24 is narrowed to the /24 containing the Agent, proposals are deduplicated and capped at eight, and evidence reports the interface class and bounding decision without exposing the host address. Collection opens no sockets and contacts no hosts. Protocol 1.5 persists opaque proposals within their Agent and venue tenancy, native onboarding supports manager approve/reject review, and decisions are recorded in Agent-local SQLite. Approval does not authorize or start discovery.

Protocol 1.6 adds a second manager action for one approved proposal. The Agent resolves its CIDR locally and performs reachability-only ICMP checks against at most 32 usable addresses, with 100-500 ms per-host timeouts and concurrency capped at eight. Responding addresses are retained only in Agent SQLite and keyed by the exact authorization command and proposal. Durable path-free results and control-plane events contain only attempted and responding counts; they contain no host addresses, ports, banners, or product claims, and discovery performs no synchronization. Rejecting the proposal removes its locally retained host sets.

Protocol 1.7 adds a separate manager action for grandMA3 identification after a completed bounded discovery with at least one responder. The command references the opaque proposal and exact discovery command only. The Agent reads that local responder set, checks the officially documented grandMA3 Web Remote HTTP service on port 8080 with 100-500 ms timeouts, and requires a `grandMA3` response signature. Addresses and matches remain in Agent SQLite. The path-free completion contains only attempted and identified counts plus the `grandMA3` product family. The probe does not authenticate, enumerate a session, synchronize state, retain response content, or claim grandMA2 support.

## Next slices

1. Persist bounded path-free grandMA3 identification state/evidence in the tenant control plane and expose it for operator review.
2. Extend the same primary-evidence model to Yamaha and L-Acoustics.
3. Define a documented primary-evidence network signature before adding grandMA2 identification.
4. Expand standard-location providers for supported workstation applications and export workflows.
