---
sidebar_label: Data Connectors
title: Data Connectors
description: Connect to SQL Server, Elasticsearch, any HTTP API and media library files, and expose their data sets to reports and data pipelines, read live or imported on a schedule.
user_manual:
  - user-manual/data-connectors
---

The **Data Connectors** modules bring data from external systems into Orchard Core's data sources (`OrchardCore.DataSources`). A data set read through a connection is listed by `IDataSourceManager` like any other data set, so the **Read a data source** step of data pipelines, the [Report Builder](report-builder/index.md) and every other consumer read it with no code of their own.

Each data set is read in one of two ways:

- **Live**: every read goes to the external system, through its connection.
- **Imported**: a refresh copies the rows into the tenant's database, on a schedule or on demand, and reads are served from the last refresh that succeeded. The data set stays readable when the external system is down.

| Feature | ID | Adds |
| --- | --- | --- |
| Data Connectors | `CrestApps.OrchardCore.DataConnectors` | Connections, data sets, the live and imported data sources, the refresh task, and **Tools > Data Connectors**. Depends on `OrchardCore.DataPipelines.Core`. |
| HTTP API Data Connector | `CrestApps.OrchardCore.DataConnectors.Http` | Any HTTP or REST API answering JSON, CSV or JSON Lines. |
| Elasticsearch Data Connector | `CrestApps.OrchardCore.DataConnectors.Elasticsearch` | Elasticsearch and OpenSearch indices, aliases and patterns. |
| Media Files Data Connector | `CrestApps.OrchardCore.DataConnectors.Files` | CSV, JSON, JSON Lines and Excel files of the media library. Depends on `OrchardCore.Media`. |
| SQL Server Data Connector | `CrestApps.OrchardCore.DataConnectors.SqlServer` | Tables, views and read-only queries of SQL Server and Azure SQL. Its own module, the only one that loads `Microsoft.Data.SqlClient`. |

The admin screens are described in the [User Manual](../user-manual/data-connectors.md).

## How it fits together

| Piece | Role |
| --- | --- |
| `IDataConnector` | Talks to one kind of system: checks a connection, lists its resources, describes a resource's fields, and reads its rows in batches. It never stores rows, schedules refreshes or checks permissions. |
| `DataConnection` | One system a tenant reaches: the connector, its settings (in `Properties`) and its secrets (protected). |
| `ConnectedDataSet` | One resource of a connection: its stable technical name, its fields (data set name, source name, type, identifier, shown), its access mode and refresh settings, and the state of its imports. |
| `ConnectedData` data source | Lists and reads the live data sets, through their connectors. |
| `ImportedData` data source | Lists and reads the imported data sets, from the row store. |
| `IDataSetRowStore` | Keeps the rows of imported data sets in the table `{prefix}DataConnectors_Rows` of the tenant's database, by version. |
| `IDataSetRefreshService` | Refreshes imported data sets; the **Connected Data Set Refresh** background task calls it every minute. |

Connections, data sets and refreshes are YesSql documents in the `DataConnectors` collection of the tenant's database, so each tenant has its own.

## Imports and refreshes

A refresh of an imported data set:

1. takes a distributed lock on the data set, so a refresh from the admin screen and a scheduled one never overlap on any instance of the site; a refresh that finds the lock taken is skipped;
2. asks the connector for the resource's fields and compares them with the data set's;
3. reads the rows in batches and writes them as a **new version** beside the one consumers read;
4. publishes the new version only when every row was written, and keeps the version before it for consumers still reading it; older versions are deleted;
5. on a failure, a cancellation or a timeout, deletes the new version and keeps the published one, and records the error on the data set and in its history.

**Full** refreshes read every row. **Incremental** refreshes need a connector that supports them (SQL Server and Elasticsearch), at least one identifier field and an incremental field (a modification date or a version number): the published rows are copied into the new version, only the rows whose incremental field is after the last refresh's largest value are read, and they replace the rows with the same identifiers. A row deleted at the source stays until the next full refresh. A refresh whose source fields changed reads every row.

