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
          "DefaultConnectionName": "default",
          "DefaultDeploymentName": "Phi-3-medium-4k-instruct",
          "Connections": {
            "default": {
              "Endpoint": "https://<!-- Your Azure Resource Name -->.services.ai.azure.com/models",
              "AuthenticationType": "ApiKey",
              "ApiKey": "<!-- Your GitHub Access Token goes here -->",
              "DefaultDeploymentName": "Phi-3-medium-4k-instruct",
              "DefaultUtilityDeploymentName": "Phi-3-medium-4k-instruct"
            }
          }
        }
      }
    }
  }
}
```

Authentication Type in the connection can be `Default`, `ManagedIdentity` or `ApiKey`. When using `ApiKey` authentication type, `ApiKey` is required.

For detailed instructions on creating Azure AI Inference and obtaining the Endpoint, refer to the official [documentation](https://learn.microsoft.com/en-us/azure/ai-foundry/model-inference/how-to/configure-project-connection?pivots=ai-foundry-portal).
