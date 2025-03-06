# Features

## Azure OpenAI Services Integration

The **Azure OpenAI Services** feature (`CrestApps.OrchardCore.OpenAI.Azure`) is designed to seamlessly integrate with Azure OpenAI. This module is managed via dependencies, enabling or disabling it automatically based on demand.

### Configuration

To configure the Azure OpenAI services, add the following section to your `appsettings.json` file:

```json
{
  "OrchardCore":{
    "CrestApps_AI":{
      "Providers":{
        "Azure":{
          "DefaultConnectionName":"<!-- Default connection name -->",
          "DefaultDeploymentName":"<!-- Default deployment name -->",
          "Connections":{
            "<!-- Unique connection name, recommended to match your Azure AccountName -->":{
              "Endpoint":"https://<!-- Your Azure Resource Name -->.openai.azure.com/",
              "AuthenticationType": "ApiKey",
              "ApiKey":"<!-- API Key to connect to your Azure AI instance -->",
              "DefaultDeploymentName":"<!-- Default deployment name -->"
            }
          }
        }
      }
    }
  }
}
```

Authentication Type in the connection can be `Default`, `ManagedIdentity` or `ApiKey`. When using `ApiKey` authentication type, `ApiKey` is required.

### Retrieving the Required Information from the Azure Portal

#### Retrieving the API Key for Azure OpenAI
1. In the **Azure Portal**, go to your **Azure OpenAI** instance.
2. Navigate to **Resource Management** > **Keys and Endpoint**.
3. Click **Show Keys** and copy one of the available keys.

### Recipes

You c

When using the Recipes feature, you can import all deployments from your Azure account with the following configuration:

```json
{
  "steps": [
    {
       "name": "ImportAzureOpenAIDeployment",
       "ConnectionNames": "all"
    }
  ]
}
```

Alternatively, to import deployments from specific connections like `us-west-connection` and `canada-east-connection`, use this configuration:

```json
{
  "steps": [
    {
       "name": "ImportAzureOpenAIDeployment",
       "ConnectionNames": [
            "us-west-connection",
            "canada-east-connection"
       ]
    }
  ]
}
```

## Azure OpenAI Chat Feature

The **Azure OpenAI Chat** feature enables the creation of AI Profiles using Azure OpenAI services.

### Recipes

When using Recipes, you can define an Azure AI profile with the following step:

```json
{
  "steps":[
    {
      "name":"AIProfile",
      "profiles":[
        {
          "Source":"Azure",
          "Name":"ExampleProfile",
          "DisplayText": "Example Profile",
          "WelcomeMessage": "What do you want to know?",
          "FunctionNames": [],
          "Type": "Chat",
          "TitleType": "InitialPrompt",
          "PromptTemplate": null,
          "ConnectionName":"<!-- The connection name for the deployment (leave blank for default) -->",
          "DeploymentId":"<!-- The deployment ID (leave blank for default) -->",
          "Properties": 
          {
              "AIProfileMetadata": 
              {
                  "SystemMessage":"You are an AI assistant that helps people find information.",
                  "Temperature":null,
                  "TopP":null,
                  "FrequencyPenalty":null,
                  "PresencePenalty":null,
                  "MaxTokens":null,
                  "PastMessagesCount":null
              }
          }
        }
      ]
    }
  ]
}
```

## Azure OpenAI Chat with Your Data Feature

The **Azure OpenAI Chat with Your Data** feature allows the use of Azure OpenAI services on custom data stored in an Azure AI Search index to create AI Chat Profiles. Activating this module will automatically enable the **Azure AI Search** feature in OrchardCore. To link your AI chat to a search index, go to `Search` > `Indexing` > `Azure AI Indices` and add your index.

### Recipes

When using Recipes, you can create an Azure AI profile with custom data from Azure AI Search by following this recipe step:

```json
{
  "steps":[
    {
      "name":"AIProfile",
      "profiles":[
        {
          "Source":"AzureAISearch",
          "Name":"ExampleProfile",
          "DisplayText": "Example Profile",
          "WelcomeMessage": "What do you want to know?",
          "FunctionNames": [],
          "Type": "Chat",
          "TitleType": "InitialPrompt",
          "ConnectionName":"<!-- Connection name for the deployment (leave blank for default) -->",
          "DeploymentId":"<!-- Deployment ID (leave blank for default) -->",
          "Properties": 
          {
              "AIProfileMetadata": 
              {
                  "SystemMessage":"You are an AI assistant that helps people find information.",
                  "Temperature":null,
                  "TopP":null,
                  "FrequencyPenalty":null,
                  "PresencePenalty":null,
                  "MaxTokens":null,
                  "PastMessagesCount":null
              },
              "AzureAIProfileAISearchMetadata":
              {
                  "IndexName": "<!-- The index name for search -->",
                  "IncludeContentItemCitations": true,
                  "Strictness":null,
                  "TopNDocuments":null
              }
          }
        }
      ]
    }
  ]
}
```
