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

### Users Module
Enhances user management with customizable display names and avatars. See the [Users Module README](src/Modules/CrestApps.OrchardCore.Users/README.md) for details.  

### AI Module
Provides services for all AI modules and provide the interface for managing AI profiles and AI Deployments. See the [AI Module README](src/Modules/CrestApps.OrchardCore.AI/README.md) for more details.  

### AI Chat Module
Provides interface for interacting with AI models like **ChatGPT**. See the [AI Module README](src/Modules/CrestApps.OrchardCore.AI.Chat/README.md) for more details.  

### Azure AI Inference Module
Extends the **AI Module** by integrating Azure AI Inference services. See the [OpenAI Module README](src/Modules/CrestApps.OrchardCore.AzureAIInference/README.md).  

### Azure OpenAI Module
Adds support for **Azure OpenAI** services within the **OpenAI Module**. See the [Azure OpenAI Module README](src/Modules/CrestApps.OrchardCore.OpenAI.Azure/README.md).  

### DeepSeek Module
Extends the **AI Module** by integrating DeepSeek services. See the [OpenAI Module README](src/Modules/CrestApps.OrchardCore.DeepSeek/README.md).  

### OpenAI Module
Extends the **AI Module** by integrating OpenAI-powered services. See the [OpenAI Module README](src/Modules/CrestApps.OrchardCore.OpenAI/README.md).  

### Ollama Module
Extends the **AI Module** by integrating any Ollama model. See the [OpenAI Module README](src/Modules/CrestApps.OrchardCore.Ollama/README.md).  

### SignalR Module
The **SignalR** module enables seamless integration of SignalR within Orchard Core. See the [Azure OpenAI Module README](src/Modules/CrestApps.OrchardCore.SignalR/README.md).  

### Resources Module
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
