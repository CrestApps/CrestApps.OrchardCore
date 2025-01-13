# CrestApps - Orchard Core Modules

CrestApps offers a collection of open-source modules designed to extend and enrich the functionality of Orchard Core, a versatile application framework built on ASP.NET Core.

## Overview

Orchard Core provides a robust foundation for developing dynamic, data-driven websites and applications. CrestApps modules are crafted to enhance this framework with a focus on usability, security, and performance.

## Key Features

- **Modularity**: Each module operates independently, allowing selective integration based on project requirements.
- **Security**: Designed following best practices to fortify application security.
- **Performance**: Optimized for speed and efficiency to enhance Orchard Core application performance.

## Project Structure

The CrestApps repository is organized for clarity and ease of use. Modules can be found in the `src/Modules` folder, with each module structured for independent usage and configuration. 

- **Modules Folder**:  
  Contains all CrestApps modules. Each module includes a `README.md` file that explains how to configure and integrate it into your project.

Example structure:
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

To get started with any module, refer to its dedicated README file for detailed instructions.

## Available Modules

### Users Module

Enhances user management with customizable display names and avatars. For detailed information, refer to the [Users Module README](src/Modules/CrestApps.OrchardCore.Users/README.md).

### OpenAI Module

This feature enabled you to use UI to interact with OpenAI modules like ChatGTP modules. For detailed information, refer to the [Users Module README](src/Modules/CrestApps.OrchardCore.OpenAI/README.md).

### Azure OpenAI Module

This feature enabled you to use UI to interact with Azure OpenAI modules. For detailed information, refer to the [Users Module README](src/Modules/CrestApps.OrchardCore.OpenAI.Azure/README.md).

### Resurces Module

This feature provides you with additial resources that you can utilize to speed up development. For detailed information, refer to the [Users Module README](src/Modules/CrestApps.OrchardCore.Resources/README.md).

## Getting Started

### Running Locally

To begin with CrestApps:

1. **Clone the Repository**:
    ```sh
    git clone https://github.com/CrestApps/CrestApps.OrchardCore.git
    ```

2. **Navigate to the Project Directory**:
    ```sh
    cd CrestApps.OrchardCore
    ```

3. **Build the Solution**:
    Ensure you have the necessary .NET SDK installed, then run:
    ```sh
    dotnet build
    ```

4. **Launch the Application**:
    ```sh
    dotnet run
    ```

5. **Enable Modules**:
    Access the Orchard Core admin dashboard to enable desired CrestApps modules.

### Package Manager

#### Production Packages

Stable releases are available on [NuGet.org](https://www.nuget.org/). For the latest updates and previews:

#### Preview Package Feed

[![Hosted By: Cloudsmith](https://img.shields.io/badge/OSS%20hosting%20by-cloudsmith-blue?logo=cloudsmith&style=for-the-badge)](https://cloudsmith.com)

Explore our preview packages and updates on the [Cloudsmith CrestApps OrchardCore repository](https://cloudsmith.io/~crestapps/repos/crestapps-orchardcore). For usage guidelines, visit the [preview package feed documentation](https://docs.orchardcore.net/en/latest/getting-started/preview-package-source/).

##### Adding the Preview Feed to Visual Studio

Navigate to NuGet Package Manager Settings in Visual Studio under Tools. Add a new source with the name `CrestAppsPreview` and URL `https://nuget.cloudsmith.io/crestapps/crestapps-orchardcore/v3/index.json`.

##### Adding the Preview Feed via NuGet.config

Alternatively, update your NuGet.config file:

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

We welcome community contributions! To contribute to CrestApps:

1. Fork the repository.
2. Create a new branch for your feature or bug fix.
3. Make your changes and commit them with clear messages.
4. Push your changes to your fork.
5. Submit a pull request to the main repository.

## License

CrestApps is licensed under the MIT License. See the [LICENSE](https://github.com/git/git-scm.com/blob/main/MIT-LICENSE.txt) file for more details.
