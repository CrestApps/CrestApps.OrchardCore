## Table of Contents

- [OpenAI Chat Feature](#openai-chat-feature)
  - [Configuration](#configuration)
  - [Using AI Deployments](#using-ai-deployments)
  - [Configuring Other AI Providers](#configuring-other-ai-providers)
  - [Configuring a Provider Example: DeepSeek](#configuring-a-provider-example-deepseek)
  - [Configuring Multiple Models](#configuring-multiple-models)

## OpenAI Chat Feature  

The **OpenAI AI Chat** feature enhances the **AI Services** functionality by integrating OpenAI-compatible models. It provides a suite of services to interact with these models, enabling advanced AI capabilities.  

### Configuration  

The **OpenAI AI Chat** feature allows you to connect to any AI provider that adheres to OpenAI API standards, such as **DeepSeek, Google Gemini, Together AI, vLLM, Cloudflare Workers AI, and more**.  

To configure a connection, add the following settings to the `appsettings.json` file:  

```json
{
  "OrchardCore": {
    "CrestApps_AI": {
      "Providers": {
        "OpenAI": {
          "DefaultConnectionName": "openai-cloud",
          "DefaultDeploymentName": "gpt-4o-mini",
          "Connections": {
            "openai-cloud": {
              "ApiKey": "<!-- Your API Key Goes Here -->",
              "DefaultDeploymentName": "gpt-4o-mini"
            }
          }
        }
      }
    }
  }
}
```

---

### Complete Configuration Example with Multiple Connection Types

OpenAI supports multiple connection types for different capabilities:

```json
{
  "OrchardCore": {
    "CrestApps_AI": {
      "Providers": {
        "OpenAI": {
          "DefaultConnectionName": "openai-cloud",
          "DefaultDeploymentName": "gpt-4o-mini",
          "Connections": {
            "openai-cloud": {
              "Type": "Chat",
              "ApiKey": "<!-- Your API Key Goes Here -->",
              "DefaultDeploymentName": "gpt-4o-mini"
            },
            "openai-embeddings": {
              "Type": "Embedding",
              "ApiKey": "<!-- Your API Key Goes Here -->",
              "DefaultDeploymentName": "text-embedding-3-small"
            },
            "openai-whisper": {
              "Type": "SpeechToText",
              "ApiKey": "<!-- Your API Key Goes Here -->",
              "DefaultDeploymentName": "whisper-1"
            }
          }
        }
      }
    }
  }
}
```

**Connection Types:**
- `Chat` - For chat/completion models (gpt-4, gpt-4o-mini, etc.)
- `Embedding` - For embedding models (text-embedding-3-small, text-embedding-3-large, etc.)
- `SpeechToText` - For speech-to-text models (whisper-1)

If no `Type` is specified, `Chat` is used as the default.

---


### Using AI Deployments  

If the **AI Deployments** feature is enabled, you can create multiple deployments under the same connection. This allows different AI profiles to utilize different models while sharing the same connection.  

### Configuring Other AI Providers  

The **OpenAI AI Chat** feature supports multiple AI providers that adhere to OpenAI API standards, such as:  

- **DeepSeek** ([Docs](https://platform.deepseek.com/usage))  
- **Google Gemini** ([Docs](https://ai.google.dev/gemini-api/docs/openai))  
- **Together AI** ([Docs](https://docs.together.ai/docs/openai-api-compatibility))  
- **vLLM** ([Docs](https://docs.vllm.ai/en/latest/serving/openai_compatible_server.html))  
- **Cloudflare Workers AI** ([Docs](https://developers.cloudflare.com/workers-ai/configuration/open-ai-compatibility/))  
- **LM Studio** ([Docs](https://github.com/xorbitsai/inference))  
- **KoboldCpp** ([Docs](https://docs.continue.dev/customize/model-providers/openai))  
- **text-gen-webui** ([Docs](https://docs.continue.dev/customize/model-providers/openai))  
- **FastChat** ([Docs](https://docs.continue.dev/customize/model-providers/openai))  
- **LocalAI** ([Docs](https://docs.continue.dev/customize/model-providers/openai))  
- **llama-cpp-python** ([Docs](https://docs.continue.dev/customize/model-providers/openai))  
- **TensorRT-LLM** ([Docs](https://docs.continue.dev/customize/model-providers/openai))  
- **BerriAI/litellm** ([Docs](https://docs.continue.dev/customize/model-providers/openai))  

---

### Configuring a Provider Example: DeepSeek  

You can configure the DeepSeek connection either through the configuration provider or via the UI using the **AI Connection Management** feature.  

#### Configuration Using `appsettings.json`  

To configure DeepSeek, add the following settings:  

```json
{
  "OrchardCore": {
    "CrestApps_AI": {
      "Providers": {
        "OpenAI": {
          "DefaultConnectionName": "deepseek",
          "DefaultDeploymentName": "deepseek-chat",
          "Connections": {
            "deepseek": {
              "Endpoint": "https://api.deepseek.com/v1",
              "ApiKey": "<!-- Your API Key Goes Here -->",
              "DefaultDeploymentName": "deepseek-chat"
            }
          }
        }
      }
    }
  }
}
```

> The `DefaultConnectionName` and `DefaultDeploymentName` under the `OpenAI` node are required only if you want to set the `deepseek` connection as the default OpenAI connection when AI profiles use the default setting.  

#### Configuration via AI Connection Management  

If you are using the **AI Connection Management** feature, you can configure DeepSeek through the UI or by executing the following recipe:  

```json
{
  "steps": [
    {
      "name": "AIProviderConnections",
      "connections": [
        {
          "Source": "OpenAI",
          "Name": "deepseek",
          "IsDefault": false,
          "DefaultDeploymentName": "deepseek-chat",
          "DisplayText": "DeepSeek",
          "Properties": {
            "OpenAIConnectionMetadata": {
              "Endpoint": "https://api.deepseek.com/v1",
              "ApiKey": "<!-- DeepSeek API Key -->"
            }
          }
        }
      ]
    }
  ]
}
```

---

### Configuring Other AI Providers  

To connect to **Google Gemini**, **Together AI**, **vLLM**, or any other supported provider, modify the `Endpoint` and `ApiKey` fields accordingly. For example, configuring **Google Gemini** would look like this:  

```json
{
  "OrchardCore": {
    "CrestApps_AI": {
      "Providers": {
        "OpenAI": {
          "DefaultConnectionName": "google-gemini",
          "DefaultDeploymentName": "gemini-pro",
          "Connections": {
            "google-gemini": {
              "Endpoint": "https://generativelanguage.googleapis.com/v1",
              "ApiKey": "<!-- Your Google Gemini API Key -->",
              "DefaultDeploymentName": "gemini-pro"
            }
          }
        }
      }
    }
  }
}
```

You can replace `Endpoint` with the appropriate URL for each provider.  

---

#### Configuring Multiple Models

If you need access to multiple DeepSeek models, you can execute the following recipe to add standard deployments:

```json
{
  "steps": [
    {
      "name": "AIDeployment",
      "deployments": [
        {
          "Name": "deepseek-chat",
          "ProviderName": "OpenAI",
          "ConnectionName": "deepseek"
        },
        {
          "Name": "deepseek-reasoner",
          "ProviderName": "OpenAI",
          "ConnectionName": "deepseek"
        }
      ]
    }
  ]
}
```

This configuration allows you to access multiple models provided by DeepSeek, such as `deepseek-chat` and `deepseek-reasoner`.

By following these steps, you can seamlessly integrate DeepSeek into your AI chat feature, either as a default provider or alongside other AI models.
