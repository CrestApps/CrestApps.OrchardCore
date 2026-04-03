---
sidebar_label: "Migration: Typed Deployments"
sidebar_position: 11
title: Migrating to Typed AI Deployments
description: Guide for migrating from connection-based deployment names to the new typed AIDeployment system.
---

# Migrating to Typed AI Deployments

## What Changed

Previously, AI model deployments were configured as string properties on `AIProviderConnection` — for example, `ChatDeploymentName`, `UtilityDeploymentName`, `EmbeddingDeploymentName`, and `ImagesDeploymentName`. This meant deployments were tightly coupled to their connection and had no independent identity.

In the new architecture, **AIDeployment** is a first-class typed entity with:

- **`Type`** — One or more deployment purposes: `Chat`, `Utility`, `Embedding`, `Image`, `SpeechToText`, or `TextToSpeech`
- **`IsDefault`** — Whether this deployment is the default for each selected type within its connection
- **Independent identity** — Each deployment has its own record and can be referenced by ID

AI Profiles and Chat Interactions now reference deployments by ID (`ChatDeploymentId`, `UtilityDeploymentId`) rather than relying on a connection name to resolve deployment names.

## Deployment Resolution Fallback

When resolving a deployment for a given type, the system follows this fallback chain:

1. **Explicit deployment** — The deployment ID set directly on the profile or interaction
2. **Connection default** — The default deployment for the requested type on the connection
3. **Global default** — The global default deployment configured in **Settings > Artificial Intelligence > Default Deployments**
4. **null** — No deployment found

:::info Utility → Chat Fallback
When resolving a **Utility** deployment and no utility deployment is found at any level of the chain above, the system automatically retries the entire chain using the **Chat** type as a last resort. This means you don't need to configure a separate Utility deployment — if one is missing, the default Chat deployment will be used instead.
:::

---

## Automatic Migration

:::tip
Most users don't need to do anything manually. The automatic migration handles the conversion on startup.
:::

On application startup, the data migration automatically:

1. Scans all existing `AIProviderConnection` records for deployment name fields
2. Creates typed `AIDeployment` records for each non-empty deployment name
3. Sets the `IsDefault` flag on the first deployment of each type per connection
4. Preserves all existing functionality — no downtime or data loss

After migration, review the auto-created deployments at **Artificial Intelligence > Deployments** to verify they look correct.

---

## Manual Steps After Migration

After the automatic migration runs:

1. **Review deployments** — Navigate to **Artificial Intelligence > Deployments** and verify the auto-created records have the correct type selections and default flags.
2. **Set global defaults** — Go to **Settings > Artificial Intelligence > Default Deployments** and configure global defaults for Chat, Utility, Embedding, Image, and voice-related deployment types as needed. These serve as fallbacks when a profile or interaction doesn't specify a deployment.
3. **Update profiles (optional)** — Existing profiles continue to work. However, you can now set separate `ChatDeploymentId` and `UtilityDeploymentId` on each profile for more granular control.

---

## appsettings.json Migration

### Old Format (still supported, deprecated)

```json
{
  "OrchardCore": {
    "CrestApps_AI": {
      "Providers": {
        "OpenAI": {
          "Connections": {
            "default": {
              "ChatDeploymentName": "gpt-4o",
              "UtilityDeploymentName": "gpt-4o-mini",
              "EmbeddingDeploymentName": "text-embedding-3-small",
              "ImagesDeploymentName": "dall-e-3"
            }
          }
        }
      }
    }
  }
}
```

### New Format (recommended)

```json
{
  "OrchardCore": {
    "CrestApps_AI": {
      "Providers": {
        "OpenAI": {
          "Connections": {
            "default": {
              "Deployments": [
                {
                  "Name": "gpt-4o",
                  "Type": "Chat",
                  "IsDefault": true
                },
                {
                  "Name": "gpt-4.1-mini",
                  "Type": ["Chat", "Utility"],
                  "IsDefault": true
                },
                {
                  "Name": "text-embedding-3-small",
                  "Type": "Embedding",
                  "IsDefault": true
                },
                {
                  "Name": "dall-e-3",
                  "Type": "Image",
                  "IsDefault": true
                }
              ]
            }
          }
        }
      }
    }
  }
}
```

If you prefer, the `Type` property can also be expressed as a comma-separated flags string such as `"Chat, Utility"`, but JSON arrays are easier to read and maintain.

:::info
Both formats are supported simultaneously. If both are present, the `Deployments` array takes precedence. We recommend migrating to the new format when convenient.
:::

### Non-Connection Deployments (New)

Contained-connection deployments (e.g., Azure Speech) can also be defined in `appsettings.json` using the `CrestApps_AI:Deployments` section. These deployments embed their own connection parameters and do not reference a shared provider connection.

