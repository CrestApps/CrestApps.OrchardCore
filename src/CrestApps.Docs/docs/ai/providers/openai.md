---
sidebar_label: OpenAI
sidebar_position: 1
title: OpenAI Chat Feature
description: OpenAI-compatible AI chat integration supporting DeepSeek, Google Gemini, Together AI, vLLM, and more.
---

| | |
| --- | --- |
| **Feature Name** | OpenAI Chat |
| **Feature ID** | `CrestApps.OrchardCore.OpenAI` |

Provides OpenAI-compatible AI services for Orchard Core.

## Overview

Use this provider for OpenAI and for OpenAI-compatible platforms such as DeepSeek, Google Gemini, Together AI, vLLM, Cloudflare Workers AI, LM Studio, LocalAI, and similar services.

## appsettings.json configuration

Add an OpenAI connection under `OrchardCore:CrestApps:AI:Connections`:

```json
{
  "OrchardCore": {
    "CrestApps": {
      "AI": {
        "Connections": [
          {
            "Name": "openai-cloud",
            "ClientName": "OpenAI",
            "ApiKey": "your-api-key"
          }
        ],
        "Deployments": [
          {
            "Name": "chat-default",
            "ClientName": "OpenAI",
            "ConnectionName": "openai-cloud",
            "ModelName": "gpt-4o",
            "Properties": {
              "AIDeploymentMetadata": {
                "Features": [ "textGeneration", "toolCalling", "streaming", "structuredOutputs" ]
              }
            }
          },
          {
            "Name": "embedding-default",
            "ClientName": "OpenAI",
            "ConnectionName": "openai-cloud",
            "ModelName": "text-embedding-3-large",
            "Properties": {
              "AIDeploymentMetadata": {
                "Features": [ "textEmbedding" ]
              }
            }
          },
          {
            "Name": "image-default",
            "ClientName": "OpenAI",
            "ConnectionName": "openai-cloud",
            "ModelName": "dall-e-3",
            "Properties": {
              "AIDeploymentMetadata": {
                "Features": [ "imageOutput" ]
              }
            }
          }
        ]
      }
    }
  }
}
```

`ClientName` ties a deployment to its provider and `ConnectionName` to the connection it authenticates with.
`Features` declares what the model can do; which deployment serves chat, utility, embedding and the rest is
decided by the deployment slots under **Configuration** -> **Artificial Intelligence** -> **Settings**, so one
deployment can back several slots. See [Model capabilities](../model-capabilities.md) for the full list of
features and slots.

Set `Endpoint` when the provider uses a custom OpenAI-compatible base URL:

```json
{
  "Connections": [
    {
      "Name": "deepseek",
      "ClientName": "OpenAI",
      "Endpoint": "https://api.deepseek.com/v1",
      "ApiKey": "your-deepseek-api-key"
    }
  ]
}
```

## Recipe setup

Use `AIProviderConnections` to create the connection and `AIDeployment` to create the deployments that profiles can select.

```json
{
  "steps": [
    {
      "name": "AIProviderConnections",
      "Connections": [
        {
          "Source": "OpenAI",
          "Name": "deepseek",
          "DisplayText": "DeepSeek",
          "Properties": {
            "OpenAIConnectionMetadata": {
              "Endpoint": "https://api.deepseek.com/v1",
              "ApiKey": "your-deepseek-api-key"
            }
          }
        }
      ]
    },
    {
      "name": "AIDeployment",
      "Deployments": [
        {
          "Name": "deepseek-chat",
          "ModelName": "deepseek-chat",
          "ClientName": "OpenAI",
          "ConnectionName": "deepseek",
          "Properties": {
            "AIDeploymentMetadata": {
              "Features": [ "textGeneration", "toolCalling", "streaming" ]
            }
          }
        },
        {
          "Name": "deepseek-reasoner",
          "ModelName": "deepseek-reasoner",
          "ClientName": "OpenAI",
          "ConnectionName": "deepseek",
          "Properties": {
            "AIDeploymentMetadata": {
              "Features": [ "textGeneration", "reasoning", "streaming" ]
            }
          }
        }
      ]
    }
  ]
}
```

## Selecting deployments

Choose deployments explicitly on AI profiles, templates, or chat interactions when you need provider-specific model selection. For tenant-wide fallbacks, configure **Settings -> Artificial Intelligence -> Default Deployments**.

## Other OpenAI-compatible providers

To connect to another compatible platform, keep the same Orchard structure and change only the provider endpoint, API key, and deployment model names. For example, a Gemini connection typically uses an endpoint such as `https://generativelanguage.googleapis.com/v1`.
