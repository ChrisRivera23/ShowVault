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

System inventory now includes bounded local recovery candidates. The first provider checks standard macOS and Windows Resolume Arena/Avenue application and user-data locations across local user profiles. Missing and inaccessible locations are ignored. Detected paths are stored only in the Agent's local discovery result; durable remote candidate/approval models and onboarding UI are not implemented yet.

## Next slices

1. Add control-plane candidate and approval records without exposing filesystem paths outside the intended venue/Agent boundary.
2. Surface detected systems in native onboarding with an approve/reject workflow.
3. Convert approved candidates into durable Agent-local allowlists.
4. Detect network interfaces and propose bounded venue subnets for approval.
5. Add protocol-aware MA Lighting, Yamaha, and L-Acoustics discovery without representing an open TCP port as product support.
6. Expand standard-location providers for supported workstation applications and export workflows.
