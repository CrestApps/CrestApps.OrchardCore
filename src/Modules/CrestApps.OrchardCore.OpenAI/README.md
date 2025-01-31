## OpenAI-Powered Artificial Intelligence Feature

The **OpenAI-Powered Artificial Intelligence** feature enhances the **Artificial Intelligence** functionality by integrating OpenAI’s models. It provides a suite of services to interact with these models, enabling advanced AI capabilities. This feature is available on demand and cannot be manually toggled.

### OpenAI-Powered AI Chat

The **OpenAI-Powered AI Chat** feature builds upon the core **OpenAI-Powered Artificial Intelligence** functionality, offering tools to create AI chatbots that engage users using OpenAI’s advanced language models. This feature is also activated on demand and cannot be manually enabled or disabled.

### Configuration

Before using any OpenAI features, ensure that the appropriate settings are configured. You can do this using various setting providers. Below is an example of how to configure the services within the `appsettings.json` file:

```json
{
  "CrestApps_AI": {
    "OpenAI": {
      "Connections": {
        "<!-- Provider name goes here -->": [
          {
            "Name": "<!-- Provide a unique name for your connection, ideally matching your Azure account's AccountName -->",
            // Additional configuration settings
          }
        ]
      }
    }
  }
}
```
