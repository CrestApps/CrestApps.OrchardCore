---
sidebar_label: Azure AI Search
sidebar_position: 3
title: Azure AI Search Data Source
description: Configure Azure AI Search as a vector search backend for RAG-powered AI experiences.
---

# Azure AI Search Data Source

> Use Azure AI Search as a vector search backend for retrieval-augmented generation.

## Quick Start

```csharp
builder.Services.AddAzureAISearchDataSourceServices(
    builder.Configuration.GetSection("CrestApps:Search:AzureAISearch"));
```

Or without configuration binding:

```csharp
builder.Services.AddAzureAISearchDataSourceServices();
```

## Configuration

### `appsettings.json`

```json
{
  "CrestApps": {
    "Search": {
      "AzureAISearch": {
        "Endpoint": "https://my-search.search.windows.net",
        "ApiKey": "your-admin-api-key"
      }
    }
  }
}
```

### `AzureAISearchConnectionOptions`

| Property | Type | Description |
|----------|------|-------------|
| `Endpoint` | `string` | Azure AI Search endpoint URL |
| `ApiKey` | `string` | Admin API key. If empty, uses `DefaultAzureCredential` |

## Services Registered (Keyed by `"AzureAISearch"`)

| Service | Implementation |
|---------|---------------|
| `IDataSourceContentManager` | `AzureAISearchDataSourceContentManager` |
| `IDataSourceDocumentReader` | `DataSourceAzureAISearchDocumentReader` |
| `IODataFilterTranslator` | `AzureAIODataFilterTranslator` |
| `ISearchIndexManager` | `AzureAISearchIndexManager` |
| `ISearchDocumentManager` | `AzureAISearchDocumentManager` |
| `IVectorSearchService` | `AzureAISearchVectorSearchService` |

When the `Endpoint` is provided, a `SearchIndexClient` singleton is also registered.

## Authentication

- **API Key** — Provide the `ApiKey` property
- **Azure AD** — Leave `ApiKey` empty and the service uses `DefaultAzureCredential` (Managed Identity, VS credentials, etc.)

## Azure Setup

### Creating an Azure AI Search Resource

