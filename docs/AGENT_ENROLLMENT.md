# Venue Agent enrollment and identity

## Security boundary

Human operators authenticate with Auth0. Venue Agents use a separate ShowVault Agent scheme and never receive or reuse human access tokens.

Enrollment and credential secrets are generated from 256 bits of cryptographically secure randomness. The control plane returns each secret only once, sends `Cache-Control: no-store`, and persists only its SHA-256 digest. Secret verification uses fixed-time comparison. Secrets must never be logged.

## Enrollment flow

1. A Manager, Administrator, or Owner requests an enrollment code for a venue.
2. The control plane creates a venue-scoped code that expires after 15 minutes.
3. The Agent exchanges the code through the rate-limited public enrollment endpoint.
4. The code is consumed exactly once using optimistic concurrency protection.
5. The control plane returns an Agent ID and durable credential once.
6. The Agent authenticates with `Authorization: ShowVault-Agent {agentId}.{secret}`.
7. An authorized venue manager can revoke the Agent immediately.

## Persisted metadata

- Enrollment ID, venue, SHA-256 digest, creator subject, creation time, expiry, consumption time, and revocation time.
- Agent ID, venue, display name, credential digest, creation time, and revocation time.

## Next implementation slice

- Add first-run enrollment behavior to the Venue Agent executable.
- Store the durable credential in the operating-system keychain, never appsettings or SQLite.
- Add credential rotation before introducing long-running outbound transport.
- Record enrollment, authentication, rotation, and revocation audit events.
