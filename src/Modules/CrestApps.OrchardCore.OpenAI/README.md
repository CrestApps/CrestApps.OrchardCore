## OpenAI AI Chat Feature

The **OpenAI AI Chat** feature enhances the **AI Services** functionality by integrating OpenAI's models. It provides a suite of services to interact with these models, enabling advanced AI capabilities.

#### Configuration

In addition to setting up the AI services [as explained here](../CrestApps.OrchardCore.AI/README.md), you can customize the OpenAI parameters to fine-tune its default behavior. Below is an example of how to configure the default parameters in the `appsettings.json` file:

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
