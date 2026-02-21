---
sidebar_label: Azure OpenAI
sidebar_position: 2
title: Azure OpenAI Integration
description: Azure OpenAI integration for AI chat profiles, deployments, and connections in Orchard Core.
---

## Table of Contents

- [Azure OpenAI Services Integration](#azure-openai-services-integration)
  - [Configuration](#configuration)
  - [How to Retrieve Azure OpenAI Credentials](#how-to-retrieve-azure-openai-credentials)
- [Azure OpenAI Chat Feature](#azure-openai-chat-feature)
  - [Recipe Configuration](#recipe-configuration)

## Azure OpenAI Integration

The Azure OpenAI Chat feature (`CrestApps.OrchardCore.OpenAI.Azure`) integrates seamlessly with Azure OpenAI. Enable this feature to use Azure OpenAI models for AI chat profiles, deployments, and connections.

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
              "DefaultDeploymentName": "<!-- Default deployment name -->",
              "DefaultUtilityDeploymentName": "<!-- Optional: a lightweight model for auxiliary tasks -->"
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

## RAG / Data Sources

Data sources and RAG are now implemented in the provider-agnostic `CrestApps.OrchardCore.AI.DataSources` module.

See: [AI Data Sources](../ai/data-sources/)
