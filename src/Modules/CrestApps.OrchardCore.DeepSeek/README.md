## DeepSeek AI Services Feature

The **DeepSeek AI Services** feature enhances the **AI Services** functionality by integrating DeepSeek's models. It provides a suite of services to interact with these models, enabling advanced AI capabilities. This feature is available on demand and cannot be manually toggled.

### DeepSeek AI Services Chat Feature

The **DeepSeek AI Services Chat** feature builds upon the core **DeepSeek AI Services** functionality, offering tools to create AI chatbots that engage users using DeepSeek's advanced language models. This feature is also activated on demand and cannot be manually enabled or disabled.

#### Configuration

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

### DeepSeek Cloud AI Chat Feature

The **DeepSeek Cloud AI Chat** feature builds upon the core **DeepSeek AI Services Chat** functionality, offering tools to create AI chatbots that engage users using DeepSeek's Cloud service. To obtain API key for DeepSeek, you can visit the [DeepSeek website](https://platform.deepseek.com/). You can configure it as follow:

```json
{
  "OrchardCore":{
    "CrestApps_AI":{
      "Providers":{
        "DeepSeekCloud": {
          "DefaultConnectionName": "deepseek-cloud",
          "DefaultDeploymentName": "deepseek-chat",
          "Connections": {
            "deepseek-cloud": {
              "ApiKey": "<!-- Your API Key Goes here. -->",
              "DefaultDeploymentName": "deepseek-chat"
            }
          }
        }
      }
    }
  }
}
```
Please note that the connection name `deepseek-cloud` should not change when using the **DeepSeek Cloud AI Chat** feature.
