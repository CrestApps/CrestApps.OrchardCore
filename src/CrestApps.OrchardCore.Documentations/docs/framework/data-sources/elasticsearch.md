---
sidebar_label: Elasticsearch
sidebar_position: 2
title: Elasticsearch Provider
description: Configure Elasticsearch as a vector search backend for RAG-powered AI experiences.
---

# Elasticsearch Provider

> Use Elasticsearch as a vector search backend for retrieval-augmented generation.

## Quick Start

```csharp
builder.Services.AddElasticsearchServices(
    builder.Configuration.GetSection("CrestApps:Elasticsearch"));
```

Or without configuration binding:

```csharp
builder.Services.AddElasticsearchServices();
```

## Configuration

### `appsettings.json`

```json
{
  "CrestApps": {
    "Search": {
      "Elasticsearch": {
        "Url": "https://localhost:9200",
        "Username": "elastic",
        "Password": "your-password",
        "CertificateFingerprint": "AA:BB:CC:..."
      }
    }
  }
}
```

### `ElasticsearchConnectionOptions`

| Property | Type | Description |
|----------|------|-------------|
| `Url` | `string` | Elasticsearch endpoint URL |
| `Username` | `string` | Basic auth username (optional) |
| `Password` | `string` | Basic auth password (optional) |
| `CertificateFingerprint` | `string` | TLS certificate fingerprint for verification (optional) |

## Services Registered (Keyed by `"Elasticsearch"`)

| Service | Implementation |
|---------|---------------|
| `IDataSourceContentManager` | `ElasticsearchDataSourceContentManager` |
| `IDataSourceDocumentReader` | `DataSourceElasticsearchDocumentReader` |
| `IODataFilterTranslator` | `ElasticsearchODataFilterTranslator` |
| `ISearchIndexManager` | `ElasticsearchSearchIndexManager` |
| `ISearchDocumentManager` | `ElasticsearchSearchDocumentManager` |
| `IVectorSearchService` | `ElasticsearchVectorSearchService` |

When the `Url` is provided, an `ElasticsearchClient` singleton is also registered.

## Docker Setup for Local Development

Use Docker Compose to run Elasticsearch locally with vector search support:

```yaml title="docker-compose.yml"
services:
  elasticsearch:
    image: docker.elastic.co/elasticsearch/elasticsearch:8.15.0
    container_name: elasticsearch
    environment:
      - discovery.type=single-node
      - xpack.security.enabled=true
      - xpack.security.http.ssl.enabled=false
      - ELASTIC_PASSWORD=changeme
    ports:
      - "9200:9200"
    volumes:
      - es-data:/usr/share/elasticsearch/data
    mem_limit: 2g

volumes:
  es-data:
    driver: local
```

Start it with:

```bash
docker compose up -d
```

:::tip
Setting `xpack.security.http.ssl.enabled=false` simplifies local development by disabling HTTPS. For production, always enable TLS and use the `CertificateFingerprint` option.
:::

Then configure your `appsettings.Development.json`:

```json
{
  "CrestApps": {
    "Search": {
      "Elasticsearch": {
        "Url": "http://localhost:9200",
        "Username": "elastic",
        "Password": "changeme"
      }
    }
  }
}
```

## Configuration Reference

### Full `appsettings.json` Example

```json
{
  "CrestApps": {
    "Search": {
      "Elasticsearch": {
        "Url": "https://my-cluster.es.us-east-1.aws.found.io:9243",
        "Username": "elastic",
        "Password": "your-secure-password",
        "CertificateFingerprint": "AA:BB:CC:DD:EE:FF:00:11:22:33:44:55:66:77:88:99:AA:BB:CC:DD:EE:FF:00:11:22:33:44:55:66:77:88:99"
      }
    }
  }
}
```

### `ElasticsearchConnectionOptions` — All Properties

