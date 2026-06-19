---
sidebar_label: Azure AI Inference
sidebar_position: 3
title: Azure AI Inference Chat Feature
description: Azure AI Inference integration for GitHub models using Azure AI provider in Orchard Core.
---

| | |
| --- | --- |
| **Feature Name** | Azure AI Inference Chat |
| **Feature ID** | `CrestApps.OrchardCore.AzureAIInference` |

Provides access to GitHub Models and other Azure AI Inference endpoints.

## Overview

The Azure AI Inference provider integrates Azure-hosted model inference endpoints into Orchard Core.

## Configuration

Add the following settings to `appsettings.json`:

```json
{
  "OrchardCore": {
    "CrestApps": {
      "AI": {
        "Providers": {
          "AzureAIInference": {
            "DefaultConnectionName": "default",
            "Connections": {
              "default": {
                "Endpoint": "https://your-resource.services.ai.azure.com/models",
                "AuthenticationType": "ApiKey",
                "ApiKey": "your-api-key",
                "Deployments": [
                  {
                    "Name": "github-models-chat",
                    "ModelName": "Phi-3-medium-4k-instruct",
                    "Purpose": "Chat"
                  },
                  {
                    "Name": "github-models-utility",
                    "ModelName": "Phi-3-medium-4k-instruct",
                    "Purpose": "Utility"
                  }
                ]
              }
            }
          }
        }
      }
    }
  }
}
```

`AuthenticationType` can be `Default`, `ManagedIdentity`, or `ApiKey`. When using `ApiKey`, the `ApiKey` field is required.

When using `ManagedIdentity`, you can optionally provide an `IdentityId` to use a user-assigned managed identity. If `IdentityId` is omitted or empty, the system-assigned managed identity is used.

```json
{
  "Connections": {
    "default": {
      "Endpoint": "https://my-resource.services.ai.azure.com/models",
      "AuthenticationType": "ManagedIdentity",
      "IdentityId": "optional-user-assigned-managed-identity-client-id"
    }
  }
}
```

For detailed instructions on creating Azure AI Inference and obtaining the endpoint, refer to the official [documentation](https://learn.microsoft.com/en-us/azure/ai-foundry/model-inference/how-to/configure-project-connection?pivots=ai-foundry-portal).

You can also provision the same connection through the `AIProviderConnections` recipe step by storing the provider settings under `Properties.AzureAIInferenceConnectionMetadata`.
