# CrestApps - Orchard Core

CrestApps provides a collection of open-source modules designed to enhance **Orchard Core**, a powerful application framework built on **ASP.NET Core**.  

## Table of Contents

- [Overview](#overview)
- [Project Structure](#project-structure)
  - [Example Structure](#example-structure)
- [Available Modules](#available-modules)
  - [Artificial Intelligence Suite](#artificial-intelligence-suite)
    - [AI Module](#ai-module)
    - [AI Chat Module](#ai-chat-module)
    - [AI Chat Interactions Module](#ai-chat-interactions-module)
    - [Orchard Core AI Agent Module](#orchard-core-ai-agent-module)
    - [Model Context Protocol (MCP) Module](#model-context-protocol-mcp-module)
    - [Azure OpenAI Module](#azure-openai-module)
    - [OpenAI Module](#openai-module)
    - [Azure AI Inference Module](#azure-ai-inference-module)
    - [Ollama Module](#ollama-module)
  - [Omnichannel Suite](#omnichannel-suite)
    - [Omnichannel (Orchestrator)](#omnichannel-orchestrator)
    - [Omnichannel Management (Mini-CRM)](#omnichannel-management-mini-crm)
    - [SMS Omnichannel Automation (AI)](#sms-omnichannel-automation-ai)
    - [Omnichannel (Azure Event Grid)](#omnichannel-azure-event-grid)
  - [Standard Modules](#standard-modules)
    - [Users Module](#users-module)
    - [SignalR Module](#signalr-module)
    - [Enhanced Roles Module](#enhanced-roles-module)
    - [Content Access Control Module](#content-access-control-module)
    - [Resources Module](#resources-module)
    - [CrestApps Recipes Module](#crestapps-recipes-module)
- [Getting Started](#getting-started)
  - [Running Locally](#running-locally)
- [Package Management](#package-management)
  - [Production Packages](#production-packages)
  - [Preview Package Feed](#preview-package-feed)
  - [Adding the Preview Feed](#adding-the-preview-feed)
    - [In Visual Studio](#in-visual-studio)
    - [Via NuGet.config](#via-nugetconfig)
- [Contributing](#contributing)
- [License](#license)

## Overview

Orchard Core offers a **flexible and scalable** foundation for building dynamic, data-driven websites and applications. CrestApps modules are developed to further improve this framework, focusing on:  

- **Modularity** – Independent modules allow for seamless integration based on project requirements.  
- **Security** – Designed following industry best practices to ensure application safety.  
- **Performance** – Optimized for speed and efficiency to maximize Orchard Core's potential.  

## Project Structure

The CrestApps repository is organized for clarity and ease of use. You can find all modules in the `src/Modules` folder, with each structured for independent usage and configuration.  

- **Modules Folder:**
  Contains all CrestApps modules. Each module includes a `README.md` file with setup and integration details.  

### Example Structure:
```
src/
└── Modules/
    ├── CrestApps.OrchardCore.Users/
    │   ├── README.md
    │   ├── Manifest.cs
    │   ├── ...
    └── OtherModules/
        ├── README.md
        ├── ...
```

To get started with any module, refer to its respective `README.md` file for detailed setup instructions.  

## Available Modules
You can install individual modules into your web project as needed, or install the `CrestApps.OrchardCore.Cms.Core.Targets` package to include all modules at once.

### Artificial Intelligence Suite

#### AI Module
Provides services for all AI modules and provide the interface for managing AI profiles and AI Deployments. See the [AI Module README](src/Modules/CrestApps.OrchardCore.AI/README.md) for more details.  

#### AI Chat Module
Provides interface for interacting with AI chat models like **ChatGPT** and others. See the [AI Chat Module README](src/Modules/CrestApps.OrchardCore.AI.Chat/README.md) for more details.  

#### AI Chat Interactions Module
Enables ad-hoc AI chat experiences with configurable parameters, document upload, and RAG (Retrieval Augmented Generation) support. Users can chat with AI models without predefined profiles and upload documents to chat against their own data. See the [AI Chat Interactions README](src/Modules/CrestApps.OrchardCore.AI.Chat.Interactions/README.md) for more details.

**Extension modules:**
- [AI Chat Interactions - Documents](src/Modules/CrestApps.OrchardCore.AI.Chat.Interactions.Documents/README.md) - Document upload and RAG support
- [AI Chat Interactions - PDF](src/Modules/CrestApps.OrchardCore.AI.Chat.Interactions.Pdf/README.md) - PDF document support
- [AI Chat Interactions - OpenXml](src/Modules/CrestApps.OrchardCore.AI.Chat.Interactions.OpenXml/README.md) - Word, Excel, PowerPoint support
- [AI Chat Interactions - Documents (Azure AI Search)](src/Modules/CrestApps.OrchardCore.AI.Chat.Interactions.Documents.AzureAI/README.md) - Azure AI Search provider for documents
- [AI Chat Interactions - Documents (Elasticsearch)](src/Modules/CrestApps.OrchardCore.AI.Chat.Interactions.Documents.Elasticsearch/README.md) - Elasticsearch provider for documents

#### Orchard Core AI Agent Module
Enhances the **AI Module** by providing AI Agents to perform tasks on your Orchard Core site. For more details, see the [Orchard Core AI Agent Module README](src/Modules/CrestApps.OrchardCore.AI.Agent/README.md).

#### Model Context Protocol (MCP) Module
Provides support for the Model Context Protocol (MCP) and contains multiple features:

- **MCP Client (feature)** — Client-side components to connect to remote MCP servers. See [README](src/Modules/CrestApps.OrchardCore.AI.Mcp/README.md) file for details.
- **MCP Server (feature)** — Enables Orchard Core to act as an MCP server, exposing AI tools and capabilities to external MCP-compatible clients such as AI agents, IDEs, copilots, and automation tools. See [README-server](src/Modules/CrestApps.OrchardCore.AI.Mcp/README-server.md) file for details.

#### Azure OpenAI Module
Extends the **AI Module** by integrating **Azure OpenAI** services. See the [Azure OpenAI Module README](src/Modules/CrestApps.OrchardCore.OpenAI.Azure/README.md).  

#### OpenAI Module
Extends the **AI Module** by integrating **OpenAI**-powered services. You can connect to any provider that adheres to OpenAI standard. Here are few providers:

- DeepSeek
- Google Gemini
- Together AI
- vLLM
- Cloudflare Workers AI 
- LM Studio
- KoboldCpp
- text-gen-webui 
- FastChat
- LocalAI
- llama-cpp-python
- TensorRT-LLM
- BerriAI/litellm

See the [OpenAI Module README](src/Modules/CrestApps.OrchardCore.OpenAI/README.md) for more details.  

#### Azure AI Inference Module
Extends the **AI Module** by integrating **Azure AI Inference** services. See the [Azure AI Inference Module README](src/Modules/CrestApps.OrchardCore.AzureAIInference/README.md).  

#### Ollama Module
Extends the **AI Module** by integrating any **Ollama** model. See the [Ollama Module README](src/Modules/CrestApps.OrchardCore.Ollama/README.md).  

### Omnichannel Suite

The Omnichannel suite provides a unified communication and activity orchestration layer across channels (SMS, Email, Phone, and more), with a mini-CRM UI and optional AI-driven automation.

#### Omnichannel (Orchestrator)
The foundation of all communication channels. Provides the core orchestration services and generic webhook entry points. See the [Omnichannel README](src/Modules/CrestApps.OrchardCore.Omnichannel/README.md).

#### Omnichannel Management (Mini-CRM)
A mini-CRM that lets you manage contacts, subjects, campaigns, dispositions, activities, and activity batches, and drive next-activity behavior via Orchard Core Workflows. See the [Omnichannel Management README](src/Modules/CrestApps.OrchardCore.Omnichannel.Managements/README.md).

#### SMS Omnichannel Automation (AI)
Allows AI to automate chatting with customers/contacts using SMS. You define how the AI should handle conversations, and it acts as an agent communicating through your SMS provider. See the [SMS Omnichannel Automation README](src/Modules/CrestApps.OrchardCore.Omnichannel.Sms/README.md).

#### Omnichannel (Azure Event Grid)
Integrates Azure Event Grid to receive communication events (e.g. from your SMS provider) and route them into Omnichannel. See the [Omnichannel Event Grid README](src/Modules/CrestApps.OrchardCore.Omnichannel.EventGrid/README.md).

### Standard Modules

#### Users Module
Enhances user management with customizable display names and avatars. See the [Users Module README](src/Modules/CrestApps.OrchardCore.Users/README.md) for details.  

#### SignalR Module
The **SignalR** module enables seamless integration of SignalR within Orchard Core. See the [SignalR Module README](src/Modules/CrestApps.OrchardCore.SignalR/README.md).  

#### Enhanced Roles Module
Extends the Orchard Core Roles module with additional reusable components. See the [Roles Module README](src/Modules/CrestApps.OrchardCore.Roles/README.md) for details.  

#### Content Access Control Module
Enables you to restrict content items based on user roles. See the [Content Access Control Module README](src/Modules/CrestApps.OrchardCore.ContentAccessControl/README.md) for details.  

#### Resources Module
Provides additional resources to accelerate development. See the [Resources Module README](src/Modules/CrestApps.OrchardCore.Resources/README.md).  

#### CrestApps Recipes Module
Provides a structured way to define and retrieve recipe steps. See the [CrestApps Recipes Module README](src/Modules/CrestApps.OrchardCore.Recipes/README.md).  

## Getting Started

### Running Locally

Follow these steps to get started with CrestApps:  

1. **Clone the Repository:**  
    ```sh
    git clone https://github.com/CrestApps/CrestApps.OrchardCore.git
    ```  

2. **Navigate to the Project Directory:**  
    ```sh
    cd CrestApps.OrchardCore
    ```  

3. **Build the Solution:**  
    Ensure you have the required **.NET SDK** installed, then run:  
    ```sh
    dotnet build
    ```  

4. **Launch the Application:**  
    ```sh
    dotnet run
    ```  

5. **Enable Modules:**  
   Access the **Orchard Core Admin Dashboard** to enable desired CrestApps modules.  

## Package Management 

This project is actively maintained and evolves alongside Orchard Core.

* If you're using Orchard Core versions from `2.1` up to `2.3`, please use package version `1.2.x`.
* For Orchard Core `3.0.0-preview-18908` and later, please use version `2.0.0-beta-0009` or newer.

**Note:** In Orchard Core v3 multiple breaking changes were introduced to improve the framework. As a result, we had to divide development into two branches to maintain compatibility.

### Production Packages
Stable releases are available on [NuGet.org](https://www.nuget.org/).  

### Preview Package Feed
[![Hosted By: Cloudsmith](https://img.shields.io/badge/OSS%20hosting%20by-cloudsmith-blue?logo=cloudsmith&style=for-the-badge)](https://cloudsmith.com)  

For the latest updates and preview packages, visit the [Cloudsmith CrestApps OrchardCore repository](https://cloudsmith.io/~crestapps/repos/crestapps-orchardcore).  

### Adding the Preview Feed

#### In Visual Studio 
1. Open **NuGet Package Manager Settings** (under *Tools*).  
2. Add a new package source:  
   - **Name:** `CrestAppsPreview`  
   - **URL:** `https://nuget.cloudsmith.io/crestapps/crestapps-orchardcore/v3/index.json`  

#### Via NuGet.config
Alternatively, update your **NuGet.config** file:  

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="NuGet" value="https://api.nuget.org/v3/index.json" />
    <add key="CrestAppsPreview" value="https://nuget.cloudsmith.io/crestapps/crestapps-orchardcore/v3/index.json" />
  </packageSources>
  <disabledPackageSources />
</configuration>
```

## Contributing

We welcome contributions from the community! To contribute:  

1. **Fork the repository.**  
2. **Create a new branch** for your feature or bug fix.  
3. **Make your changes** and commit them with clear messages.  
4. **Push your changes** to your fork.  
5. **Submit a pull request** to the main repository.  

## License

CrestApps is licensed under the **MIT License**. See the [LICENSE](https://github.com/git/git-scm.com/blob/main/MIT-LICENSE.txt) file for more details.