1. Go to the [Azure Portal](https://portal.azure.com/) and search for **"AI Search"**.
2. Click **Create** and fill in:
   - **Resource group**: Select or create one
   - **Service name**: A globally unique name (e.g., `myapp-search`)
   - **Location**: Choose a region close to your application
   - **Pricing tier**: **Basic** or higher is required for vector search support

:::warning
The **Free** tier does not support vector search (semantic ranking). Use **Basic** or higher for RAG workloads.
:::

3. After deployment, navigate to the resource and note the **URL** (e.g., `https://myapp-search.search.windows.net`).
4. Under **Settings → Keys**, copy the **Primary admin key** for API key authentication.

## Authentication Options

### Option 1: API Key Authentication

The simplest approach — provide the admin API key directly:

```json title="appsettings.json"
{
  "CrestApps": {
    "Search": {
      "AzureAISearch": {
        "Endpoint": "https://myapp-search.search.windows.net",
        "ApiKey": "your-admin-api-key"
      }
    }
  }
}
```

:::tip
Store the API key in environment variables, Azure Key Vault, or User Secrets — not in source-controlled configuration files.
:::

### Option 2: DefaultAzureCredential (Recommended for Production)

Leave `ApiKey` empty and the service uses `DefaultAzureCredential`, which automatically tries multiple authentication methods:

```json title="appsettings.json"
{
  "CrestApps": {
    "Search": {
      "AzureAISearch": {
        "Endpoint": "https://myapp-search.search.windows.net"
      }
    }
  }
}
```

`DefaultAzureCredential` tries these methods in order:

1. **Environment variables** (`AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_CLIENT_SECRET`)
2. **Managed Identity** (when running in Azure App Service, Azure Functions, etc.)
3. **Visual Studio / VS Code credentials** (for local development)
4. **Azure CLI** (`az login`)

To use Managed Identity:
1. Enable system-assigned managed identity on your Azure App Service.
2. In the Azure AI Search resource, go to **Access Control (IAM)** → **Add role assignment**.
3. Assign the **Search Index Data Contributor** role to the managed identity.

## Configuration Reference

### Full `appsettings.json` Example

```json
{
  "CrestApps": {
    "Search": {
      "AzureAISearch": {
        "Endpoint": "https://myapp-search.search.windows.net",
        "ApiKey": "your-admin-api-key"
      }
    }
  }
}
```

### `AzureAISearchConnectionOptions` — All Properties

| Property | Type | Required | Default | Description |
|----------|------|----------|---------|-------------|
| `Endpoint` | `string` | Yes | — | Azure AI Search endpoint URL. Format: `https://{service-name}.search.windows.net` |
| `ApiKey` | `string` | No | — | Admin API key. When empty, `DefaultAzureCredential` is used for authentication. |

:::info
When `Endpoint` is provided, the framework registers a `SearchIndexClient` singleton that all keyed services share. If `Endpoint` is empty or null, no client is registered and the data source is effectively disabled.
:::

## Verification

After configuring the connection, verify it is working:

### 1. Check Service Health via REST

```bash
curl -H "api-key: your-admin-api-key" \
     "https://myapp-search.search.windows.net/indexes?api-version=2024-07-01"
```

A successful response returns a JSON list of indexes (possibly empty).

### 2. Verify from the Application

Inject `ISearchIndexManager` (keyed by `"AzureAISearch"`) and check if the connection is live:

```csharp
public sealed class AzureSearchHealthCheck
{
    private readonly ISearchIndexManager _indexManager;

    public AzureSearchHealthCheck(
        [FromKeyedServices("AzureAISearch")] ISearchIndexManager indexManager)
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

### 3. Check via Azure Portal

Navigate to your Azure AI Search resource → **Indexes** to see all created indexes, their document counts, and storage sizes.

## Troubleshooting

### 401 Unauthorized

**Error:** `Azure.RequestFailedException: Status: 401 (Unauthorized)`

**Cause:** Invalid API key or insufficient permissions for managed identity.

**Fix:**
- Verify the API key in `appsettings.json` matches the key in Azure Portal → **Settings → Keys**
- If using `DefaultAzureCredential`, ensure the identity has the **Search Index Data Contributor** role
- Check that the endpoint URL is correct

### 403 Forbidden

**Error:** `Azure.RequestFailedException: Status: 403 (Forbidden)`

**Cause:** The API key has read-only permissions, or RBAC role is insufficient.

**Fix:**
- Use the **Admin key** (not the query key) for write operations
- If using RBAC, assign **Search Index Data Contributor** (not just **Reader**)

### Service Not Found

**Error:** `No such host is known`

**Cause:** The endpoint URL is incorrect or the service does not exist.

**Fix:**
- Verify the `Endpoint` value matches the Azure portal
- Ensure the format is `https://{service-name}.search.windows.net` (no trailing slash, no path)

### Vector Search Not Available

**Error:** `Semantic search is not supported on this service tier`

**Cause:** The Azure AI Search resource is on the Free tier.

**Fix:**
- Upgrade to **Basic** or higher pricing tier in the Azure Portal
- Vector search features require at least the Basic tier

### Index Creation Fails

**Error:** `Index field with vector search configuration requires a vector search profile`

**Cause:** The index schema references a vector search profile that does not exist.

**Fix:**
- This is typically handled automatically by the framework. If you see this error, ensure you are using the latest version of the data source services.
- Manually verify the index schema in the Azure Portal under your search resource → **Indexes**.

## Orchard Core Integration

The [Azure AI Search data source module](../../ai/data-sources/azure-ai.md) adds admin UI for configuring Azure AI Search connections and mapping content types to indices.
