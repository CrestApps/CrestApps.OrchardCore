## OpenAI Chat Feature

The **OpenAI AI Chat** feature enhances the **AI Services** functionality by integrating OpenAI's models. It provides a suite of services to interact with these models, enabling advanced AI capabilities.

### Configuration

To configure the OpenAI connection, add the following settings to the `appsettings.json` file:

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
              "ApiKey": "<!-- Your API Key Goes here -->",
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

### Using AI Deployments  

If the **AI Deployments** feature is enabled, you can create multiple deployments under the same connection. This allows different AI profiles to utilize different models while sharing the same connection.  

### Configuring DeepSeek Connection

The **OpenAI AI Chat** feature enables interaction with any AI provider that adheres to OpenAI standards, including DeepSeek. You can configure the DeepSeek connection either through the configuration provider or via the UI using the **AI Connection Management** feature.

#### Configuration Using Configuration Provider

To configure the DeepSeek connection using `appsettings.json` file, use the following example:

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

- The `DefaultConnectionName` and `DefaultDeploymentName` under the `OpenAI` node are required only if you want to set the `deepseek` connection as the default OpenAI connection when AI profiles use the default setting.

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
