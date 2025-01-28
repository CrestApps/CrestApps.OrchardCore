# Features

## Azure OpenAI Services

The **Azure OpenAI Services** feature (`CrestApps.OrchardCore.OpenAI.Azure`) provides the core functionality to integrate with Azure OpenAI. This module is dependency-managed, meaning it will automatically be enabled or disabled based on demand, and manual activation or deactivation is not supported.

## Azure OpenAI Deployments

The **Azure OpenAI Deployments** feature (`CrestApps.OrchardCore.OpenAI.Azure.Deployments`) allows integration with Azure OpenAI deployments. Initially, certain UI features required for direct creation of Azure deployments were not available. To create a deployment, use the **Azure AI Foundry** portal, then configure it within the UI by selecting the `Azure` source and entering the same deployment name.

Before using this feature, ensure that the necessary services are configured. You can configure them using different methods. Below is an example of how to set up these services in the `appsettings.json` file:

```json
{
  "CrestApps_OpenAI":{
    "Connections":{
      "Azure":[
        {
          "Name":"<!-- A unique name for your connection. It’s recommended to match your Azure account's AccountName -->",
          "TenantId":"<!-- Your Azure TenantId -->",
          "ClientId":"<!-- Your Azure ClientId -->",
          "ClientSecret":"<!-- Your Azure ClientSecret -->",
          "SubscriptionId":"<!-- Your Azure SubscriptionId -->",
          "AccountName":"<!-- Your Azure Cognitive Account Name -->",
          "ResourceGroupName":"<!-- Your Azure Cognitive Resource Group Name -->",
          "ApiKey":"<!-- API Key to connect to your Azure AI instance -->"
        }
      ]
    }
  }
}
```

#### How to Retrieve Required Information from the Azure Portal

##### 1. Retrieve the Tenant ID
1. Open the **Azure Portal** and search for **Microsoft Entra ID** (formerly Azure Active Directory).
2. In the **Overview** section, locate and copy the **Tenant ID**.

##### 2. Obtain the Client ID and Client Secret
1. In the **Microsoft Entra ID** service within the **Azure Portal**, navigate to **Manage** > **App registrations**.
2. Create a new application or select an existing one.
3. In the application details:
   - Copy the **Application (client) ID**.
   - Copy the **Directory (tenant) ID**.
4. Go to the **Certificates & secrets** section.
   - Click **New client secret**.
   - Provide a description and expiration date, then save.
   - Copy the **Client Secret value** (make sure to save it securely, as it won’t be visible again after this step).

##### 3. Retrieve the API Key for Azure OpenAI
1. Navigate to your **Azure OpenAI** instance in the **Azure Portal**.
2. Go to **Resource Management** > **Keys and Endpoint**.
3. Click **Show Keys** and copy one of the available keys.

### Recipes

If you're using the Recipes feature, you can import all the deployments from your Azure account as follows:

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

Alternatively, if you have multiple connections configured, you can specify which ones to import. For example, the following imports deployments from `us-west-connection` and `canada-east-connection`:

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

## Azure OpenAI

The **Azure OpenAI** feature (`CrestApps.OrchardCore.OpenAI.Azure.Standard`) allows you to use Azure OpenAI services to create AI Chat Profiles.

!!! info
    This feature depends on the `Azure OpenAI Deployments` feature. Ensure that the `Azure OpenAI Deployments` feature is properly configured.

### Recipes

If you're using the Recipes feature, you can create an Azure profile using the following recipe step:

```json
{
  "steps":[
    {
      "name":"OpenAIChatProfile",
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

## Azure OpenAI with Azure AI Search

The **Azure OpenAI with Azure AI Search** feature (`CrestApps.OrchardCore.OpenAI.Azure.AISearch`) allows you to leverage Azure OpenAI services on custom data stored in the Azure AI Search index to create AI Chat Profiles. Enabling this module will automatically activate the **Azure AI Search** feature within OrchardCore. To connect your AI chat, navigate to `Search` > `Indexing` > `Azure AI Indices` and add an index.

!!! info
    This feature depends on the `Azure OpenAI Deployments` feature. Be sure to configure the `Azure OpenAI Deployments` feature first.

### Recipes

If you're using the Recipes feature, you can create an Azure profile with the following recipe step:

```json
{
  "steps":[
    {
      "name":"OpenAIChatProfile",
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