```json
{
  "OrchardCore": {
    "CrestApps_AI": {
      "Deployments": [
        {
          "ClientName": "AzureSpeech",
          "Name": "my-speech-to-text",
          "Type": "SpeechToText",
          "IsDefault": true,
          "Endpoint": "https://eastus.api.cognitive.microsoft.com/",
          "AuthenticationType": "ApiKey",
          "ApiKey": "your-speech-service-api-key"
        }
      ]
    }
  }
}
```

Deployments defined this way are read-only, ephemeral (exist only while in configuration), and appear alongside database-managed deployments in dropdown menus and API queries.

---

## Code Migration

### Reading Deployment Names

If your code referenced deployment names from the connection:

```csharp
// Old (deprecated)
var chatDeployment = connection.ChatDeploymentName;
var embeddingDeployment = connection.EmbeddingDeploymentName;

// New (recommended) — use IAIDeploymentManager
var chatDeployment = await deploymentManager.ResolveAsync(
    AIDeploymentType.Chat, connectionName: connectionName);
var embeddingDeployment = await deploymentManager.ResolveAsync(
    AIDeploymentType.Embedding, connectionName: connectionName);
```

### AI Profile DeploymentId

If your code referenced `DeploymentId` on AI Profiles:

```csharp
// Old (deprecated)
var deploymentId = profile.DeploymentId;

// New (recommended)
var chatDeploymentId = profile.ChatDeploymentId;
var utilityDeploymentId = profile.UtilityDeploymentId;
```

### AICompletionContext

If your code built or read `AICompletionContext`:

```csharp
// Old (deprecated)
context.DeploymentId = "some-id";

// New (recommended)
context.ChatDeploymentId = "some-id";
```

---

## Recipe Migration

### Old Recipe Format (still works)

```json
{
  "steps": [
    {
      "name": "AIProfile",
      "profiles": [
        {
          "Name": "MyProfile",
          "ConnectionName": "default",
          "DeploymentId": "some-deployment-id",
          ...
        }
      ]
    }
  ]
}
```

### New Recipe Format

```json
{
  "steps": [
    {
      "name": "AIProfile",
      "profiles": [
        {
          "Name": "MyProfile",
          "ConnectionName": "default",
          "ChatDeploymentId": "chat-deployment-id",
          "UtilityDeploymentId": "utility-deployment-id",
          ...
        }
      ]
    }
  ]
}
```

The new `AIProfile` recipe format does not require `Source`. Profiles are source-agnostic and resolve their active client from the selected deployment or the optional `ConnectionName` fallback.

### AI Deployment Recipes

Create typed deployments via recipes:

```json
{
  "steps": [
    {
      "name": "AIDeployment",
      "deployments": [
        {
          "Name": "gpt-4o",
          "ClientName": "OpenAI",
          "ConnectionName": "default",
          "Type": "Chat",
          "IsDefault": true
        },
        {
          "Name": "gpt-4o-mini",
          "ClientName": "OpenAI",
          "ConnectionName": "default",
          "Type": "Utility",
          "IsDefault": true
        },
        {
          "Name": "dall-e-3",
          "ClientName": "OpenAI",
          "ConnectionName": "default",
          "Type": "Image",
          "IsDefault": true
        }
      ]
    }
  ]
}
```

For typed deployments, use `ClientName` in new recipes. The older `ProviderName` property is still read for backward compatibility only.

---

## Summary of Deprecated Properties

| Deprecated Property | Replacement |
|---|---|
| `AIProviderConnection.ChatDeploymentName` | `AIDeployment` record with `Type = Chat` |
| `AIProviderConnection.UtilityDeploymentName` | `AIDeployment` record with `Type = Utility` |
| `AIProviderConnection.EmbeddingDeploymentName` | `AIDeployment` record with `Type = Embedding` |
| `AIProviderConnection.ImagesDeploymentName` | `AIDeployment` record with `Type = Image` |
| `AIProvider.DefaultChatDeploymentName` | `AIDeployment` with `IsDefault = true` |
| `AIProvider.DefaultEmbeddingDeploymentName` | `AIDeployment` with `IsDefault = true` |
| `AIProvider.DefaultUtilityDeploymentName` | `AIDeployment` with `IsDefault = true` |
| `AIProvider.DefaultImagesDeploymentName` | `AIDeployment` with `IsDefault = true` |
| `AIProfile.DeploymentId` | `AIProfile.ChatDeploymentId` |
| `ChatInteraction.DeploymentId` | `ChatInteraction.ChatDeploymentId` |
| `AICompletionContext.DeploymentId` | `AICompletionContext.ChatDeploymentId` |

:::warning
The deprecated properties still work for backward compatibility but will be removed in a future major release. Migrate to the new typed deployment system at your earliest convenience.
:::
