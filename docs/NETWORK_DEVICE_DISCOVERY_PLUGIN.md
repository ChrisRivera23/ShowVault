# Network device discovery plugin

`showvault.network-device` is a signed, first-party Venue Agent plugin for bounded TCP reachability discovery. It answers whether explicitly approved device endpoints are reachable from the Agent; it does not identify a vendor or claim that an application is healthy.

## Local allowlist

Every permitted endpoint must be present in the Agent's local `NetworkDiscoveryTargets` configuration as `host:port` (or `[IPv6]:port`). Configuration accepts at most 128 unique endpoints and is validated when the Agent starts. An empty list disables network discovery.

The control plane issues protocol 1.2 `DiscoverNetworkDevices` with a subset of those exact endpoints:

```json
{
  "targets": ["lighting-console.local:443", "192.0.2.10:80"],
  "timeoutMilliseconds": 1000
}
```

Requests must contain 1–128 unique allowlisted targets. Timeout is constrained to 100–5000 milliseconds, and the Agent runs no more than eight probes concurrently.

## Result and safety boundary

Each result records the endpoint and one status: reachable, refused, timed out, or unreachable. The full result is stored durably before the Agent emits a compact `JobCompleted` summary with target and reachable counts.

The plugin requests only `ConnectNetworkEndpoints`. It performs a TCP connection attempt and does not:

- sweep an address range or discover unconfigured hosts;
- send application data or capture banners;
- run shell commands or external scanners;
- read files, credentials, SNMP communities, or device configuration; or
- treat reachability as recovery verification.

Vendor-aware discovery, authenticated device backup, and recovery remain separate plugins after the Product Owner selects the first real integration.
