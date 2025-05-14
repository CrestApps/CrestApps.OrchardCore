# Features

## Azure OpenAI Services Integration

The Azure OpenAI Services feature (`CrestApps.OrchardCore.OpenAI.Azure`) integrates seamlessly with Azure OpenAI. This module is dependency-driven, meaning it automatically enables or disables itself based on usage requirements.

### Configuration

Add the following section to your `appsettings.json` to configure Azure OpenAI:

```json
{
  "OrchardCore": {
    "CrestApps_AI": {
      "Providers": {
        "Azure": {
          "DefaultConnectionName": "<!-- Default connection name -->",
          "DefaultDeploymentName": "<!-- Default deployment name -->",
          "Connections": {
            "<!-- Unique connection name, ideally your Azure AccountName -->": {
              "Endpoint": "https://<!-- Your Azure Resource Name -->.openai.azure.com/",
              "AuthenticationType": "ApiKey",
              "ApiKey": "<!-- API Key for your Azure AI instance -->",
              "DefaultDeploymentName": "<!-- Default deployment name -->"
            }
          }
        }
      }
    }
  }
}
```

Valid values for `AuthenticationType` are: `Default`, `ManagedIdentity`, or `ApiKey`. If using `ApiKey`, the `ApiKey` field is required.

### How to Retrieve Azure OpenAI Credentials

#### Get the API Key and Endpoint

1. Open the Azure Portal and navigate to your Azure OpenAI instance.
2. Go to **Resource Management** > **Keys and Endpoint**.
3. Copy the **Endpoint**.
4. Copy one of the two available **API keys**.

## Azure OpenAI Chat Feature

This feature allows the creation of AI profiles using Azure OpenAI chat capabilities.

### Recipe Configuration

Define an AI profile with the following step in your recipe:

```json
{
  "steps": [
    {
      "name": "AIProfile",
      "profiles": [
        {
          "Source": "Azure",
          "Name": "ExampleProfile",
          "DisplayText": "Example Profile",
          "WelcomeMessage": "What do you want to know?",
          "FunctionNames": [],
          "Type": "Chat",
          "TitleType": "InitialPrompt",
          "PromptTemplate": null,
          "ConnectionName": "<!-- Connection name (optional) -->",
          "DeploymentId": "<!-- Deployment ID (optional) -->",
          "Properties": {
            "AIProfileMetadata": {
              "SystemMessage": "You are an AI assistant that helps people find information.",
              "Temperature": null,
              "TopP": null,
              "FrequencyPenalty": null,
              "PresencePenalty": null,
              "MaxTokens": null,
              "PastMessagesCount": null
            }
          }
        }
      ]
    }
  ]
}
```

## Azure OpenAI – Bring Your Own Data

This feature builds on the **AI Data Source Management** system, enabling Azure OpenAI to interact with your own data repositories. It is automatically activated by dependent features and cannot be manually enabled or disabled.

### AI Profile with Data Source Recipe Example

To define a profile that uses a data-source, use this recipe structure:

```json
{
  "steps": [
    {
      "name": "AIProfile",
      "profiles": [
        {
          "Source": "AzureOpenAIOwnData",
          "Name": "ExampleProfile",
          "DisplayText": "Example Profile",
          "WelcomeMessage": "What do you want to know?",
          "FunctionNames": [],
          "Type": "Chat",
          "TitleType": "InitialPrompt",
          "ConnectionName": "<!-- Connection name (optional) -->",
          "DeploymentId": "<!-- Deployment ID (optional) -->",
          "Properties": {
            "AIProfileMetadata": {
              "SystemMessage": "You are an AI assistant that helps people find information.",
              "Temperature": null,
              "TopP": null,
              "FrequencyPenalty": null,
              "PresencePenalty": null,
              "MaxTokens": null,
              "PastMessagesCount": null
            },
            "AIProfileDataSourceMetadata": {
              "DataSourceId": "<!-- Data source ID -->",
              "DataSourceType": "<!-- Data source type, e.g., 'azure_search' -->"
            }
          }
        }
      ]
    }
  ]
}
```
## Azure AI Search-Powered Data Source

This feature extends the **Bring Your Own Data** capability by enabling integration with Azure AI Search. It allows your models to use Azure AI Search as a source for the data.

### Azure AI Search-Powered Data Source Recipe Example

To define a Azure AI Search-Powered data-source, use this recipe structure:

```
{
  "steps": [
    {
      "name": "AIDataSource",
      "DataSources": [
        {
          "ProfileSource": "AzureOpenAIOwnData",
          "Type": "azure_search",
          "DisplayText": "Articles in Azure AI Search",
          "Properties": {
            "AzureAIProfileAISearchMetadata": {
              "IndexName": "articles",
              "Strictness": 3,
              "TopNDocuments": 5
            }
          }
        }
      ]
    }
  ]
}
```
---

