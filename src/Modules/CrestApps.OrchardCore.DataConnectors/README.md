# Data Connectors

Brings data from external systems into Orchard Core's data sources, so reports, data pipelines and every other `IDataSourceManager` consumer read it with no code of their own.

- A **connection** is how the site reaches one system: its connector, its server and its credentials.
- A **data set** reads one resource of a connection (a table, an index, an API path, a file), and is read **live** (every read goes to the system) or **imported** (the rows are copied into the tenant's database on a schedule and read from there).

Both kinds are exposed through two data sources, **Connected data (live)** (`ConnectedData`) and **Connected data (imported)** (`ImportedData`).

## Features

| Feature | ID | Adds |
| --- | --- | --- |
| Data Connectors | `CrestApps.OrchardCore.DataConnectors` | Connections, data sets, the two data sources, the refresh task, **Tools › Data Connectors** |
| HTTP API Data Connector | `CrestApps.OrchardCore.DataConnectors.Http` | Any HTTP/REST API answering JSON, CSV or JSON Lines; API key, Basic, bearer, OAuth 2.0 and OpenID Connect; page, offset, cursor, next-link and `Link` header paging |
| Elasticsearch Data Connector | `CrestApps.OrchardCore.DataConnectors.Elasticsearch` | Elasticsearch and OpenSearch indices and aliases |
| Media Files Data Connector | `CrestApps.OrchardCore.DataConnectors.Files` | CSV, JSON, JSON Lines and Excel files of the media library |
| SQL Server Data Connector | `CrestApps.OrchardCore.DataConnectors.SqlServer` (module `CrestApps.OrchardCore.DataConnectors.SqlServer`) | Tables, views and read-only queries of SQL Server and Azure SQL |

The base feature depends on Orchard Core's `OrchardCore.DataPipelines.Core`.

## Configuration

```json
{
  "OrchardCore": {
    "CrestApps": {
      "DataConnectors": {
        "RefreshTimeout": "00:30:00",
        "LiveReadTimeout": "00:05:00",
        "ImportBatchSize": 1000,
        "RefreshRunsKept": 25,
        "MaxRefreshesPerRun": 10,
        "PreviewRows": 20,
        "Network": {
          "AllowPrivateNetworks": false,
          "AllowedHosts": [ "sql01.corp.example.com" ],
          "MaxResponseBytes": 104857600
        }
      }
    }
  }
}
```

By default a connector can't reach the site's own network (loopback, private, link-local and shared addresses). Name the internal hosts a tenant needs in `AllowedHosts`, or set `AllowPrivateNetworks` for a site that runs inside a private network.

## Security

- **Manage data connections and their credentials** guards connections and secrets; **Manage connected data sets**, **Refresh imported data sets** and **Read every connected data set** guard the rest, and each data set has its own **Read the connected data set - {name}** permission.
- A data set is listed and read only for a user holding its read permission, whether it is live or imported, so an import made with the connection's credentials never shows its rows to someone who couldn't read the live data set. No user reads nothing.
- Secrets are stored protected with the tenant's data protection keys, are write-only in the editors, and are never logged.

## Writing a connector

Implement `IDataConnector` (static `Name`, `DisplayName`, `Description`, `Capabilities`; `TestAsync`, `DiscoverAsync`, `DescribeAsync`, `ReadAsync`), register it with `services.AddDataConnector<TConnector>()` in your module's startup, and add display drivers for `DataConnection` and `ConnectedDataSet` that edit its settings. A relational database is a few members on `RelationalDataConnector`. See the documentation site for the full guide.