When the source's fields change, **Accept** adds the new fields, follows a changed source type (unless an administrator chose another type), and keeps a removed field with empty values so the consumers that use it keep working; **Fail** fails the refresh and keeps the published rows. The history lists what changed.

Rows are stored as JSON objects keyed by the data set's field names, so a schema change needs no table change. Reads page through a version by its row id, never loading a data set into memory. A single identifier field filtered by **Equals** or **In** is read by key.

## Live reads

A live read maps the data set's fields to the connector's source fields, coerces each value to its field's type, re-batches the rows and stops at the consumer's row limit. Conditions are passed to the connector only when it declares `ConditionPushdown`, and only on fields that read the source's own type. A connector applies a condition only when doing so can't drop a row that matches it, because consumers filter the rows again:

| Connector | Applied by the source |
| --- | --- |
| SQL Server | Number, date and boolean ranges, number **Equals**/**In**, the row limit (`TOP`) |
| Elasticsearch | Ranges on number and date fields |
| HTTP API, media files | None |

Text conditions are never applied by a database or an index: a collation or an analyzer may not compare case the way consumers do.

Every live read and every listing of resources has a timeout (`LiveReadTimeout`).

## Security

| Permission | Allows |
| --- | --- |
| Manage data connections and their credentials (`ManageDataConnections`) | Create, edit, check and delete connections. Security critical. |
| Manage connected data sets (`ManageConnectedDataSets`) | Create, edit, preview and delete data sets. |
| Refresh imported data sets (`RefreshConnectedDataSets`) | Refresh on demand; implied by **Manage connected data sets**. |
| Read every connected data set (`ReadAllConnectedDataSets`) | Read every data set from reports and pipelines. |
| Read the connected data set - *name* (`ReadConnectedDataSet_{Name}`) | Read one data set; implied by **Read every connected data set**. |

Administrators have the first four by default.

- **One rule for live and imported data.** A data set is listed, described and read only when the `DataSourceContext.User` holds its read permission. An import runs with the connection's credentials and no user, and never goes through a data source, so copying the rows locally never lets anyone read them who could not read the live data set. A context with no user reads nothing, as with Orchard Core's own data sources.
- **Secrets** (passwords, API keys, bearer tokens, client secrets) are protected with the tenant's data protection keys under the purpose `CrestApps.DataConnectors.Secrets`, are write-only in the editors (an empty box keeps the stored secret), and never appear in logs or error messages. A SQL Server password is never part of the stored settings.
- **Outbound network.** HTTP-based connectors and the SQL Server connector refuse hosts that resolve to loopback, private, link-local, shared (100.64/10), multicast or unspecified addresses, checked on the address when the connection is opened, so a name that later resolves to an internal address is still refused. Allow internal hosts with `Network:AllowedHosts`, or every internal address with `Network:AllowPrivateNetworks`.
- **Redirects** are followed only on the same scheme, host and port, at most three times, and never with automatic redirection. A next-page link must stay on the connection's server, and a data set's path is resolved against the connection's base URL.
- **Token endpoints and OpenID Connect issuers** must use HTTPS. Tokens are cached per tenant and connection until a minute before they expire, and a request answered with 401 asks for a new token once.
- **SQL.** Tables are quoted with the connector's identifier rules, values are always bound as parameters, and every read runs in a transaction that is always rolled back. A custom query must be a single `SELECT` expression: it runs as a derived table, and a query whose parentheses don't balance, that holds a `;` or a `--` comment outside quotes, or that doesn't start with `SELECT` is refused. Give the connection a login that can only read what its data sets need: a connector can't take away rights its login has, and whoever can edit a data set can query anything that login can read.
- Responses larger than `Network:MaxResponseBytes` are refused.

## Connectors

### HTTP API

The connection holds the base URL, the authentication, extra headers and the request timeout. The data set's resource is a path (with an optional query string) appended to the base URL.

| Authentication | Sends |
| --- | --- |
| None | Nothing |
| API key | The key in a header (with an optional prefix, such as `ApiKey `) or a query parameter |
| Basic | `Authorization: Basic` with a user name and password |
| Bearer token | `Authorization: Bearer` with a fixed token |
| OAuth 2.0 | A token from the token endpoint, with the client credentials or the password grant; the client authenticates with `client_secret_basic` or `client_secret_post`; optional scopes and audience |
| OpenID Connect | The same, with the token endpoint read from the issuer's `/.well-known/openid-configuration` |

| Paging | Reads |
| --- | --- |
| One response | A single request |
| Page number | `?page=1,2,…` (parameter, first page and page size parameter configurable) |
| Offset | `?offset=0,100,…` |
| Cursor | The cursor at a path of the response, sent back as a parameter |
| Next link | The URL of the next page at a path of the response |
| `Link` header | The `rel="next"` URL of the response's `Link` header |

Paging stops at an empty page, at a short page when a page size is sent, at **Most pages**, or at the consumer's row limit. Records are found at a dotted path (`data.items`, `results[0].rows`); nested objects become fields named by their path (`address.city`) and arrays are kept as JSON text. `POST` requests send a JSON body in which `{page}`, `{offset}`, `{limit}` and `{cursor}` are replaced by the paging values. In the path and the body, `{param:Name}` is replaced by a parameter the consumer passes, such as a parameter of a **Read a data source** step. A response in CSV or JSON Lines is read through the data source file formats. Fields are inferred from up to 200 records of the first two pages.

Throttled and failed requests (408, 429, 500, 502, 503, 504, and connection failures) are tried up to four times, waiting as long as `Retry-After` says, or 1, 2 and 4 seconds, and at most 30 seconds.

### Elasticsearch

The connection holds the cluster URL and its authentication (none, Basic, API key in `Authorization: ApiKey …`, or a bearer token). The data set's resource is an index, an alias or a pattern such as `logs-*`, with an optional query clause in the query DSL. Indices and aliases are listed with `_cat/indices` and `_cat/aliases`, without system indices. Fields come from `_mapping`: numbers, dates and booleans keep their types, objects are flattened by path, and `_id` is the identifier. Documents are read with the scroll API, sorted by `_doc`, and the scroll is cleared when the read ends. It works with Elasticsearch 7 to 9 and OpenSearch.

### Media files

The connection names a folder of the media library (or the whole library); the data set's resource is a file path in it, which can't leave the folder. Any file format registered with the data sources reads: CSV (delimiter and header row configurable), JSON, JSON Lines and Excel (sheet configurable). Field types are inferred from up to 200 rows; a number with a leading zero, such as a postal code, stays text.

### SQL Server

The connection holds the server (`host`, `host,port`, `host\instance`), the database and the sign-in: a SQL login, the site's Windows account, a Microsoft Entra service principal, the host's managed identity, or the default Entra credential. It can ask for a read-only replica (`ApplicationIntent=ReadOnly`). Tables and views come from `INFORMATION_SCHEMA.TABLES`; fields come from the result's schema without reading rows, with the primary key as the identifier. Transient errors (timeouts, deadlocks, Azure SQL throttling) are marked so a later refresh retries.

## Configuration

| Setting | Default | Meaning |
| --- | --- | --- |
| `RefreshTimeout` | `00:30:00` | The longest a refresh runs before it fails. |
| `LiveReadTimeout` | `00:05:00` | The longest a live read, a check or a listing of resources runs. |
| `ImportBatchSize` | `1000` | The rows a refresh reads and writes at a time. |
| `RefreshRunsKept` | `25` | The refreshes kept in each data set's history. |
| `MaxRefreshesPerRun` | `10` | The most refreshes one run of the background task does. |
| `PreviewRows` | `20` | The rows the preview shows. |
| `Network:AllowPrivateNetworks` | `false` | Let connectors reach the site's own network. |
| `Network:AllowedHosts` | none | Hosts connectors may reach on any network. |
| `Network:MaxResponseBytes` | `104857600` | The largest HTTP response a connector reads. |

```json
{
  "OrchardCore": {
    "CrestApps": {
      "DataConnectors": {
        "RefreshTimeout": "01:00:00",
        "Network": {
          "AllowedHosts": [ "sql01.corp.example.com", "search.internal" ]
        }
      }
    }
  }
}
```

```bash
OrchardCore__CrestApps__DataConnectors__RefreshTimeout=01:00:00
OrchardCore__CrestApps__DataConnectors__Network__AllowedHosts__0=sql01.corp.example.com
OrchardCore__CrestApps__DataConnectors__Network__AllowPrivateNetworks=true
```

In a tenant's own `App_Data/Sites/{tenant}/appsettings.json`, leave out the `OrchardCore` wrapper.

## Write a connector

A connector is a class in any module; nothing in the Data Connectors modules changes.

```csharp
public sealed class InventoryApiConnector : IDataConnector
{
    public static string Name => "InventoryApi";

    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<InventoryApiConnector>("Inventory API");

    public static LocalizationSource Description { get; } = LocalizationSource.Create<InventoryApiConnector>("Reads the stock levels of the inventory system.");

    public static DataConnectorCapabilities Capabilities => DataConnectorCapabilities.Live | DataConnectorCapabilities.Import;

    public Task<DataConnectorTestResult> TestAsync(DataConnectorContext context, CancellationToken cancellationToken = default) { ... }

    public Task<IReadOnlyList<DataConnectorResource>> DiscoverAsync(DataConnectorContext context, CancellationToken cancellationToken = default) { ... }

    public Task<IReadOnlyList<DataField>> DescribeAsync(DataConnectorReadContext context, CancellationToken cancellationToken = default) { ... }

    public async IAsyncEnumerable<DataBatch> ReadAsync(DataConnectorReadContext context, [EnumeratorCancellation] CancellationToken cancellationToken = default) { ... }
}
```

```csharp
[Feature("Contoso.InventoryConnector")]
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDataConnector<InventoryApiConnector>();
        services.AddDisplayDriver<DataConnection, InventoryConnectionDisplayDriver>();
    }
}
```

- Read the connection's settings with `context.Connection.GetOrCreate<TSettings>()` and the data set's with `context.DataSet.GetOrCreate<TSettings>()`; a driver for `DataConnection` or `ConnectedDataSet` that checks `connection.Connector` edits them. Store secrets with `IDataConnectionSecretManager.Set` and read them with `context.Secrets.Get`.
- Return rows keyed by your own field names, the `Name`s `DescribeAsync` returns. The infrastructure maps them to the data set's fields, coerces the values, keeps the fields asked for and enforces the row limit.
- Stream batches of `context.BatchSize` rows, honor the cancellation token, and stop at `context.MaxRows` when it is set.
- Treat `context.Conditions` as hints and apply one only when it can't drop a matching row. Declare `ConditionPushdown` only if you apply some.
- Apply `context.WatermarkField` and `context.Watermark` (rows after the value) when you declare `IncrementalImport`.
- Throw `DataConnectorException` with a message safe to show, and set `IsTransient` for an error a later try may not have.
- Send HTTP requests through `DataConnectorHttpClient` to get the network rule, the authentication kinds, retries and redirect checks; a relational database is a dialect on `RelationalDataConnector` (connect, quote, list tables, version, and the row limit when it isn't `LIMIT`).

## Refresh from code

```csharp
var dataSet = await dataSetStore.FindByNameAsync("Customers");
var run = await refreshService.RefreshAsync(dataSet, DataSetRefreshTrigger.Manual, User.Identity?.Name);
```

`IDataSetRefreshService.RefreshDueAsync` refreshes the data sets that are due or that someone asked to refresh, as the background task does.