## Elasticsearch-Powered Data Source

This feature extends the **Bring Your Own Data** capability by enabling integration with **Elasticsearch**, allowing your models to use Elasticsearch as a data source.

### Elasticsearch-Powered Data Source Recipe Example

To define an Elasticsearch-Powered data-source, use this recipe structure:

```
{
  "steps": [
    {
      "name": "AIDataSource",
      "DataSources": [
        {
          "ProfileSource": "AzureOpenAIOwnData",
          "Type": "elasticsearch",
          "DisplayText": "Articles in Elasticsearch",
          "Properties": {
            "AzureAIProfileElasticsearchMetadata": {
              "IndexName": "articles",
              "Strictness": 3,
              "TopNDocuments": 5
            }
          }
        }
      ]
    }
  ]
}
```
---

### Configuration

This functionality relies on the `OrchardCore.Search.Elasticsearch` module. Refer to the [official Orchard Core documentation](https://docs.orchardcore.net/en/latest/reference/modules/Elasticsearch/#elasticsearch-configuration) for basic Elasticsearch configuration.

If your Elasticsearch cluster has security enabled, you’ll also need to generate an **API key** via Kibana to allow the chat client to access Elasticsearch indexes.

---

### Generating an API Key in Kibana

#### 1. Log in to Kibana

* Open Kibana in your browser.
* Sign in using an account with permission to manage API keys.

#### 2. Navigate to API Key Management

* Go to **Management** > **Stack Management**.
* Under **Security**, select **API Keys**.

#### 3. Create a New API Key

* Click **Create API key**.
* Fill in the form:

  * **Name**: e.g., `search-service-key`
  * **Expiration**: Optional (e.g., `1d` for one day)
  * **Privileges**: Optional role descriptors for access control

#### 4. Copy and Format the API Key

* After creation, Kibana will display the key in **Base64** format.

You can use this key in one of two ways:

##### Option 1: Using `encoded_api_key`

Use the Base64-encoded key directly by setting `AuthenticationType` to `encoded_api_key`. For example, in your `appsettings.json`:

```json
{
  "OrchardCore": {
    "OrchardCore_Elasticsearch": {
      "AuthenticationType": "encoded_api_key",
      "EncodedApiKey": "<!-- Base64 encoded key -->"
    }
  }
}
```

##### Option 2: Using `key_and_key_id`

You can also use the `key_and_key_id` authentication type, which requires both the API key ID and the key itself.

To obtain these:

* In Kibana, click the dropdown next to the created API key and switch the view from **Base64** to **JSON**.
* You will see details like:

```json
{
  "id": "<!-- Key ID -->",
  "name": "<!-- Key Name -->",
  "api_key": "<!-- Key -->",
  "encoded": "<!-- Base64 encoded ID + Key -->"
}
```

Then configure `appsettings.json` like this:

```json
{
  "OrchardCore": {
    "OrchardCore_Elasticsearch": {
      "AuthenticationType": "key_and_key_id",
      "KeyId": "<!-- Key ID -->",
      "Key": "<!-- Key -->"
    }
  }
}
```

## MongoDB-Powered Data Source

This feature extends the **Bring Your Own Data** capability by enabling integration with **Mongo DB**, allowing your models to use Mongo DB as a data source.

### Elasticsearch-Powered Data Source Recipe Example

To define an Elasticsearch-Powered data-source, use this recipe structure:

```
{
  "steps": [
    {
      "name": "AIDataSource",
      "DataSources": [
        {
          "ProfileSource": "AzureOpenAIOwnData",
          "Type": "mongo_db",
          "DisplayText": "Articles in Mongo DB",
          "Properties": {
            "AzureAIProfileMongoDBMetadata": {
              "IndexName": "articles",
              "Strictness": 3,
              "TopNDocuments": 5,
              "EndpointName": "<!-- Mongo DB endpoint name -->",
              "AppName": "<!-- Mongo DB application name -->",
              "CollectionName": "<!-- Mongo DB collection name -->",
              "Authentication": {
                  "Type": "username_and_password",
                  "Username": "<!-- Mongo DB username -->",
                  "Password": "<!-- Mongo DB password -->"
              }
            }
          }
        }
      ]
    }
  ]
}
```

## Registering a Custom Data Source

Register a new data source using the following code:

```csharp
services.AddAIDataSource(AzureOpenAIConstants.AISearchImplementationName, "azure_search", o =>
{
    o.DisplayName = S["Azure OpenAI with Azure AI Search"];
    o.Description = S["Enables AI models to use Azure AI Search as a data source for your data."];
});
```

## Implementing the Data Source Handler

Implement the `IAzureOpenAIDataSourceHandler` interface to wire your data source into the chat system. You can reference `AzureAISearchOpenAIDataSourceHandler` in the source code as an example.

