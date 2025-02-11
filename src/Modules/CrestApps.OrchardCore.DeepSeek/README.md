## DeepSeek AI Services Feature

The **DeepSeek AI Chat** feature extends the capabilities of the **AI Services** functionality by integrating DeepSeek's advanced models.

### Configuration

To configure DeepSeek AI services, include the following in your `appsettings.json` file:

```json
{
  "OrchardCore": {
    "CrestApps_AI": {
      "Providers": {
        "DeepSeek": {
          "DefaultConnectionName": "deepseek-cloud",
          "DefaultDeploymentName": "deepseek-chat",
          "Connections": {
            "deepseek-cloud": {
              "ApiKey": "<!-- Your API Key Goes here -->",
              "DefaultDeploymentName": "deepseek-chat"
            }
          }
        }
      }
    }
  }
}
```
