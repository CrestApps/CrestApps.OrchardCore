---
sidebar_label: PostgreSQL
sidebar_position: 4
title: AI Data Sources - PostgreSQL
description: PostgreSQL source support for AI data sources and knowledge base indexing.
---

| | |
| --- | --- |
| **Feature Name** | AI Data Sources - PostgreSQL |
| **Feature ID** | `CrestApps.OrchardCore.AI.DataSources.PostgreSQL` |

Adds PostgreSQL source support for AI data sources.

## Overview

This module lets AI data sources read documents directly from a PostgreSQL table instead of an Orchard-managed search index profile. It is useful when your knowledge-base pipeline needs to pull source records from an external relational store while still writing embeddings into an Orchard-managed AI knowledge base index.

The PostgreSQL source editor captures:

- **Connection string**
- **Table name**
- **Key field**
- **Title field**
- **Content field**

The handler reads rows in batches, supports targeted reads by key for explicit reprocessing, and falls back to the `id` column when no key field is configured.

## Global connection string

A connection string can be configured once for the whole application instead of on every data source. When one is configured, the source editor offers **Use the globally configured connection string**, and a data source that uses it stores no connection string of its own.

Configure it under the shared PostgreSQL section, which every PostgreSQL feature reads:

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

To point data sources at a different server than the rest of the application, override the shared value under the data source section:

```json
{
  "OrchardCore": {
    "CrestApps": {
      "AI": {
        "DataSources": {
          "PostgreSQL": {
            "ConnectionString": "Host=reporting;Port=5432;Username=postgres;Password=postgres;Database=reporting"
          }
        }
      }
    }
  }
}
```

The same values can be supplied as environment variables, which is how the Aspire host wires its local PostgreSQL container:

```text
OrchardCore__CrestApps__PostgreSQL__ConnectionString
OrchardCore__CrestApps__AI__DataSources__PostgreSQL__ConnectionString
```

A connection string stored on a data source always wins over the configured values.

## Getting started

1. Enable **AI Data Sources** and **AI Data Sources - PostgreSQL**.
2. Create an AI knowledge base index under **Search > Indexing**.
3. Create a new data source under **Artificial Intelligence > Data Sources**.
4. Choose **PostgreSQL** as the **Source type**.
5. Enter the PostgreSQL connection string and source table name.
6. Map the key, title, and content fields, then save the data source.

## Notes

- Table names can include schema-qualified names such as `public.articles`.
- Store the connection string securely and avoid committing it to source control. Use user secrets or environment variables for the configured values.
- Incremental Orchard content-event sync only applies to Orchard-managed source index profiles. External PostgreSQL sources are intended for full sync or provider-specific reprocessing flows.
