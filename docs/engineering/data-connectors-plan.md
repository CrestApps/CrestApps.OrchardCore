# Data connectors and imported data sets (issue #1199)

Status: implemented, 2026-10-30 (see "Decisions made while building" at the end for where the build differs from the first plan). Tracks [CrestApps.OrchardCore#1199](https://github.com/CrestApps/CrestApps.OrchardCore/issues/1199).

## Goal

Let a tenant bring data from outside systems (any SQL Server database, any Elasticsearch or OpenSearch cluster, any HTTP/REST API, files in the media library) into Orchard Core's existing data source infrastructure (`OrchardCore.DataSources.Abstractions`), so the **Read a data source** pipeline step, the report builder and every other `IDataSourceManager` consumer can read it with no source-specific code.

Two access modes per data set:

- **Live**: every read goes to the external system.
- **Imported** (materialized): a shared refresh engine copies the rows into tenant-local storage on a schedule or on demand; reads are served from the last successfully published copy.

## What already exists and is reused

| Piece | From | Use |
| --- | --- | --- |
| `IDataSource`, `DataSetDescriptor`, `DataSetSchema`, `DataField`, `DataBatch`, `DataSourceQuery`, `DataCondition`, `DataValues` | OC `OrchardCore.DataSources.Abstractions` | The consumption contract; nothing is duplicated. |
| `AddDataSource<T>()`, `IDataSourceManager` | OC `OrchardCore.DataSources.Core` (feature `OrchardCore.DataPipelines.Core`) | Registration and discovery of the two new data sources. |
| `IDataFileFormatManager` (CSV, JSON, JSON Lines, Excel) | OC `OrchardCore.DataSources.Core` | Parsing for the Files connector and non-JSON HTTP responses. |
| `DataFilterPredicates` re-filtering in consumers | OC `DataSourceStep` | Conditions are hints. A source may push a condition down only when doing so never drops a matching row. |
| `DocumentCatalog<T, TIndex>`, `ICatalog<T>`, `ExtensibleEntity` | CrestApps `YesSql.Core` / CrestApps.Core | Records for connections, data sets and refresh runs; connector settings in `Properties`. |
| `ProtectedSettingExtensions` | `CrestApps.OrchardCore.Core` | Secrets protected with the tenant's data protection keys. |
| `IBackgroundTask` + `[BackgroundTask]` lock, `IDistributedLock` | Orchard Core | Scheduling without a new job framework; one refresh per data set across instances. |
| `HttpBackgroundJob.ExecuteAfterEndOfRequestAsync` | Orchard Core | "Refresh now" runs after the request in its own shell scope. |
| AdminList shape, `<admin-list>` | Orchard Core admin | Connection, data set and refresh run lists. |
| Outbound-host guard pattern (`WebhookNetwork`) | Commerce.Webhooks | SSRF guard for HTTP-based connectors. |

## Projects

| Project | Contents |
| --- | --- |
| `src/Abstractions/CrestApps.OrchardCore.DataConnectors.Abstractions` | Connector contract, capabilities, descriptors, records (`DataConnection`, `ConnectedDataSet`, `DataSetRefreshRun`), row store contract, network policy contract. No provider dependencies. |
| `src/Core/CrestApps.OrchardCore.DataConnectors.Core` | Connector manager, data set field mapping, refresh engine, SQL row store, the two `IDataSource`s, HTTP authentication (API key, Basic, Bearer, OAuth 2.0 client credentials / password, OpenID Connect discovery), outbound network guard, relational connector base class, HTTP/REST, Elasticsearch and Files connectors. |
| `src/Modules/CrestApps.OrchardCore.DataConnectors` | Orchard module: features, admin screens, display drivers, migrations, permissions, background task. |
| `src/Modules/CrestApps.OrchardCore.DataConnectors.SqlServer` | SQL Server connector (the only project that references `Microsoft.Data.SqlClient`). |

Features (ids):

| Feature | Id | Depends on |
| --- | --- | --- |
| Data Connectors | `CrestApps.OrchardCore.DataConnectors` | `OrchardCore.DataPipelines.Core` |
| HTTP API connector | `CrestApps.OrchardCore.DataConnectors.Http` | Data Connectors |
| Elasticsearch connector | `CrestApps.OrchardCore.DataConnectors.Elasticsearch` | Data Connectors |
| Media files connector | `CrestApps.OrchardCore.DataConnectors.Files` | Data Connectors, `OrchardCore.Media` |
| SQL Server connector | `CrestApps.OrchardCore.DataConnectors.SqlServer` | Data Connectors |

## 1. Connector contract

```csharp
public interface IDataConnector
{
    static abstract string Name { get; }
    static abstract LocalizationSource DisplayName { get; }
    static abstract LocalizationSource Description { get; }
    static abstract DataConnectorCapabilities Capabilities { get; }

    Task<DataConnectorTestResult> TestAsync(DataConnectorContext context, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DataConnectorResource>> DiscoverAsync(DataConnectorContext context, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DataField>> DescribeAsync(DataConnectorReadContext context, CancellationToken cancellationToken = default);
    IAsyncEnumerable<DataBatch> ReadAsync(DataConnectorReadContext context, CancellationToken cancellationToken = default);
}
```

- Static members mirror `IDataSource` and `IDataFileFormat`: `services.AddDataConnector<T>()` adds a `DataConnectorDescriptor` to `DataConnectorOptions`; `IDataConnectorManager` resolves by name. A connector module never touches the core module.
- `DataConnectorCapabilities` flags: `Live`, `Import`, `IncrementalImport`, `Discovery`, `ConditionPushdown`.
- `DataConnectorContext` carries the `DataConnection` and an `IDataConnectorSecrets` reader (unprotects a named secret only when the connector asks for it).
- `DataConnectorReadContext` adds the `ConnectedDataSet`, the source fields wanted, conditions (already translated to source field names), `MaxRows`, `BatchSize`, parameters and, for incremental imports, the watermark field and value.
- Connectors return raw batches keyed by **source** field names. The shared `ConnectedDataSetMapper` maps source names to data set field names, coerces types with `DataValues.Coerce`, keeps only the wanted fields, and enforces `MaxRows` and batch size. Connectors never re-implement that.
- Pagination, batching, cancellation, timeouts and source errors are the connector's job; errors surface as `DataConnectorException` with a message safe to show (never a secret or a connection string).

## 2. Records

- `DataConnection` (`CatalogItem`): `Name` (technical, unique), `DisplayText`, `Description`, `Connector`, created/modified, author. Connector settings live in `Properties` (e.g. `SqlServerConnectionSettings`); secrets are stored protected and never rendered back into a form.
- `ConnectedDataSet` (`CatalogItem`): `Name` (stable data set name, unique), `DisplayText`, `Description`, `Group`, `ConnectionId`, `Resource`, `AccessMode` (`Live` | `Imported`), `Fields` (`ConnectedDataField`: `Name`, `SourceName`, `DisplayName`, `Type`, `IsIdentifier`, `IsIncluded`), `DefaultDateField`, `Refresh` settings (`Mode` Full/Incremental, `IncrementalField`, `IntervalMinutes`, `SchemaChangePolicy`), connector resource settings in `Properties`, and the published state (`ActiveVersion`, `LastRefreshUtc`, `LastSuccessUtc`, `RowCount`, `LastError`, `Watermark`, `NextRefreshUtc`, `RefreshRequestedUtc`).
- `DataSetRefreshRun` (`CatalogItem`): data set, trigger (Manual/Scheduled), mode, status (Running/Succeeded/Failed/Canceled), started/completed, rows read/written, version, schema change summary, error.

All three live in their own YesSql collection `DataConnectors` with map indexes; YesSql is tenant-scoped, so records are tenant-isolated.

## 3. Materialized storage: evaluated options

| Option | Verdict |
| --- | --- |
| Content items | Rejected: versioning, handlers and indexing overhead per row; the issue rules it out as the default. |
| One YesSql document per row | Rejected: document JSON plus index rows per record; no batch insert; slow full refreshes. |
| One generated SQL table per data set, typed columns | Strong pushdown, but DDL per refresh across four dialects, identifier limits, migrations of user-defined schemas, and harder atomic swap. Kept as a possible second `IDataSetRowStore`. |
| **One shared row table, rows as JSON, versioned** | **Chosen.** Cross-dialect (YesSql `ISqlDialect` + Dapper), batched multi-row inserts, keyset reads, schema changes need no DDL, staging and atomic publication by version. |

Table `{prefix}DataConnectors_Rows`: `Id` (identity), `DataSetId` (26), `Version` (26), `RowKey` (64, SHA-256 of the identifier values, nullable), `Data` (JSON object keyed by data set field name). Indexes `(DataSetId, Version, Id)` and `(DataSetId, Version, RowKey)`. Created by the module migration through `SchemaBuilder`, so it gets the tenant's table prefix and schema.

`IDataSetRowStore`: `WriteAsync(dataSetId, version, rows)`, `CopyAsync(dataSetId, fromVersion, toVersion)`, `DeleteKeysAsync(dataSetId, version, keys)`, `ReadAsync(dataSetId, version, afterId, take)`, `CountAsync`, `DeleteVersionAsync`, `DeleteAllAsync`.

## 4. Refresh engine (`IDataSetRefreshService`)

1. Acquire `IDistributedLock` `CrestApps.DataConnectors.Refresh:{dataSetId}` (no wait). Busy → the refresh is skipped and logged, never overlapping.
2. Create a `DataSetRefreshRun` (Running) and a new version id.
3. `DescribeAsync` the source; compare with the data set's fields. Policy `Accept` (default): added fields join the data set, removed fields stay (empty) so consumers keep working, type changes update the field unless an administrator chose the type; `Fail`: the run fails and the published version stays. A schema change during an incremental refresh turns it into a full refresh.
4. Full: stream every batch into the new version. Incremental (connector has `IncrementalImport`, data set has identifiers and an incremental field): copy the active version into the new one, read rows with `field > watermark`, delete their keys in the new version, insert them, keep the largest value as the new watermark.
5. Publish: in one `ISession` save, set `ActiveVersion`, row count, watermark, fields, `LastSuccessUtc`; then delete the previous version's rows. A reader that started on the old version keeps reading it until its next batch; the old rows are deleted after publication (a short grace delete is acceptable for v1).
6. Failure or cancellation: delete the staged version, record the error on the run and the data set (`LastError`), keep `ActiveVersion`. Bounded retries: transient failures (HTTP 429/5xx, timeouts, SQL transient numbers) are retried inside connectors up to 3 times; the run itself is not retried until its next schedule.
7. Every run has a timeout (`DataConnectorsOptions.RefreshTimeout`, default 30 minutes) linked with the host's cancellation token.

Scheduling: `DataSetRefreshBackgroundTask` (`* * * * *`, OC task lock) lists imported data sets whose `NextRefreshUtc <= now` or `RefreshRequestedUtc` is set, and refreshes them one at a time in their own scope. "Refresh now" sets `RefreshRequestedUtc` and runs the refresh after the request through `HttpBackgroundJob`; if the site restarts first, the background task picks it up.

## 5. Data sources exposed to consumers

- `ConnectedDataSource` (name `ConnectedData`, "Connected data"): one data set per `ConnectedDataSet` with `AccessMode = Live`. Reads go through the connector with conditions pushed down only when the connector declares `ConditionPushdown` (each connector pushes only conditions that can't drop a matching row).
- `ImportedDataSource` (name `ImportedData`, "Imported data"): one data set per `ConnectedDataSet` with `AccessMode = Imported` and an `ActiveVersion`. Reads stream from `IDataSetRowStore` in keyset batches; identifier conditions (`Equals`/`In` on an identifier field) are pushed down by row key.
- Both implement `IDataSource` directly (data sets are dynamic). Descriptors carry `Group` (the connection's display name), `DefaultDateField` and, in `Description`, the last refresh time for imported sets. They never expose connection settings.

## 6. Security

- Permissions: `ManageDataConnections` (connections and their secrets), `ManageConnectedDataSets` (data sets), `RefreshConnectedDataSets`, `ReadAllConnectedDataSets`, and a dynamic `ReadConnectedDataSet_{Name}` per data set (implied by `ReadAllConnectedDataSets`), all granted to Administrator by default.
- **Authorization policy (DataSourceContext):** a data set is listed, described and read only when `context.User` is not null and holds `ReadConnectedDataSet_{Name}`. This is the same check for live and imported data sets, so materializing under the connection's service credentials never widens access: whoever could not read the live data set cannot read its imported copy either. A null user (no principal) reads nothing, matching OC's own data sources.
- Imports run with the connection's credentials and no user; they never go through `IDataSource`.
- Secrets (passwords, API keys, client secrets, tokens, connection strings) are protected at rest with purpose `CrestApps.DataConnectors.Secrets`, are write-only in the UI ("leave empty to keep"), and are never logged or put in error messages. SQL Server connection strings are parsed with `SqlConnectionStringBuilder`; the password is stored apart from the rest.
- HTTP-based connectors (HTTP API, Elasticsearch, OpenID token endpoints) use one `HttpClient` whose handler refuses to connect to loopback, private, link-local and unspecified addresses unless `DataConnectors:Network:AllowPrivateNetworks` is true or the host is in `DataConnectors:Network:AllowedHosts`. The check runs on the resolved address at connect time (DNS rebinding safe). Redirects are not followed automatically; a redirect is followed (up to 3) only to the same origin. Paths of a data set are relative to the connection's base URL; a next-page link must stay on the same origin.
- SQL Server: identifiers come from discovery and are quoted (`QUOTENAME` rules); custom queries must be a single read statement (`SELECT`/`WITH`), run with a command timeout, inside a transaction that is always rolled back, with parameters bound, never concatenated. Documentation tells administrators to use a login with read-only rights; the connector can't enforce privileges it doesn't own. The host of the connection string goes through the same network policy.
- Tenant isolation: records and rows are in the tenant's database and table prefix; token caches and locks are keyed by tenant name.

## 7. Initial connectors

| Connector | Resources | Live | Import | Incremental | Pushdown | Auth |
| --- | --- | --- | --- | --- | --- | --- |
| SQL Server | Tables and views (discovered from `INFORMATION_SCHEMA`), or a custom read query | yes | yes | yes (watermark column) | numeric, date and boolean comparisons, `MaxRows` → `TOP` | SQL login, integrated (connection string) |
| Elasticsearch / OpenSearch | Indices and aliases (`_cat/indices`, `_alias`), optional query DSL | yes | yes | yes (date/numeric field) | ranges on numeric/date fields, terms on `keyword` fields | None, Basic, API key, Bearer |
| HTTP API | A path (GET/POST) on the base URL, records at a JSON path; JSON or any registered file format | yes | yes | no (no reliable change tracking) | none | None, API key (header/query), Basic, Bearer, custom headers, OAuth 2.0 client credentials or password grant, OpenID Connect (discovery document → token endpoint) |
| Media files | CSV, JSON, JSON Lines, Excel files in the media library | yes | yes | no | none | n/a |

HTTP API pagination strategies: none, page number, offset/limit, cursor (next cursor at a JSON path), next link (URL at a JSON path), `Link` header (`rel="next"`). Every strategy stops at `MaxPages` (default 1,000) and on an empty page.

OAuth/OpenID tokens are cached in memory per tenant + connection + settings hash until 60 s before expiry; a 401 clears the cache and retries once.

The relational connector is an abstract base (`RelationalDataConnector`) with a small dialect contract (open connection, list resources, quote, limit, type mapping), so PostgreSQL, MySQL or SQLite connectors are a dialect each. Tests run it against SQLite.

## 8. Admin

- **Configuration → Data → Connections** (`ManageDataConnections`): AdminList of connections; Add opens a connector picker; the editor is the shared fields (name, description) plus the connector's own display driver; **Test connection** button; delete refuses while data sets use the connection.
- **Configuration → Data → Connected data sets** (`ManageConnectedDataSets`): AdminList with mode, connection, last refresh status, row count; Add picks a connection; the editor picks a discovered resource (or a custom query/path), the access mode (only the modes the connector supports), the fields (discover, include, display name, type, identifier), the refresh mode, interval and schema change policy; **Discover fields**, **Preview** (first 20 rows through the same path consumers use), **Refresh now**; a refresh history list per data set.

## 9. Tests

- Connector manager registration and extensibility (a test connector registered from outside the module).
- Mapper: field mapping, coercion, field selection, `MaxRows`, batching.
- Network guard: private/loopback refused, allowed hosts, redirect rules.
- HTTP auth: API key header/query, Basic, Bearer, OAuth client credentials, OpenID discovery, token caching and the 401 retry.
- HTTP API connector: each pagination strategy, records path, type inference, error mapping, max pages.
- Elasticsearch connector: mapping → fields (nested objects), scroll paging and clear, pushdown body, auth header.
- Relational connector on SQLite: discovery, describe, read, custom query guard, pushdown that never drops rows, incremental watermark.
- SQL Server dialect: quoting, `TOP`, read-only statement guard, connection string parsing keeps the password out.
- Row store on SQLite: write/read keyset, copy, delete keys, delete version, tenant table prefix.
- Refresh engine: initial import, full refresh, failure keeps last good version and deletes staged rows, incremental upsert, schema change Accept/Fail, overlap lock, cancellation.
- Data sources: discovery via `IDataSourceManager`, authorization (no user, no permission, per-data-set permission, read-all), live read through the connector, imported read from the store, identifier pushdown.
- Public API baselines and architecture tests updated.

## 10. Out of scope (v1)

Typed per-data-set tables, cross-source federation, write-back, change data capture, PostgreSQL/MySQL connectors (the dialect contract is there), OAuth authorization-code flows that need an interactive user.

## Decisions made while building

- **Removed source fields are kept** under the Accept policy, with empty values, instead of leaving the data set: a sampled source (an HTTP API) can miss a field on one page, and dropping it would break every report that uses it.
- **Skipped refreshes are logged, not recorded** in the history: the background task and a manual refresh often meet on the lock, and a history full of "skipped" entries hides the runs that matter.
- **The previous version is kept** after a publish, and deleted by the next one, so a consumer that started reading it can finish.
- **The session is saved before rows are written.** On SQLite, a read transaction left open by the YesSql session keeps the row store's own connection from committing.
- **The SQL Server host goes through the network rule too**, not only HTTP connectors, so a tenant can't probe the host's network through a database connection.
- **Custom SQL queries run as a derived table**, which is what makes "one SELECT" enforceable (a second statement or a CTE can't be wrapped); the text check rejects unbalanced parentheses, `;` and `--` outside quotes.
- Records carry a YesSql `Id` so a record saved after a commit in the same scope updates its row rather than inserting a second one.

