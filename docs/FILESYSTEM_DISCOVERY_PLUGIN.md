# Filesystem discovery plugin

`showvault.filesystem` is the first first-party Venue Agent plugin. It proves the discovery and durable command-execution boundary without freezing a general plugin SDK.

## Security boundary

The Agent operator must explicitly allow local roots in configuration. Remote commands cannot expand this local authority.

```json
{
  "Agent": {
    "DiscoveryRoots": [
      "/Users/showvault/ProductionData"
    ]
  }
}
```

Use absolute paths. On Windows, use values such as `D:\\ProductionData`. An empty list disables filesystem discovery. The plugin does not follow filesystem reparse points or symbolic links, and inaccessible files fail the command rather than silently producing an incomplete inventory.

## StartDiscovery payload

```json
{
  "pluginId": "showvault.filesystem",
  "rootPath": "/Users/showvault/ProductionData/Console",
  "maxFiles": 1000
}
```

The requested root must be the configured root or one of its descendants. `maxFiles` defaults to 1,000 and cannot exceed 10,000.

## Result

The plugin returns a bounded inventory with a root path, completion time, truncation flag, and for each regular file:

- relative path;
- byte size;
- last-modified timestamp;
- lowercase SHA-256 content hash.

The executor stores the full result durably in local SQLite, keyed by command ID, before completing the command. It emits a compact `JobCompleted` summary containing the plugin, root, file count, and truncation flag. Invalid configuration, inaccessible content, unknown plugins, and malformed commands produce a durable `JobFailed` event. The command ID is reused as the outcome event ID so retries remain idempotent.

## Current limits

- Execution is in-process because this is a signed first-party plugin.
- Discovery reads and hashes content but does not copy it into a recovery package.
- Cancellation is limited to Agent shutdown; user-requested cancellation is deferred until a real long-running operation boundary is established.
- A vendor-specific application plugin remains a product-owner decision.
