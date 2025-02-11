## Azure AI Inference Chat Feature

The **Azure AI Inference Chat** feature enhances the **AI Services** functionality by integrating GitHub models using Azure AI Inference provider. It provides a suite of services to interact with these models, enabling advanced AI capabilities.

### Configuration

To configure the OpenAI connection, add the following settings to the `appsettings.json` file:

```json
{
  "OrchardCore": {
    "CrestApps_AI": {
      "Providers": {
        "AzureAIInference": {
          "DefaultConnectionName": "default",
          "DefaultDeploymentName": "Phi-3-medium-4k-instruct",
          "Connections": {
            "default": {
              "ApiKey": "<!-- Your GitHub Access Token goes here -->",
              "DefaultDeploymentName": "Phi-3-medium-4k-instruct"
            }
          }
        }
      }
    }
  }
}
```
