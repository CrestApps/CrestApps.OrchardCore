# CrestApps.OrchardCore.Cms.Core.Targets

This is a meta-package that bundles all CrestApps modules for easy installation in OrchardCore CMS applications.

## Usage

### For NuGet Package Consumers

Add a reference to `CrestApps.OrchardCore.Cms.Core.Targets` in your web project:

```xml
<PackageReference Include="CrestApps.OrchardCore.Cms.Core.Targets" Version="2.0.0-preview" />
```

This will automatically bring in all CrestApps modules as NuGet dependencies, ensuring they are included during `dotnet publish` and Docker builds.

### For Source Code Development

When working with the source code, reference this project directly:

```xml
<ProjectReference Include="path/to/CrestApps.OrchardCore.Cms.Core.Targets/CrestApps.OrchardCore.Cms.Core.Targets.csproj" />
```

## How It Works

This project uses `ProjectReference` items with `PrivateAssets="none"` to reference all CrestApps modules. During `dotnet pack`:

1. Each `ProjectReference` is converted to a `PackageReference` dependency in the generated `.nupkg`
2. The version for each dependency is resolved from the project's `VersionPrefix` and `VersionSuffix` properties
3. The `PrivateAssets="none"` attribute ensures dependencies are visible to package consumers

This pattern follows the same approach used by OrchardCore's own `OrchardCore.Application.Cms.Core.Targets` package.

## Verifying Package Contents

To verify the generated package has correct dependencies:

```bash
# Pack the project
dotnet pack -c Release

# Extract and view the .nuspec file
unzip -p bin/Release/CrestApps.OrchardCore.Cms.Core.Targets.*.nupkg *.nuspec
```

The `.nuspec` should contain `<dependency>` entries for all CrestApps modules with proper version numbers.

## Troubleshooting

### DLLs Not Included in Publish Output

If modules are not being included during `dotnet publish`:

1. **Verify package source**: Ensure all CrestApps packages are available from your configured NuGet sources
2. **Check versions**: All CrestApps packages must be published with matching versions
3. **Clear NuGet cache**: Run `dotnet nuget locals all --clear` to clear cached packages
4. **Verify dependencies**: Inspect the `.nupkg` file to confirm dependencies are present

### Modules Not Loaded at Runtime (DLLs Present but Not Working)

If DLLs are present but OrchardCore doesn't discover the modules:

1. **Check `.deps.json`**: Verify modules are listed in your app's `.deps.json` file:
   ```bash
   cat YourApp.deps.json | grep -i "CrestApps.OrchardCore"
   ```

2. **Verify module registration**: Each module must have a `Manifest.cs` with the `[assembly: Module(...)]` attribute. This is how OrchardCore discovers modules.

3. **Enable features in OrchardCore**: CrestApps modules need to be enabled in OrchardCore's admin panel under Features, or via a recipe during setup.

4. **Check feature dependencies**: Some features have `EnabledByDependencyOnly = true` meaning they're only enabled when another feature depends on them.

5. **Review startup logs**: OrchardCore logs module discovery. Check application logs for messages about module loading.

6. **Docker-specific issues**:
   - Ensure the working directory is set correctly in your Dockerfile
   - Verify environment variables like `ASPNETCORE_URLS` are properly configured
   - Check if there are missing native dependencies that modules might need

### Debugging Module Discovery

To debug why modules aren't being loaded:

```csharp
// In Program.cs, add logging to see module discovery
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Debug);
```

OrchardCore will log which modules and features it discovers and enables.

## Included Modules

This meta-package includes:

- CrestApps.OrchardCore.AI
- CrestApps.OrchardCore.AI.Agent
- CrestApps.OrchardCore.AI.Chat
- CrestApps.OrchardCore.AI.Chat.Interactions
- CrestApps.OrchardCore.AI.Mcp
- CrestApps.OrchardCore.AzureAIInference
- CrestApps.OrchardCore.ContentAccessControl
- CrestApps.OrchardCore.Ollama
- CrestApps.OrchardCore.Omnichannel
- CrestApps.OrchardCore.OpenAI
- CrestApps.OrchardCore.OpenAI.Azure
- CrestApps.OrchardCore.Recipes
- CrestApps.OrchardCore.Resources
- CrestApps.OrchardCore.Roles
- CrestApps.OrchardCore.SignalR
- CrestApps.OrchardCore.Users
- And more...
