# Recovery history read model

The recovery-history endpoint derives a tenant-scoped Scan → Backup → Verify → Restore view from the existing durable command and Agent-event ledger.

```http
GET /api/v1/organizations/{organizationId}/venues/{venueId}/recovery-runs
Authorization: Bearer <Auth0 access token>
```

Any organization member may read recovery history for a venue in that organization. Users outside the organization are forbidden. Agent-local payload details such as discovery roots, package paths, and restore targets are not returned.

Each `StartDiscovery` command begins a run. Later stages are linked through their typed payload references:

- `CreateBackup.discoveryCommandId`;
- `VerifyBackup.backupCommandId`;
- `StartRestore.backupCommandId`.

The latest valid linked command represents each stage. Every child command and outcome must match the run's Agent identity, commands must follow their parent in time, and Restore must reference the selected verification. Durable outcome events determine `completed` or `failed`; acknowledged commands without outcomes are `in_progress`; queued commands are `pending`; expired commands are `expired`; and missing stages are `not_started`. Malformed or invalid linkage is ignored rather than breaking or contaminating the venue history.

The Flutter recovery dashboard includes a strict model for this response and a responsive four-stage history presentation. Native Auth0 Universal Login supplies the API bearer token; the client discovers the first accessible organization and venue before loading this endpoint. Every displayed recovery run comes from the tenant-scoped control-plane response. Empty, pending, in-progress, completed, failed, and expired states use distinct language and icons, and the client never substitutes synthetic recovery evidence.
