---
sidebar_label: PostgreSQL
sidebar_position: 4
title: AI Data Sources - PostgreSQL
description: PostgreSQL source support for AI data sources and knowledge base indexing.
user_manual:
  - user-manual/ai/knowledge
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

### Which tenants may use it

The global connection string signs in with the host's credentials, so it reaches every table they can read, other tenants' included. The default tenant may read any table through it. Another tenant may read only the tables the host lists for it in `SharedConnectionTables` under the data source section, named as the data source names them (`table` or `schema.table`, compared by case, as PostgreSQL compares the quoted name), when a data source is saved and again before every sync. The operator lists them in the tenant's own configuration (for example `App_Data/Sites/<tenant>/appsettings.json`, or the `OrchardCore:<tenant>` section of the host's configuration), which tenant administrators can't change:

```json
{
  "CrestApps": {
    "AI": {
      "DataSources": {
        "PostgreSQL": {
          "SharedConnectionTables": [ "tenant_a.articles" ]
        }
      }
    }
  }
}
```

A data source on another tenant that already reads a table through the global connection string stops syncing until its table is listed, or until it is given its own connection string.

## A data source's own connection string

A connection string a data source stores is checked when the data source is saved (editor, API and **AIDataSource** recipe step) and again before every sync:

- It names one server by its host name or address: not a list of servers, a `host:port` pair (use `Port`) or a local socket path.
- The server may not be on the site's own network (loopback, private or link-local addresses, or a host name that resolves to one), unless the host sets `CrestApps:AI:Egress:AllowPrivateNetwork` to `true`; the cloud metadata services stay refused. The client connects to the address that was checked, not to whatever the name resolves to a moment later, and still checks the server's certificate against the host name (`SSL Mode=VerifyFull`).
- Outside the default tenant, it may not sign in as the host. It needs a password; it may not name a file on the server (`Passfile`, `SSL Certificate`, `SSL Key`, `SSL Password` or `Root Certificate`); and the client never uses the host's Kerberos ticket or Windows account, its default password file or its default client certificate. The host can allow this for a tenant with `AllowHostIdentity` under the data source section.

A data source saved before these checks that breaks one of them stops syncing until its connection string is fixed: a server on a private network needs `AllowPrivateNetwork`, and, outside the default tenant, a server that asks for no password needs a password in the connection string all the same.

## Getting started

1. Enable **AI Data Sources** and **AI Data Sources - PostgreSQL**.
2. Create an AI knowledge base index under **Search** → **Indexing**.
3. Create a new data source under **Artificial Intelligence** → **Data Sources**.
4. Choose **PostgreSQL** in the **Available Source Types** dialog.
5. Enter the **Connection string** (or tick **Use the globally configured connection**) and the **Table name**.
6. Map the **Key field**, **Title field**, and **Content field**, then save the data source.

The data source editor is described in the User Manual under [Data sources](../../user-manual/ai/knowledge.md#data-sources).

## Notes

- Table names can include schema-qualified names such as `public.articles`.
- Store the connection string securely and avoid committing it to source control. Use user secrets or environment variables for the configured values.
- Incremental Orchard content-event sync only applies to Orchard-managed source index profiles. External PostgreSQL sources are intended for full sync or provider-specific reprocessing flows.
