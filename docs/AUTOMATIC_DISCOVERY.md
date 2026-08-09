# Automatic venue discovery

ShowVault must be installable at an unknown venue without requiring the operator to pre-enter application paths, computer specifications, device addresses, or vendor inventory.

The primary outcome is to start ShowVault and find supported venue equipment and software represented in the integration catalog, including systems used in nightclubs, concert halls, houses of worship, and comparable production spaces.

## Intended workflow

1. Install the native client and Venue Agent.
2. Enroll the Agent and grant clearly explained operating-system permissions.
3. ShowVault inventories the host and identifies candidate applications, standard data locations, mounted storage, and local network interfaces.
4. The operator approves a bounded local recovery scope and, separately, the venue subnet to scan.
5. Product-specific plugins validate candidates before ShowVault credits them as protected systems.
6. The operator reviews findings and chooses what to protect.

Manual path and endpoint entry remains an advanced fallback, not the primary onboarding experience.

## Compatibility and direct-connection requirements

Discovery must work on a documented, tested range of older macOS and Windows venue computers rather than assuming current hardware or operating systems. The client, Agent, installer, local database, credential storage, and plugin runtime must each have explicit minimum versions. Unsupported systems must receive a clear explanation and practical fallback; release packaging must not silently raise minimum versions.

A laptop connected directly to one device by Cat5/Cat6 is a required test topology for non-networked equipment such as a grandMA2 console or Yamaha DM7. Directly connected private or IPv4 link-local interfaces follow the proposal, approval, bounded reachability, and separately authorized product-identification flow. Link-local proposals require exactly one qualifying active physical Ethernet interface; Wi-Fi and ambiguous multiple-link cases are excluded. Direct connection never permits configuration, synchronization, or generic-banner identification.

## Security boundary

Automatic discovery does not authorize backup or restore by itself. Candidate discovery is read-only and limited to documented standard locations. Each candidate records why it was found and remains marked as requiring operator approval. Approved paths are then converted into the Agent's exact local allowlist.

Network discovery must derive candidate interfaces and subnets locally, exclude loopback, tunnel, and obviously non-venue interfaces, and require a single explicit operator approval before bounded non-invasive discovery. IPv4 link-local is excluded except for the exact single-physical-Ethernet direct-link rule. ShowVault must not sweep arbitrary routed networks or treat generic reachability as product identification.

## Current slice

System inventory now includes bounded local recovery candidates and read-only local subnet proposals. The first recovery provider checks standard macOS and Windows Resolume Arena/Avenue application and user-data locations across local user profiles. Missing and inaccessible locations are ignored. Detected paths remain only in Agent SQLite. The Agent publishes opaque IDs plus bounded product/type/evidence metadata; the control plane persists tenant-scoped decisions, and the native dashboard supports manager approve/reject review without receiving paths. Protocol 1.3 delivers path-free decisions to the originating Agent, which resolves only locally known IDs and idempotently adds or removes durable exact scopes. Protocol 1.4 validates an approved Resolume user-data candidate through the real plugin and stores its hashed discovery result locally. The control plane correlates path-free outcomes to the unique validation command, native onboarding displays pending/passed/failed state and file count, and only the latest passing result can start backup without a path. Unknown IDs fail without granting access, installed applications are not accepted as recovery roots, and a new decision clears stale validation evidence.

Subnet derivation considers active physical Ethernet and Wi-Fi interfaces with private IPv4 unicast addresses and contiguous usable masks. It excludes loopback, tunnel, PPP, VPN/virtual/container/bridge-like interfaces, public addresses, /31-/32 prefixes, and network or broadcast addresses. Protocol 1.10 additionally permits one IPv4 link-local proposal only when exactly one active physical Ethernet interface qualifies; Wi-Fi and multiple qualifying Ethernet interfaces produce no link-local proposal. Private networks broader than /24 are narrowed to /24. Link-local presents the full `169.254.0.0/16` scope for explicit review because self-assigned peers need not share a /24, but later active discovery remains capped at 32 exact targets. Proposals are deduplicated and capped at eight, evidence does not expose the host address, and collection opens no sockets or contacts hosts. Approval does not authorize or start discovery.

Protocol 1.11 improves an approved link-local discovery without sweeping the `/16`. Immediately before the separately authorized ICMP run, the Agent executes the operating system's read-only `arp` command with a two-second timeout and a 256 KiB output cap. It accepts only complete IPv4 entries from the one exact qualifying physical Ethernet interface, filters them to the approved `169.254.0.0/16`, excludes the Agent/network/broadcast addresses, deduplicates them, and prioritizes at most 64 passive candidates. The final active target set still contains at most 32 addresses total, uses 100-500 ms timeouts and concurrency eight, and stores responders only in Agent SQLite. If the cache is empty, the bounded sequential fallback remains; discovery cannot guarantee a peer that has produced no observable neighbor entry.

