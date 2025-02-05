## OpenAI-Powered Artificial Intelligence Feature

The **OpenAI-Powered Artificial Intelligence** feature enhances the **Artificial Intelligence** functionality by integrating OpenAI’s models. It provides a suite of services to interact with these models, enabling advanced AI capabilities. This feature is available on demand and cannot be manually toggled.

### OpenAI-Powered AI Chat Feature

The **OpenAI-Powered AI Chat** feature builds upon the core **OpenAI-Powered Artificial Intelligence** functionality, offering tools to create AI chatbots that engage users using OpenAI’s advanced language models. This feature is also activated on demand and cannot be manually enabled or disabled.

### Configuration

In addition to setting up the AI services [as explained here](../CrestApps.OrchardCore.AI/README.md), you can customize the OpenAI parameters to fine-tune its default behavior. Below is an example of how to configure the default parameters in the `appsettings.json` file:

```json
{
  "OrchardCore":{
    "CrestApps_AI":{
      "OpenAI":{
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
