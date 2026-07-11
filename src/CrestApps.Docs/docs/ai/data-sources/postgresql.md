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

## Getting started

1. Enable **AI Data Sources** and **AI Data Sources - PostgreSQL**.
2. Create an AI knowledge base index under **Search > Indexing**.
3. Create a new data source under **Artificial Intelligence > Data Sources**.
4. Choose **PostgreSQL** as the **Source type**.
5. Enter the PostgreSQL connection string and source table name.
6. Map the key, title, and content fields, then save the data source.

## Notes

- Table names can include schema-qualified names such as `public.articles`.
- Store the connection string securely and avoid committing it to source control.
- Incremental Orchard content-event sync only applies to Orchard-managed source index profiles. External PostgreSQL sources are intended for full sync or provider-specific reprocessing flows.