Protocol 1.6 adds a second manager action for one approved proposal. The Agent resolves its CIDR locally and performs reachability-only ICMP checks against at most 32 usable addresses, with 100-500 ms per-host timeouts and concurrency capped at eight. Responding addresses are retained only in Agent SQLite and keyed by the exact authorization command and proposal. Durable path-free results and control-plane events contain only attempted and responding counts; they contain no host addresses, ports, banners, or product claims, and discovery performs no synchronization. Rejecting the proposal removes its locally retained host sets.

Protocol 1.7 adds a separate manager action for grandMA3 identification after a completed bounded discovery with at least one responder. The command references the opaque proposal and exact discovery command only. The Agent reads that local responder set, checks the officially documented grandMA3 Web Remote HTTP service on port 8080 with 100-500 ms timeouts, and requires a `grandMA3` response signature. Addresses and matches remain in Agent SQLite. The path-free completion contains only attempted and identified counts plus the `grandMA3` product family. The probe does not authenticate, enumerate a session, synchronize state, retain response content, or claim grandMA2 support.

The control plane correlates identification outcomes to the exact Agent, proposal, discovery authorization, and pending identification command. It persists bounded path-free pending/completed/failed state, attempted and matched counts, product-family evidence, failure details, and completion time. Native onboarding polls pending work and displays the result for operator review without receiving host addresses. A new proposal decision or bounded discovery clears stale identification evidence.

Protocol 1.8 adds a separate manager-authorized Yamaha DME7 check using only the responders retained by one exact bounded discovery. The Agent uses Yamaha's documented TCP port 49280 and sends only the documented LF-terminated `devinfo productname` and `devinfo manufacturer` read queries. A match requires exact `DME7` and `Yamaha Corporation` responses. Addresses, matches, and bounded response handling remain Agent-local; path-free completion contains attempted/matched counts and `Yamaha DME7`. An open port, partial response, another Yamaha model, or generic reachability is not product evidence.

Yamaha results use independent tenant-scoped pending/completed/failed state and exact Agent/proposal/discovery/identification correlation. Native onboarding offers a separate Yamaha action, polls pending work, and displays bounded path-free counts and family evidence without overwriting grandMA3 results. A new subnet decision or reachability discovery clears both product results because their responder authorization is stale.

Protocol 1.9 adds independently authorized grandMA2 identification against the same exact Agent-local responder boundary. MA Lighting's official grandMA2 Telnet Remote documentation assigns TCP 30000 to the command line, requires login before commands, and shows the unauthenticated server greeting. The Agent connects, sends zero bytes, reads at most 4,096 banner bytes with a 100-500 ms timeout, and requires the documented guest/login-prompt combination before returning `grandMA2`. Telnet Remote must already be enabled, so a disabled service is a safe false negative. An open port, generic Telnet banner, partial greeting, grandMA3 behavior, or reachability is not evidence.

grandMA2 results have independent exact command correlation and pending/completed/failed review state. Host addresses and response bytes remain Agent-local; the control plane receives only attempted/matched counts and bounded `grandMA2` evidence. A new decision or discovery clears grandMA2, grandMA3, and Yamaha results as stale.

L-Acoustics network identification is deliberately deferred after official-evidence review. Public L-Acoustics material confirms that LA Network Manager and LA Device Scanner discover and identify units, but does not publish their wire contract or a demonstrably read-only query; the same tools also configure, synchronize, rename, or change network settings. The documented Electronics HTTP API could become a defensible product-specific boundary, but its contract is available only after identity submission and separate terms acceptance, and the public page does not establish a safe identity endpoint or response signature. ShowVault sends no L-Acoustics probe and does not treat an open port, generic HTTP, Milan/AVDECC or Dante metadata, or reachability as L-Acoustics evidence. Reconsider only with terms-authorized primary API documentation and a representative fixture, or a newly published read-only protocol contract.

## Next slices

1. Build a direct-link fixture harness that validates macOS and Windows ARP output, empty-cache behavior, target ordering, and grandMA2/Yamaha identification without real venue configuration changes.
2. Validate grandMA2 identification against representative console/onPC fixtures with Telnet Remote enabled and disabled.
3. Expand standard-location providers for supported workstation applications and export workflows.
