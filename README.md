# CrestApps - Orchard Core

CrestApps provides a collection of open-source modules designed to enhance **Orchard Core**, a powerful application framework built on **ASP.NET Core**.  

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

#### Orchard Core AI Agent Module
Enhances the **AI Module** by providing AI Agents to perform tasks on your Orchard Core site. For more details, see the [Orchard Core AI Agent Module README](src/Modules/CrestApps.OrchardCore.AI.Agent/README.md).

#### Model Context Protocol (MCP) Module
Enhances the **AI Module** by adding support for connecting to any **MCP server**, whether hosted locally or remotely. For more details, see the [MCP Module README](src/Modules/CrestApps.OrchardCore.AI.Mcp/README.md).

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
* For Orchard Core `3.0.0-preview-18795` and later, please use version `2.0.0-beta-0006` or newer.

**Note:** The reason for this split is that Orchard Core `3.0.0-preview-18669` upgraded to YesSql `5.4.1`, which introduced a binary breaking change. As a result, we had to divide development into two branches to maintain compatibility.

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
