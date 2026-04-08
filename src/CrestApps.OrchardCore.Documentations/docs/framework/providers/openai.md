---
sidebar_label: OpenAI
sidebar_position: 2
title: OpenAI Provider
description: Connect to the OpenAI API for chat completions, embeddings, image generation, and speech services.
---

:::info Canonical framework docs
The shared framework guidance now lives in **[CrestApps.Core](https://core.crestapps.com/docs/framework/providers/openai)**. This Orchard Core page is kept for Orchard-specific integration context and cross-links.
:::

# OpenAI Provider

> Connect to the OpenAI API (`api.openai.com`) for chat completions, embeddings, image generation, and speech.

## Quick Start

```csharp
builder.Services
    .AddCrestAppsAI()
    .AddOrchestrationServices()
    .AddOpenAIProvider();
```

## Services Registered

| Service | Implementation | Lifetime |
|---------|---------------|----------|
| `IAIClientProvider` | `OpenAIClientProvider` | Scoped |
| `IAICompletionClient` | `OpenAICompletionClient` | Scoped |
| Connection source | — | Scoped |

## Configuration

### Connection Setup

Provide an API key through your connection source:

```csharp
builder.Services.AddAIConnectionSource("OpenAI", options =>
{
    options.Connections.Add(new AIProviderConnectionEntry
    {
        Name = "my-openai",
        ProviderName = "OpenAI",
        // Set API key and optional endpoint
    });
});
```

### Constants

| Constant | Value |
|----------|-------|
| `OpenAIConstants.ProviderName` | `"OpenAI"` |
| `OpenAIConstants.ClientName` | `"OpenAI"` |

## Capabilities

| Capability | Supported |
|-----------|-----------|
| Chat completions | ✅ |
| Streaming | ✅ |
| Embeddings | ✅ |
| Image generation | ✅ |
| Speech-to-text | ✅ |
| Text-to-speech | ✅ |

## Configuration Example

A full `appsettings.json` configuration for OpenAI:

```json
{
  "CrestApps": {
    "AI": {
      "Providers": {
        "OpenAI": {
          "ApiKey": "sk-..."
        }
      }
    }
  }
}
```

Or register connections programmatically:

```csharp
builder.Services.AddAIConnectionSource("OpenAI", options =>
{
    options.Connections.Add(new AIProviderConnectionEntry
    {
        Name = "my-openai",
        ProviderName = "OpenAI",
        // API key is read from configuration or set directly
    });
});
```

:::tip
Never commit API keys to source control. Use environment variables, user secrets, or a vault provider:
```bash
dotnet user-secrets set "CrestApps:AI:Providers:OpenAI:ApiKey" "sk-..."
```
:::

## Available Models

| Model | Type | Context Window | Best For |
|-------|------|---------------|----------|
| `gpt-4.1` | Chat | 1M tokens | Complex reasoning, coding, instruction following |
| `gpt-4.1-mini` | Chat | 1M tokens | Balanced performance and cost |
| `gpt-4.1-nano` | Chat | 1M tokens | Fast, cost-effective for simple tasks |
| `o4-mini` | Reasoning | 200K tokens | STEM, math, coding with chain-of-thought |
| `gpt-4o` | Chat | 128K tokens | Multimodal (text + vision), general purpose |
| `gpt-4o-mini` | Chat | 128K tokens | Budget-friendly multimodal |
| `text-embedding-3-small` | Embedding | 8K tokens | Cost-effective embeddings |
| `text-embedding-3-large` | Embedding | 8K tokens | Higher-quality embeddings |
| `dall-e-3` | Image | — | Image generation |
| `whisper-1` | Speech-to-text | — | Audio transcription |
| `tts-1` / `tts-1-hd` | Text-to-speech | — | Voice synthesis |

:::info
Model availability and capabilities change frequently. Check the [OpenAI models documentation](https://platform.openai.com/docs/models) for the latest information.
:::

## Streaming

The OpenAI provider fully supports streaming responses. When streaming is enabled, tokens are sent to the client as they are generated rather than waiting for the complete response:

```csharp
// Streaming is handled automatically by the orchestrator when the
// chat interaction is configured for streaming (the default for real-time chat).
// No additional configuration is needed.
```

Streaming is the default behavior for the chat interactions module. The `OpenAICompletionClient` uses the `IChatClient.GetStreamingResponseAsync()` method from the Microsoft.Extensions.AI abstraction.

## Function Calling

OpenAI models support function calling (tool use), which is the foundation for the [Custom AI Tools](../tools.md) system. When tools are registered and assigned to a profile, the OpenAI provider automatically:

1. Serializes tool definitions as JSON Schema in the request
2. Parses tool call responses from the model
3. Invokes the matching `AITool` via the orchestrator
4. Sends tool results back to the model for the final response

All GPT-4 and newer models support parallel function calling (multiple tools invoked in a single turn).

## Orchard Core Integration

The [OpenAI provider module](../../ai/providers/openai.md) adds admin UI for managing OpenAI connections and deployments.
