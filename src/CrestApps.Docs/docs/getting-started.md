---
sidebar_label: Getting Started
sidebar_position: 2
title: Getting Started
description: How to set up and run CrestApps Orchard Core modules
---

# Getting Started

Follow these steps to get started with CrestApps Orchard Core modules.

## Prerequisites

- [.NET SDK](https://dotnet.microsoft.com/download) (version matching your Orchard Core target)
- An existing Orchard Core application, or use the CrestApps starter project

## Installation

### Install All Modules

Add the `CrestApps.OrchardCore.Cms.Core.Targets` package to include all modules at once:

```bash
dotnet add package CrestApps.OrchardCore.Cms.Core.Targets
```

### Install Individual Modules

Or install only the modules you need:

```bash
dotnet add package CrestApps.OrchardCore.AI
dotnet add package CrestApps.OrchardCore.AI.Chat
# ... add other modules as needed
```

## Running Locally

1. **Clone the Repository:**
   ```bash
   git clone https://github.com/CrestApps/CrestApps.OrchardCore.git
   ```

2. **Navigate to the Project Directory:**
   ```bash
   cd CrestApps.OrchardCore
   ```

3. **Build the Solution:**
   ```bash
   dotnet build
   ```

4. **Launch the Application:**
   ```bash
   dotnet run
   ```

5. **Enable Modules:**
   Access the **Orchard Core Admin Dashboard** to enable desired CrestApps modules.

## Package Feeds

### Production Packages

Stable releases are available on [NuGet.org](https://www.nuget.org/).

### Preview Packages

[![Hosted By: Cloudsmith](https://img.shields.io/badge/OSS%20hosting%20by-cloudsmith-blue?logo=cloudsmith&style=for-the-badge)](https://cloudsmith.com)

For the latest updates and preview packages, visit the [Cloudsmith CrestApps OrchardCore repository](https://cloudsmith.io/~crestapps/repos/crestapps-orchardcore).

### Adding the Preview Feed

#### In Visual Studio

1. Open **NuGet Package Manager Settings** (under *Tools*).
2. Add a new package source:
   - **Name:** `CrestAppsPreview`
   - **URL:** `https://nuget.cloudsmith.io/crestapps/crestapps-orchardcore/v3/index.json`

#### Via NuGet.config

Update your **NuGet.config** file:

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

CrestApps is licensed under the **MIT License**. See the [LICENSE](https://opensource.org/licenses/MIT) file for more details.
