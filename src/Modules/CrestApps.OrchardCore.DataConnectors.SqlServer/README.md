# Data Connectors - SQL Server

Adds the **SQL Server Data Connector** (`CrestApps.OrchardCore.DataConnectors.SqlServer`) to [Data Connectors](../CrestApps.OrchardCore.DataConnectors/README.md): data sets that read the tables, views and read-only queries of any SQL Server or Azure SQL database, live or imported.

It is its own module so a tenant that reads no database never loads the SQL client.

## Connection settings

| Setting | What it does |
| --- | --- |
| Server | `host`, `host,port` or `host\instance` |
| Database | The database read |
| Sign in with | A SQL login, the site's Windows account, a Microsoft Entra service principal, the host's managed identity, or the default Entra credential |
| Login or client ID / Password or client secret | The credentials; the secret is stored protected and never shown again |
| Encrypt, Trust the server's certificate, Read from a read-only replica, Connect timeout | Connection options |

## Reading

- Tables and views are listed from `INFORMATION_SCHEMA.TABLES`; a table's primary key becomes the data set's identifier.
- A custom query must be one `SELECT`; it runs as a derived table inside a transaction that is always rolled back, with read parameters bound as `@Name`.
- Number, date and boolean comparisons and the row limit (`TOP`) are applied by the server; text conditions are left to the consumer, because a collation may compare case.
- An incremental import reads the rows whose incremental column is after the last import's largest value.

Give the connection a login that can only read what its data sets need. The server goes through the Data Connectors network rule, so a server on the site's own network needs `CrestApps:DataConnectors:Network:AllowedHosts` or `AllowPrivateNetworks`.
