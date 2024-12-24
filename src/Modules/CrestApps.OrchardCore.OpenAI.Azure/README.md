# Features

## Azure OpenAI Services

The **Azure OpenAI Services** feature (`CrestApps.OrchardCore.OpenAI.Azure`) provides the necessary core services to integrate with Azure OpenAI. This module is a dependency-managed feature, meaning it will automatically be enabled or disabled based on demand. Manual enabling or disabling is not supported.

Before using this feature, you must configure the necessary services. You can configure the settings via any of the available methods. Below is an example of how to configure the services using the `appsettings.json` file:

```json
{
  "OrchardCore": {
    "CrestApps_Azure_Arm": {
      "ClientId": "<!-- Your Azure ClientId goes here -->",
      "ClientSecret": "<!-- Your Azure ClientSecret goes here -->",
      "SubscriptionId": "<!-- Your Azure SubscriptionId goes here -->",
      "TenantId": "<!-- Your Azure TenantId goes here -->"
    },
    "CrestApps_OpenAI_Azure": {
      "AccountName": "<!-- Your Azure Cognitive Account Name -->",
      "ResourceGroupName": "<!-- Your Azure Cognitive Resource Group Name -->"
    }
  }
}
```

## Azure OpenAI

The **Azure OpenAI** feature (`CrestApps.OrchardCore.OpenAI.Azure.Standard`) enables you to use Azure OpenAI services to create AI Chat Profiles.

### Recipes

If you're utilizing the Recipes feature, you can create an Azure profile by using the following recipe step:

```json
{
  "steps":[
    {
      "name":"AIChatProfile",
      "profiles":[
        {
          "Source":"Azure",
          "Title":"Example Profile",
          "DeploymentName":"<!-- Your Azure model deployment name goes here -->",
          "SystemMessage":"You are an AI assistant that helps people find information.",
          "Temperature":null,
          "TopP":null,
          "FrequencyPenalty":null,
          "PresencePenalty":null,
          "TokenLength":null,
          "PastMessagesCount":null,
          "Strictness":null,
          "TopNDocuments":null
        }
      ]
    }
  ]
}
```

## Azure OpenAI with Azure AI Search

The **Azure OpenAI with Azure AI Search** feature (`CrestApps.OrchardCore.OpenAI.Azure.AISearch`) allows you to utilize Azure OpenAI services on your custom data stored in the Azure AI Search index to create AI Chat Profiles. Enabling this module will automatically activate the **Azure AI Search** feature in OrchardCore. To connect your AI chat, navigate to `Search` > `Indexing` > `Azure AI Indices` and add an index.

### Recipes

If you're using the Recipes feature, you can create an Azure profile using the following recipe step:

```json
{
  "steps":[
    {
      "name":"AIChatProfile",
      "profiles":[
        {
          "Source":"AzureAISearch",
          "Title":"Example Profile",
          "IndexName":"<!-- Your Azure index name goes here -->",
          "DeploymentName":"<!-- Your Azure model deployment name goes here -->",
          "SystemMessage":"You are an AI assistant that helps people find information.",
          "Temperature":null,
          "TopP":null,
          "FrequencyPenalty":null,
          "PresencePenalty":null,
          "TokenLength":null,
          "PastMessagesCount":null,
          "Strictness":null,
          "TopNDocuments":null
        }
      ]
    }
  ]
}
```
