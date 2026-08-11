# Agent communication and durable queue

## Current event flow

1. The Agent initializes its local SQLite queue with WAL journaling.
2. It records a versioned `AgentConnected` event before attempting network delivery.
3. The dispatcher authenticates with the Agent credential and posts the envelope to the control plane.
4. Shared validation bounds the correlation ID and JSON payload and rejects unsupported protocol or event types before local persistence and again at control-plane ingress.
5. Successful delivery marks the local outbox row delivered.
6. Network, authentication, timeout, rate-limit, and server failures increment the attempt count and schedule exponential backoff, capped at five minutes, preserving events across credential remediation.
7. Other client-error responses permanently reject the local row so invalid events do not retry forever.
8. The control plane verifies that the envelope Agent ID matches the authenticated identity and persists each event ID once.
9. Repeated delivery of an already accepted event returns success without duplicating the record.

The SQLite database contains operational queue data only. Agent credentials remain exclusively in the operating-system credential store.

## Current command flow

1. An authorized organization owner, administrator, or manager issues a typed command for an active Agent in one of that organization's venues.
2. Shared validation bounds command identity, type, protocol, validity, correlation ID, JSON depth, and UTF-8 payload size before PostgreSQL persistence.
3. The authenticated Agent reads a database-bounded candidate window, marks expired commands durably, and returns at most 25 pending, unexpired commands.
4. The Agent repeats shared validation and rejects envelopes with a different Agent ID or an already expired validity window.
5. Each valid envelope is inserted idempotently into local SQLite before the Agent acknowledges it.
6. The control plane records acknowledgement idempotently and omits acknowledged commands from later polls.
7. Local command transitions use conditional updates. The allowed transitions are `pending` to `running` or `cancelled`, and `running` to `completed`, `failed`, or `cancelled`.
8. Transport, non-shutdown timeout, and malformed-success-response failures are logged only as bounded categories and retried on a later polling cycle.

This provides at-least-once delivery with local deduplication: a lost acknowledgement can cause another poll, but it cannot create a second local command.

## Next implementation slice

- Define the minimal plugin manifest and file-oriented discovery contract.
- Implement the first-party file discovery plugin.
- Connect `StartDiscovery` to a command executor that uses the durable state transitions.
- Emit typed completion/failure events through the existing event outbox.
- Add cancellation once there is a real operation boundary to cancel.
