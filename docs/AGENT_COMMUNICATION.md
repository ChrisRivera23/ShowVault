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

## Command queue boundary

Typed `AgentCommandEnvelope` values can be inserted idempotently into the durable SQLite command queue and survive restart. Command retrieval, acknowledgement, state transitions, cancellation, and execution are intentionally deferred to the next vertical slice.

## Next implementation slice

- Persist control-plane commands scoped to one Agent.
- Add authenticated polling with protocol-version validation.
- Persist commands locally before acknowledging receipt.
- Add resumable status transitions and event emission.
- Connect `StartDiscovery` to the first file-oriented plugin only after the queue semantics are proven.
