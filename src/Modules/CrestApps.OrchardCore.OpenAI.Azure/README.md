# Features

## Azure OpenAI Services Feature

The **Azure OpenAI-Powered Artificial Intelligence** feature (`CrestApps.OrchardCore.OpenAI.Azure`) provides the primary functionality for integrating with Azure OpenAI. This module is dependency-managed, meaning it is automatically enabled or disabled based on demand. Manual activation or deactivation is not supported.

## Azure OpenAI-Powered Artificial Intelligence Deployments Feature

The **Azure OpenAI-Powered Artificial Intelligence Deployments** feature (`CrestApps.OrchardCore.OpenAI.Azure.Deployments`) enables integration with Azure OpenAI deployments. Initially, certain UI features for creating Azure deployments were unavailable. To create a deployment, use the **Azure AI Foundry** portal, then configure it in the UI by selecting the `Azure` source and entering the deployment name.

Before utilizing this feature, ensure that the required services are configured. These can be set up in various ways. Below is an example of configuring these services within the `appsettings.json` file:

```json
{
  "CrestApps_AI": {
    "Connections": {
      "Azure": [
        {
          "Name": "<!-- A unique name for your connection. It's recommended to match your Azure account's AccountName -->",
          "TenantId": "<!-- Your Azure TenantId -->",
          "ClientId": "<!-- Your Azure ClientId -->",
          "ClientSecret": "<!-- Your Azure ClientSecret -->",
          "SubscriptionId": "<!-- Your Azure SubscriptionId -->",
          "AccountName": "<!-- Your Azure Cognitive Account Name -->",
          "ResourceGroupName": "<!-- Your Azure Cognitive Resource Group Name -->",
          "ApiKey": "<!-- API Key to connect to your Azure AI instance -->"
        }
      ]
    }
  }
}
```

#### How to Retrieve the Required Information from the Azure Portal

##### 1. Retrieve the Tenant ID
1. Open the **Azure Portal** and search for **Microsoft Entra ID** (formerly Azure Active Directory).
2. In the **Overview** section, locate and copy the **Tenant ID**.

##### 2. Obtain the Client ID and Client Secret
1. In **Microsoft Entra ID** within the **Azure Portal**, navigate to **Manage** > **App registrations**.
2. Either create a new application or select an existing one.
3. In the application details:
   - Copy the **Application (client) ID**.
   - Copy the **Directory (tenant) ID**.
4. Navigate to the **Certificates & secrets** section.
   - Click **New client secret**.
   - Provide a description and expiration date, then save.
   - Copy the **Client Secret value** (ensure to save it securely, as it will not be shown again).

##### 3. Retrieve the API Key for Azure OpenAI
1. Navigate to your **Azure OpenAI** instance in the **Azure Portal**.
2. Go to **Resource Management** > **Keys and Endpoint**.
3. Click **Show Keys** and copy one of the available keys.

### Recipes

If you're utilizing the Recipes feature, you can import all deployments from your Azure account as shown below:

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

Alternatively, if you have multiple connections, specify which ones to import. For example, to import deployments from `us-west-connection` and `canada-east-connection`:

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

## Azure OpenAI-Powered Artificial Intelligence Chat Feature

The **Azure OpenAI-Powered Artificial Intelligence Chat** feature (`CrestApps.OrchardCore.OpenAI.Azure.Standard`) enables the creation of AI Chat Profiles using Azure OpenAI services.

!!! info
    This feature depends on the **Azure OpenAI-Powered Artificial Intelligence Deployments** feature. Be sure to configure it properly before using this feature.

### Recipes

When using the Recipes feature, you can create an Azure profile with the following recipe step:

```json
{
  "steps":[
    {
      "name":"AIChatProfile",
      "profiles":[
        {
          "Source":"Azure",
          "Name":"Example Profile",
          "WelcomeMessage": "What do you want to know?",
          "FunctionNames": [],
          "Type": "Chat",
          "TitleType": "InitialPrompt",
          "PromptTemplate": null,
          "DeploymentId":"<!-- The deployment id for the deployment. -->",
          "SystemMessage":"You are an AI assistant that helps people find information.",
          "Properties": 
          {
              "OpenAIChatProfileMetadata": 
              {
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

## Azure OpenAI-Powered Artificial Intelligence Chat with Azure AI Search Feature

The **Azure OpenAI-Powered Artificial Intelligence Chat with Azure AI Search** feature (`CrestApps.OrchardCore.OpenAI.Azure.AISearch`) enables the use of Azure OpenAI services on custom data stored in an Azure AI Search index to create AI Chat Profiles. Enabling this module will automatically activate the **Azure AI Search** feature within OrchardCore. To connect your AI chat, navigate to `Search` > `Indexing` > `Azure AI Indices` and add an index.

!!! info
    This feature depends on the **Azure OpenAI Deployments** feature. Ensure the **Azure OpenAI Deployments** feature is configured first.

### Recipes

When using the Recipes feature, you can create an Azure profile with the following recipe step:

```json
{
  "steps":[
    {
      "name":"AIChatProfile",
      "profiles":[
        {
          "Source":"AzureAISearch",
          "Name":"Example Profile",
          "WelcomeMessage": "What do you want to know?",
          "FunctionNames": [],
          "Type": "Chat",
          "TitleType": "InitialPrompt",
          "PromptTemplate": null,
          "DeploymentId":"<!-- The deployment id for the deployment. -->",
          "SystemMessage":"You are an AI assistant that helps people find information.",
          "Properties": 
          {
              "OpenAIChatProfileMetadata": 
              {
                  "Temperature":null,
                  "TopP":null,
                  "FrequencyPenalty":null,
                  "PresencePenalty":null,
                  "MaxTokens":null,
                  "PastMessagesCount":null
              },
              "AzureAIChatProfileAISearchMetadata":
              {
                  "IndexName": "<!-- The index name to search -->",
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
