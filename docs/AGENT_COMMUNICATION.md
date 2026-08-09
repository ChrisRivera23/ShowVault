# Agent communication and durable queue

## Current event flow

1. The Agent initializes its local SQLite queue with WAL journaling.
2. It records a versioned `AgentConnected` event before attempting network delivery.
3. The dispatcher authenticates with the Agent credential and posts the envelope to the control plane.
4. Successful delivery marks the local outbox row delivered.
5. Network or server failure increments the attempt count and schedules exponential backoff, capped at five minutes.
6. The control plane verifies that the envelope Agent ID matches the authenticated identity and persists each event ID once.
7. Repeated delivery of an already accepted event returns success without duplicating the record.

The SQLite database contains operational queue data only. Agent credentials remain exclusively in the operating-system credential store.

## Current command flow

1. An authorized organization owner, administrator, or manager issues a typed command for an active Agent in one of that organization's venues.
2. The control plane validates the JSON payload and validity window, then persists the versioned envelope in PostgreSQL.
3. The authenticated Agent polls up to 25 pending, unexpired commands.
4. The Agent rejects envelopes with a different Agent ID, unsupported protocol version, or expired validity window.
5. Each valid envelope is inserted idempotently into local SQLite before the Agent acknowledges it.
6. The control plane records acknowledgement idempotently and omits acknowledged commands from later polls.
7. Local command transitions use conditional updates. The allowed transitions are `pending` to `running` or `cancelled`, and `running` to `completed`, `failed`, or `cancelled`.

This provides at-least-once delivery with local deduplication: a lost acknowledgement can cause another poll, but it cannot create a second local command.

Protocol 1.9 adds `IdentifyGrandMa2`. It is valid only for the responder set retained by an exact approved-subnet discovery command. The Agent sends no application data, reads a bounded official TCP 30000 greeting, and reports only path-free counts plus the `grandMA2` family. Control-plane state is independently correlated to the Agent, proposal, discovery command, and identification command.

Protocol 1.10 permits one bounded IPv4 link-local subnet proposal only when exactly one active physical Ethernet interface qualifies. It changes no authorization shape: proposal creation remains socket-free, manager approval grants scope only, and discovery still requires a separate command capped at 32 hosts with 100-500 ms timeouts. Wi-Fi, virtual/routed interfaces, and ambiguous multiple-Ethernet cases remain excluded.

Protocol 1.11 changes no envelope shape. For an approved `169.254.0.0/16` direct link, the Agent reads its bounded OS ARP table and prioritizes complete entries from the exact qualifying interface before filling the same maximum 32-target ICMP set. Neighbor addresses remain local and are not emitted unless reduced to the existing path-free attempted/responding counts.

## Next implementation slice

- Define the minimal plugin manifest and file-oriented discovery contract.
- Implement the first-party file discovery plugin.
- Connect `StartDiscovery` to a command executor that uses the durable state transitions.
- Emit typed completion/failure events through the existing event outbox.
- Add cancellation once there is a real operation boundary to cancel.
