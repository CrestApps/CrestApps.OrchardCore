## AI Chat Feature

The **AI Chat** feature builds upon the **AI Services** feature by adding AI chat capabilities. Once enabled, any chat-type AI profile with the "Show On Admin Menu" option will appear under the **Artificial Intelligence** section in the admin menu, allowing you to interact with your chat profiles. If the Widgets feature is enabled, a widget will also be available to add to your content.

**Note**: This feature does not provide completion client implementations (e.g., OpenAI, DeepSeek, etc.). To enable chat capabilities, you must enable at least one feature that implements an AI completion client, such as:

- **OpenAI AI Chat** (`CrestApps.OrchardCore.OpenAI`): AI-powered chat using Azure OpenAI service.
- **Azure OpenAI Chat** (`CrestApps.OrchardCore.OpenAI.Azure.Standard`): AI services using Azure OpenAI models.
- **Azure OpenAI Chat with Your Data** (`CrestApps.OrchardCore.OpenAI.Azure.AISearch`): AI chat using Azure OpenAI models combined with Azure AI Search data.
- **Azure AI Inference Chat** (`CrestApps.OrchardCore.AzureAIInference`): AI services using Azure AI Inference (GitHub models) models.
- **DeepSeek AI Chat** (`CrestApps.OrchardCore.DeepSeek`): AI-powered chat using Azure DeepSeek cloud service.
- **Ollama AI Chat** (`CrestApps.OrchardCore.Ollama`): AI-powered chat using Azure Ollama service.


![Screenshot of the admin chat](../../../docs/images/admin-ui-sample.gif)
