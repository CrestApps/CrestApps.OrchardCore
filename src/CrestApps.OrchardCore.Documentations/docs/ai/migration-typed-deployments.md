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
- **`Name`** — A unique technical name used in settings, profiles, interactions, and recipes
- **`ModelName`** — A non-unique provider-facing model or deployment name sent to the AI provider
- **Site-level defaults** — Default deployments for profiles, chat interactions, and voice features are configured centrally in **Settings > Artificial Intelligence > Default Deployments**

AI Profiles now use `ChatDeploymentName` and `UtilityDeploymentName`, while profile templates, chat interactions, and site defaults still use selector properties such as `ChatDeploymentName` and `UtilityDeploymentName`. In all cases, the stored value is now the deployment's technical `Name` rather than its document `ItemId`.

## Deployment Resolution Fallback

When resolving a deployment for a given type, the system follows this fallback chain:

1. **Explicit deployment** — The deployment technical name set directly on the profile or interaction
2. **Global default** — The global default deployment configured in **Settings > Artificial Intelligence > Default Deployments**
3. **First matching deployment** — The first deployment that supports the requested type in the current scope
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
3. Backfills `ModelName` from `Name` for older deployment records that predate the split
4. Converts stored deployment selectors from legacy deployment `ItemId` values to technical deployment names
5. Preserves all existing functionality — no downtime or data loss

After migration, review the auto-created deployments at **Artificial Intelligence > Deployments** to verify they look correct.

---

## Manual Steps After Migration

After the automatic migration runs:

1. **Review deployments** — Navigate to **Artificial Intelligence > Deployments** and verify the auto-created records have the correct type selections.
2. **Set global defaults** — Go to **Settings > Artificial Intelligence > Default Deployments** and configure global defaults for Chat, Utility, Embedding, Image, and voice-related deployment types as needed. These serve as fallbacks when a profile or interaction doesn't specify a deployment.
3. **Update profiles (optional)** — Existing profiles continue to work. However, you can now set separate `ChatDeploymentName` and `UtilityDeploymentName` values on each profile for more granular control. These selectors save the deployment's technical `Name`.

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
                    "Name": "openai-chat",
                    "ModelName": "gpt-4o",
                    "Type": "Chat",
                    "IsDefault": true
                  },
                  {
                    "Name": "openai-chat-utility",
                    "ModelName": "gpt-4.1-mini",
                    "Type": ["Chat", "Utility"],
                    "IsDefault": true
                  },
                  {
                    "Name": "openai-embedding",
                    "ModelName": "text-embedding-3-small",
                    "Type": "Embedding",
                    "IsDefault": true
                  },
                  {
                    "Name": "openai-image",
                    "ModelName": "dall-e-3",
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
            "Name": "azure-speech-stt",
            "ModelName": "my-speech-to-text",
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

If `ModelName` is omitted in configuration, the system falls back to `Name` for backward compatibility. New configurations should set both values whenever the provider-facing model name should differ from the deployment's technical lookup name.

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

var providerModelName = chatDeployment.ModelName;
```

### AI Profile deployment selectors

If your code referenced `DeploymentId` on AI Profiles:

```csharp
// Old (deprecated)
var deploymentId = profile.DeploymentId;

// New (recommended)
var chatDeploymentName = profile.ChatDeploymentName;
var utilityDeploymentName = profile.UtilityDeploymentName;
```

### AICompletionContext

If your code built or read `AICompletionContext`:

```csharp
// Old (deprecated)
context.DeploymentId = "some-id";

// New (recommended)
context.ChatDeploymentName = "some-id";
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
          "DeploymentId": "legacy-deployment-item-id",
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
          "ChatDeploymentName": "openai-chat",
          "UtilityDeploymentName": "openai-utility",
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
          "Name": "openai-chat",
          "ModelName": "gpt-4o",
          "ClientName": "OpenAI",
          "ConnectionName": "default",
          "Type": "Chat",
          "IsDefault": true
        },
        {
          "Name": "openai-utility",
          "ModelName": "gpt-4o-mini",
          "ClientName": "OpenAI",
          "ConnectionName": "default",
          "Type": "Utility",
          "IsDefault": true
        },
        {
          "Name": "openai-image",
          "ModelName": "dall-e-3",
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
| `AIProfile.DeploymentId` | `AIProfile.ChatDeploymentName` |
| `ChatInteraction.DeploymentId` | `ChatInteraction.ChatDeploymentName` |
| `AICompletionContext.DeploymentId` | `AICompletionContext.ChatDeploymentName` |

:::warning
The deprecated properties still work for backward compatibility but will be removed in a future major release. Migrate to the new typed deployment system at your earliest convenience.
:::
