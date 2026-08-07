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

On first start, set `Agent__EnrollmentCode` for that process invocation. After a successful exchange, remove it from the environment. The Agent never writes the enrollment code to appsettings, SQLite, or the credential store.

The durable identity is stored in Windows Credential Manager on Windows or Keychain Services on macOS. Subsequent starts load it without calling the enrollment endpoint. Unsupported operating systems fail closed.

Credential rotation requires the current Agent credential, returns the replacement once with `Cache-Control: no-store`, updates the OS credential store only after server success, and invalidates the prior credential immediately.

## Persisted metadata

- Enrollment ID, venue, SHA-256 digest, creator subject, creation time, expiry, consumption time, and revocation time.
- Agent ID, venue, display name, credential digest, creation time, and revocation time.

## Deployment caveat

The macOS implementation uses the process account's default keychain. LaunchDaemon installation must validate that the selected service account has an available keychain while logged out; otherwise the installer must provision a dedicated service keychain and access policy. The Agent fails closed when Keychain Services rejects access.

## Next implementation slice

- Add authenticated outbound Agent communication.
- Add the durable SQLite job and event queue.
- Schedule credential rotation without making offline startup depend on control-plane availability.
- Record enrollment, authentication, rotation, and revocation audit events.
