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

Add an OpenAI connection under `OrchardCore:CrestApps:AI:Providers:OpenAI`:

```json
{
  "OrchardCore": {
    "CrestApps": {
      "AI": {
        "Providers": {
          "OpenAI": {
            "DefaultConnectionName": "openai-cloud",
            "Connections": {
              "openai-cloud": {
                "ApiKey": "your-api-key",
                "Deployments": [
                  {
                    "Name": "chat-default",
                    "ModelName": "gpt-4o",
                    "Purpose": "Chat"
                  },
                  {
                    "Name": "utility-default",
                    "ModelName": "gpt-4o-mini",
                    "Purpose": "Utility"
                  },
                  {
                    "Name": "embedding-default",
                    "ModelName": "text-embedding-3-large",
                    "Purpose": "Embedding"
                  },
                  {
                    "Name": "image-default",
                    "ModelName": "dall-e-3",
                    "Purpose": "Image"
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

Set `Endpoint` when the provider uses a custom OpenAI-compatible base URL:

```json
{
  "Connections": {
    "deepseek": {
      "Endpoint": "https://api.deepseek.com/v1",
      "ApiKey": "your-deepseek-api-key"
    }
  }
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
          "Purpose": "Chat"
        },
        {
          "Name": "deepseek-reasoner",
          "ModelName": "deepseek-reasoner",
          "ClientName": "OpenAI",
          "ConnectionName": "deepseek",
          "Purpose": "Utility"
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
