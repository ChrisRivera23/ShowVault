# Venue Agent enrollment and identity

## Security boundary

Human operators authenticate with Auth0. Venue Agents use a separate ShowVault Agent scheme and never receive or reuse human access tokens.

Enrollment and credential secrets are generated from 256 bits of cryptographically secure randomness. The control plane generates enrollment codes; the Agent generates its credential secret before activation and protects the pending transition in the OS credential store. The control plane sends secret-bearing responses with `Cache-Control: no-store` and persists only SHA-256 digests. Secret verification uses fixed-time comparison. Secrets must never be logged.

## Enrollment flow

1. A Manager, Administrator, or Owner requests an enrollment code for a venue.
2. The control plane creates a venue-scoped code that expires after 15 minutes.
3. Before network access, the Agent generates a request ID and credential secret and durably stores a pending enrollment in Credential Manager or Keychain Services.
4. The Agent exchanges the code, request ID, and credential secret through the rate-limited public enrollment endpoint.
5. The code is consumed exactly once using optimistic concurrency protection. The request ID and issued Agent ID make an identical retry idempotent without persisting plaintext server-side.
6. After the server response, the Agent atomically replaces the pending state with its active identity. If that save fails or the process stops, restart retries the same pending enrollment and receives the same identity.
7. The Agent authenticates with `Authorization: ShowVault-Agent {agentId}.{secret}`.
8. An authorized venue manager can revoke the Agent immediately.

On first start, set `Agent__EnrollmentCode` for that process invocation. Before contacting the server, the Agent temporarily places the code only in its protected pending credential-store record so a crash before or after the exchange is recoverable. Successful activation replaces that record with the durable Agent identity. The Agent never writes the enrollment code to appsettings, SQLite, logs, or server-side plaintext storage.

The durable identity is stored in Windows Credential Manager on Windows or Keychain Services on macOS. Subsequent starts load it without calling the enrollment endpoint. Unsupported operating systems fail closed.

Credential rotation first persists a pending record containing the old identity, a request ID, and the Agent-generated replacement secret. The server applies a request ID once and treats an exact replay as success. If the response or final local save is lost, restart tries the old credential and then the pending new credential, reconciles the same rotation, and replaces pending state with the active identity. A reused request ID with a different secret is rejected.

## Persisted metadata

- Enrollment ID, venue, SHA-256 digest, creator subject, creation time, expiry, consumption time, revocation time, activation request ID, and issued Agent ID.
- Agent ID, venue, display name, credential digest, creation time, rotation time/request ID, and revocation time.

## Deployment caveat

The macOS implementation uses the process account's default keychain. LaunchDaemon installation must validate that the selected service account has an available keychain while logged out; otherwise the installer must provision a dedicated service keychain and access policy. The Agent fails closed when Keychain Services rejects access.

## Next implementation slice

- Add authenticated outbound Agent communication.
- Add the durable SQLite job and event queue.
- Schedule credential rotation without making offline startup depend on control-plane availability.
- Record enrollment, authentication, rotation, and revocation audit events.
