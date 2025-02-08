## Ollama AI Chat Feature  

The **Ollama AI Chat** allows developers to interact with any Ollama model seamlessly. You can explore all supported models at [Ollama Search](https://ollama.com/search).  

### Running Ollama Locally  

To run an Ollama model locally using this project:  

1. Set `CrestApp.Aspire.HostApp` as your startup project.  
2. Run the project to start the Aspire host, which sets up the necessary environment to connect to any Ollama model locally.  

By default, the project uses the `deepseek-v2:16b` model (8.9GB). Ensure your system has enough storage space before running it. The model will be downloaded automatically on the first run. You can monitor the download and service statuses from the **Resources** tab in the Aspire dashboard.  

### Configuration  

Beyond setting up AI services [as detailed here](../CrestApps.OrchardCore.AI/README.md), you can fine-tune the OpenAI parameters to customize its behavior. Modify the default settings in the `appsettings.json` file as shown below:  

```json
{
  "OrchardCore": {
    "CrestApps_AI": {
      "Ollama": {
        "DefaultParameters": {
          "Temperature": 0,
          "MaxOutputTokens": 800,
          "TopP": 1,
          "FrequencyPenalty": 0,
          "PresencePenalty": 0,
          "PastMessagesCount": 10
        }
      }
    }
  }
}
```  

This configuration allows you to control factors like response randomness (`Temperature`), token limits (`MaxOutputTokens`), and conversational memory (`PastMessagesCount`).  
