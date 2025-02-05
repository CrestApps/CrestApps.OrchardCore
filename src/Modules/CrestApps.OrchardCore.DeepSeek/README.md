## DeepSeek-Powered Artificial Intelligence Feature

The **DeepSeek-Powered Artificial Intelligence** feature enhances the **Artificial Intelligence** functionality by integrating DeepSeek's models. It provides a suite of services to interact with these models, enabling advanced AI capabilities. This feature is available on demand and cannot be manually toggled.

### DeepSeek-Powered AI Chat Feature

The **DeepSeek-Powered AI Chat** feature builds upon the core **DeepSeek-Powered Artificial Intelligence** functionality, offering tools to create AI chatbots that engage users using DeepSeek's advanced language models. This feature is also activated on demand and cannot be manually enabled or disabled.

### Configuration

In addition to setting up the AI services [as explained here](../CrestApps.OrchardCore.AI/README.md), you can customize the OpenAI parameters to fine-tune its default behavior. Below is an example of how to configure the default parameters in the `appsettings.json` file:

```json
{
  "OrchardCore":{
    "CrestApps_AI":{
      "DeepSeek":{
        "DefaultParameters":{
          "Temperature":0,
          "MaxOutputTokens":800,
          "TopP":1,
          "FrequencyPenalty":0,
          "PresencePenalty":0,
          "PastMessagesCount":10
        }
      }
    }
  }
}
```

### Artificial Intelligence Powered by DeepSeek Cloud Service Chat Feature

The **Artificial Intelligence Powered by DeepSeek Cloud Service Chat** feature builds upon the core **DeepSeek-Powered Artificial Intelligence** functionality, offering tools to create AI chatbots that engage users using DeepSeek's Cloud service. To obtain API key for DeepSeek, you can visit the [DeepSeek website](https://platform.deepseek.com/). You can configure it as follow:

```json
{
  "OrchardCore":{
    "CrestApps_AI":{
      "Providers":{
        "DeepSeek": {
          "DefaultConnectionName": "deepseek-cloud",
          "Connections": {
            "deepseek-cloud": {
              "ApiKey": "<!-- Your API Key Goes here. -->",
              "Model": "deepseek-chat"
            }
          }
        }
      }
    }
  }
}
```
