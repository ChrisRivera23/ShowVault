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

The latest linked command represents each stage. Durable outcome events determine `completed` or `failed`; acknowledged commands without outcomes are `in_progress`; unacknowledged commands are `pending`; and missing stages are `not_started`. Malformed linkage payloads are ignored rather than breaking the entire venue history.

The first Flutter recovery dashboard includes a strict model for this response and a responsive four-stage history presentation. It currently displays clearly labeled foundation preview data because native Auth0 client sign-in and live API loading have not yet been configured. Preview content must not be interpreted as venue evidence.
