Module twin — desired properties (paste under properties.desired)

Only one key is required:
  postgresConnectionString — Npgsql connection string (e.g. Host=postgres;Port=5432;Database=edge_db;Username=...;Password=...;SSL Mode=Prefer)

Queries are fixed in code against the "Count" table (aggregates by zone; minute buckets are rolled up for hourly).

Direct methods (invoke on module DeviceDataManagerModule)

1) GetCountsByDay
   Body (JSON): { "day": "2026-03-26", "timeZone": "America/Lima" }
   Response: { "day": "...", "zones": [ { "zoneName": "...", "totalEvents": 123 } ] }

2) GetCountsByHour
   Body (JSON): { "day": "2026-03-26", "timeZone": "America/Lima" }
   Response: { "day": "...", "rows": [ { "zoneName": "...", "hourUtc": "...", "totalEvents": 99 } ] }
   (Per-minute rows in the table are summed into one row per zone per hour; no per-minute direct method.)

Optional dev override: environment variable POSTGRES_CONNECTION_STRING if twin is empty.

--- ACR pull error (registry operation error / StatusCode 500) ---

Private images need registry credentials on the Edge device. In the deployment manifest,
under $edgeAgent -> properties.desired -> runtime -> settings -> registryCredentials,
configure glitchaiacr.azurecr.io (see deployment.template.json).

- username: usually the registry short name (e.g. glitchaiacr)
- password: ACR admin password OR a service principal token with AcrPull
- address: glitchaiacr.azurecr.io

Replace REPLACE_WITH_ACR_ADMIN_PASSWORD_OR_TOKEN in the manifest, then redeploy.

Also verify: the image tag exists in ACR; the device reaches *.azurecr.io (firewall/DNS);
the module image URI matches exactly (including :beta2).
