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

Provides a way to interact with GitHub Models using Azure Inference service provider.

## Overview

The **Azure AI Inference Chat** feature enhances the **AI Services** functionality by integrating GitHub models using Azure AI Inference provider. It provides a suite of services to interact with these models, enabling advanced AI capabilities.

### Configuration

To configure the OpenAI connection, add the following settings to the `appsettings.json` file:

```json
{
  "OrchardCore": {
    "CrestApps_AI": {
      "Providers": {
        "AzureAIInference": {
          "Connections": {
            "default": {
              "Endpoint": "https://<!-- Your Azure Resource Name -->.services.ai.azure.com/models",
              "AuthenticationType": "ApiKey",
              "ApiKey": "<!-- Your GitHub Access Token goes here -->",
              "Deployments": [
                { "Name": "Phi-3-medium-4k-instruct", "Type": "Chat", "IsDefault": true },
                { "Name": "Phi-3-medium-4k-instruct", "Type": "Utility", "IsDefault": true }
              ]
            }
          }
        }
      }
    }
  }
}
```

:::warning Legacy Format (Deprecated)
The following format using `ChatDeploymentName`, `UtilityDeploymentName`, etc. is still supported but deprecated. Existing configurations will be auto-migrated at runtime.

```json
{
  "Connections": {
    "default": {
      "Endpoint": "https://my-resource.services.ai.azure.com/models",
      "AuthenticationType": "ApiKey",
      "ApiKey": "...",
      "ChatDeploymentName": "Phi-3-medium-4k-instruct",
      "UtilityDeploymentName": "Phi-3-medium-4k-instruct",
      "EmbeddingDeploymentName": "",
      "ImagesDeploymentName": ""
    }
  }
}
```
:::

Authentication Type in the connection can be `Default`, `ManagedIdentity` or `ApiKey`. When using `ApiKey` authentication type, `ApiKey` is required.

When using `ManagedIdentity`, you can optionally provide an `IdentityId` to use a **user-assigned managed identity**. If `IdentityId` is omitted or empty, the **system-assigned managed identity** is used.

```json
{
  "Connections": {
    "default": {
      "Endpoint": "https://my-resource.services.ai.azure.com/models",
      "AuthenticationType": "ManagedIdentity",
      "IdentityId": "<!-- Optional: client ID of a user-assigned managed identity -->"
    }
  }
}
```

For detailed instructions on creating Azure AI Inference and obtaining the Endpoint, refer to the official [documentation](https://learn.microsoft.com/en-us/azure/ai-foundry/model-inference/how-to/configure-project-connection?pivots=ai-foundry-portal).