| Property | Type | Required | Default | Description |
|----------|------|----------|---------|-------------|
| `Url` | `string` | Yes | — | Elasticsearch endpoint URL. Include the port if non-standard (e.g., `https://localhost:9200`). |
| `Username` | `string` | No | — | Username for basic authentication. Typically `"elastic"` for the built-in superuser. |
| `Password` | `string` | No | — | Password for basic authentication. |
| `CertificateFingerprint` | `string` | No | — | SHA-256 fingerprint of the Elasticsearch TLS certificate. Required when using self-signed certificates. Format: `AA:BB:CC:...` |

:::info
When `Url` is provided, the framework registers an `ElasticsearchClient` singleton that all keyed services share. If `Url` is empty or null, no client is registered and the data source is effectively disabled.
:::

## Verification

After configuring the connection, verify it is working:

### 1. Check Elasticsearch Health

```bash
curl -u elastic:changeme http://localhost:9200/_cluster/health?pretty
```

Expected output should show `"status": "green"` or `"yellow"`.

### 2. Verify from the Application

Inject `ISearchIndexManager` (keyed by `"Elasticsearch"`) and check if the connection is live:

```csharp
public sealed class ElasticsearchHealthCheck
{
    private readonly ISearchIndexManager _indexManager;

    public ElasticsearchHealthCheck(
        [FromKeyedServices("Elasticsearch")] ISearchIndexManager indexManager)
    {
        _indexManager = indexManager;
    }

    public async Task<bool> IsHealthyAsync()
    {
        // Attempt to check if a known index exists
        return await _indexManager.ExistsAsync("_test_ping");
    }
}
```

### 3. Check Indexes via Elasticsearch API

```bash
# List all indexes
curl -u elastic:changeme http://localhost:9200/_cat/indices?v

# Check a specific index mapping
curl -u elastic:changeme http://localhost:9200/your-index-name/_mapping?pretty
```

## Index Management

Indexes are created automatically when a data source is configured and content is indexed for the first time. The `ElasticsearchSearchIndexManager` handles index lifecycle:

- **Creation** — `CreateAsync()` defines the index schema with vector fields (dense_vector type), content fields, and filter fields.
- **Existence check** — `ExistsAsync()` verifies an index is present before querying.
- **Deletion** — `DeleteAsync()` removes an index and all its data.

Index names are generated from the data source configuration and include the tenant prefix in multi-tenant environments.

:::warning
Deleting an index removes all indexed documents permanently. Re-indexing from the data source is required after deletion.
:::

## Troubleshooting

### Connection Refused

**Error:** `Elasticsearch.Net.ElasticsearchClientException: Connection refused`

**Cause:** Elasticsearch is not running or the URL is incorrect.

**Fix:**
- Verify Elasticsearch is running: `docker ps` or `curl http://localhost:9200`
- Check the `Url` in `appsettings.json` matches the actual endpoint
- Ensure the port is correct (default: 9200)

### Authentication Failed

**Error:** `Elasticsearch.Net.ElasticsearchClientException: 401 Unauthorized`

**Cause:** Invalid username or password.

**Fix:**
- Verify credentials in `appsettings.json`
- Reset the elastic user password: `docker exec -it elasticsearch bin/elasticsearch-reset-password -u elastic`

### Certificate Error

**Error:** `The SSL connection could not be established`

**Cause:** TLS certificate mismatch when connecting to an HTTPS endpoint.

**Fix:**
- Provide the correct `CertificateFingerprint` in configuration
- For local development, disable TLS in Elasticsearch or use `http://` instead of `https://`
- Get the fingerprint: `openssl s_client -connect localhost:9200 | openssl x509 -fingerprint -sha256 -noout`

### Index Not Found

**Error:** `index_not_found_exception` when querying

**Cause:** The index has not been created yet, or the index name is incorrect.

**Fix:**
- Trigger indexing from the admin UI or via the data source management API
- Verify the index name matches what the application expects: `curl -u elastic:changeme http://localhost:9200/_cat/indices?v`

## Orchard Core Integration

The [Elasticsearch data source module](../../ai/data-sources/elasticsearch.md) adds admin UI for configuring Elasticsearch connections and mapping content types to indices.
