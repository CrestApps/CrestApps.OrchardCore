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

This branch depends on Orchard Core **2.1.0** up to **2.2.0**.

### Artificial Intelligence Suite

#### AI Module
Provides services for all AI modules and provide the interface for managing AI profiles and AI Deployments. See the [AI Module README](src/Modules/CrestApps.OrchardCore.AI/README.md) for more details.  

#### AI Chat Module
Provides interface for interacting with AI chat models like **ChatGPT** and others. See the [AI Chat Module README](src/Modules/CrestApps.OrchardCore.AI.Chat/README.md) for more details.  

#### Orchard Core AI Agent Module
Enhances the **AI Module** by providing AI Agents to perform tasks on your Orchard Core site. For more details, see the [Orchard Core AI Agent Module README](src/Modules/CrestApps.OrchardCore.AI.Agent/README.md).

#### Model Context Protocol (MCP) Module
Enhances the **AI Module** by adding support for connecting to any MCP server, whether hosted locally or remotely. For more details, see the [MCP Module README](src/Modules/CrestApps.OrchardCore.AI.Mcp/README.md).

#### Azure OpenAI Module
Adds support for **Azure OpenAI** services within the **OpenAI Module**. See the [Azure OpenAI Module README](src/Modules/CrestApps.OrchardCore.OpenAI.Azure/README.md).  

#### OpenAI Module
Extends the **AI Module** by integrating OpenAI-powered services. You can connect to any provider that adheres to OpenAI standard. Here are few providers:

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
Extends the **AI Module** by integrating Azure AI Inference services. See the [Azure AI Inference Module README](src/Modules/CrestApps.OrchardCore.AzureAIInference/README.md).  

#### Ollama Module
Extends the **AI Module** by integrating any Ollama model. See the [Ollama Module README](src/Modules/CrestApps.OrchardCore.Ollama/README.md).  

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

### Production Packages
Stable releases are available on [NuGet.org](https://www.nuget.org/).  

## Contributing

We welcome contributions from the community! To contribute:  

1. **Fork the repository.**  
2. **Create a new branch** for your feature or bug fix.  
3. **Make your changes** and commit them with clear messages.  
4. **Push your changes** to your fork.  
5. **Submit a pull request** to the main repository.  

## License

CrestApps is licensed under the **MIT License**. See the [LICENSE](https://github.com/git/git-scm.com/blob/main/MIT-LICENSE.txt) file for more details.  
