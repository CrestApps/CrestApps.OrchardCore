# AI Data Sources - PostgreSQL

Adds PostgreSQL source support for CrestApps Orchard Core AI data sources so operators can read source documents from PostgreSQL tables and sync them into an AI knowledge base index.

## Features

- Registers **PostgreSQL** as an AI data source source type.
- Adds an editor for PostgreSQL connection strings and table names.
- Reads PostgreSQL rows into the AI knowledge base indexing pipeline.
- Falls back to a globally configured connection string when a data source does not define its own.

## Usage

1. Enable **AI Data Sources** and **AI Data Sources - PostgreSQL**.
2. Create or edit an AI data source.
3. Select **PostgreSQL** as the source type.
4. Enter the PostgreSQL connection string, table name, and field mappings.
5. Save the data source and trigger synchronization.

## Global connection string

Configure a connection string once and data sources can use it instead of storing their own:

```json
{
  "OrchardCore": {
    "CrestApps": {
      "PostgreSQL": {
        "ConnectionString": "Host=localhost;Port=5432;Username=postgres;Password=postgres;Database=vectordb"
      }
    }
  }
}
```

`CrestApps:AI:DataSources:PostgreSQL:ConnectionString` overrides the shared value for data sources only, and a connection string stored on a data source overrides both.
