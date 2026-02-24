# CrestApps.OrchardCore Development Instructions

**ALWAYS reference these instructions first and fallback to search or bash commands only when you encounter unexpected information that does not match the info here.**

## Project Overview

CrestApps.OrchardCore is a collection of open-source modules for **Orchard Core CMS**, a modular application framework built on **ASP.NET Core/.NET 10**. The repository contains AI modules, omnichannel communication, user management enhancements, content access control, and other CMS extensions.

**License**: MIT  
**Target Framework**: .NET 10.0 (net10.0)  
**Architecture**: Modular, multi-tenant application framework

## Working Effectively

### Prerequisites and Environment Setup

Install .NET 10.0 SDK first:
```bash
# Add Microsoft package repository (Ubuntu/Debian)
wget https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb
rm packages-microsoft-prod.deb

# Install .NET 10.0 SDK - TAKES 1-2 MINUTES
sudo apt-get update
sudo apt-get install -y dotnet-sdk-10.0

# Verify installation
dotnet --version  # Should show 10.0.x
```

### Build Process

**CRITICAL BUILD LIMITATION**: The .NET build requires access to Orchard Core 3.0 preview packages from `https://nuget.cloudsmith.io/orchardcore/preview/v3/index.json`. In environments with restricted network access (like CI runners or sandboxed environments), the build will fail with network connectivity errors.

#### Asset Build (Always Works)
```bash
# Install npm dependencies - TAKES 2-3 MINUTES, NEVER CANCEL
npm install

# Build frontend assets - TAKES 4 SECONDS
npm run rebuild
# OR
gulp rebuild
```

#### .NET Solution Build (Network Dependent)
```bash
# Full solution build - ONLY WORKS WITH NETWORK ACCESS TO CLOUDSMITH
# TAKES 5-10 MINUTES when successful, NEVER CANCEL
dotnet build -c Release -warnaserror /p:TreatWarningsAsErrors=true /p:RunAnalyzers=true /p:NuGetAudit=false
```

**If build fails with NU1301 errors** about `nuget.cloudsmith.io`, this is expected in restricted environments. Document this limitation rather than attempting workarounds.

#### Unit Tests (Requires Successful Build)
```bash
# Run unit tests - TAKES 2-5 MINUTES, NEVER CANCEL
dotnet test -c Release --no-build ./tests/CrestApps.OrchardCore.Tests/CrestApps.OrchardCore.Tests.csproj
```

### Running the Application

**Web Application**: The main application is in `src/Startup/CrestApps.OrchardCore.Cms.Web/`

```bash
# Run the CMS web application (requires successful build)
cd src/Startup/CrestApps.OrchardCore.Cms.Web
dotnet run

# Application runs on: http://localhost:5000
# Admin setup occurs on first run
```

**Aspire Orchestration**: For full-stack local development with Ollama, Elasticsearch, and Redis, use the Aspire AppHost:

```bash
cd src/Startup/CrestApps.Aspire.AppHost
dotnet run
```

### Validation Scenarios

**When the build succeeds**, always validate changes by:

1. **Build Validation**: Run both asset and .NET builds
2. **Test Validation**: Execute the full test suite  
3. **Web Application Testing**: 
   - Start the web application
   - Complete the Orchard Core setup wizard
   - Enable relevant CrestApps modules in the admin dashboard
   - Test module-specific functionality (AI chat, user management, etc.)
4. **Asset Validation**: Run `npm run rebuild` and verify no asset changes are uncommitted

**When build fails due to network issues**:
- Focus on asset-only changes using `npm run rebuild`
- Document any .NET code changes for testing in environments with network access
- Use static analysis and code review for .NET validation

## Repository Structure and Navigation

### Key Directories
```
src/
├── Abstractions/               # Shared interface/abstraction libraries
│   ├── CrestApps.OrchardCore.Abstractions/         # Core abstractions
│   ├── CrestApps.OrchardCore.AI.Abstractions/      # AI abstractions
│   └── CrestApps.OrchardCore.Users.Abstractions/   # User abstractions
├── Common/                     # Shared utility libraries
│   └── CrestApps.Support/                          # General support utilities
├── Core/                       # Core service libraries (not Orchard modules)
│   ├── CrestApps.Azure.Core/                       # Azure utilities
│   ├── CrestApps.OrchardCore.AI.Chat.Interactions.Core/  # Chat interaction core services
│   ├── CrestApps.OrchardCore.AI.Core/              # AI core services
│   ├── CrestApps.OrchardCore.AI.Mcp.Core/          # MCP core services
│   ├── CrestApps.OrchardCore.Core/                 # General OrchardCore core
│   ├── CrestApps.OrchardCore.Omnichannel.Core/     # Omnichannel core services
│   ├── CrestApps.OrchardCore.OpenAI.Azure.Core/    # Azure OpenAI core
│   ├── CrestApps.OrchardCore.OpenAI.Core/          # OpenAI core services
│   ├── CrestApps.OrchardCore.Recipes.Core/         # Recipes core services
│   ├── CrestApps.OrchardCore.Roles.Core/           # Roles core services
│   ├── CrestApps.OrchardCore.SignalR.Core/         # SignalR core services
│   ├── CrestApps.OrchardCore.Users.Core/           # Users core services
│   └── CrestApps.OrchardCore.YesSql.Core/          # YesSql core utilities
├── Modules/                    # All CrestApps Orchard Core modules
│   ├── CrestApps.OrchardCore.AI/                   # AI base services and deployments
│   ├── CrestApps.OrchardCore.AI.Agent/             # AI agents
│   ├── CrestApps.OrchardCore.AI.Chat/              # AI chat interface and profiles
│   ├── CrestApps.OrchardCore.AI.Chat.Copilot/      # GitHub Copilot-style AI chat
│   ├── CrestApps.OrchardCore.AI.Chat.Interactions/ # Real-time AI chat interactions (SignalR hub)
│   ├── CrestApps.OrchardCore.AI.Chat.Interactions.Documents/          # Document-based chat interactions
│   ├── CrestApps.OrchardCore.AI.Chat.Interactions.Documents.AzureAI/  # Azure AI document interactions
│   ├── CrestApps.OrchardCore.AI.Chat.Interactions.Documents.Elasticsearch/ # Elasticsearch document interactions
│   ├── CrestApps.OrchardCore.AI.Chat.Interactions.OpenXml/            # OpenXml document chat interactions
│   ├── CrestApps.OrchardCore.AI.Chat.Interactions.Pdf/                # PDF document chat interactions
│   ├── CrestApps.OrchardCore.AI.DataSources/                          # AI data source management
│   ├── CrestApps.OrchardCore.AI.DataSources.AzureAI/                  # Azure AI Search data sources
│   ├── CrestApps.OrchardCore.AI.DataSources.Elasticsearch/            # Elasticsearch data sources
│   ├── CrestApps.OrchardCore.AI.Documents/                            # AI document indexing and management
│   ├── CrestApps.OrchardCore.AI.Documents.AzureAI/                    # Azure AI document storage
│   ├── CrestApps.OrchardCore.AI.Documents.Elasticsearch/              # Elasticsearch document storage
│   ├── CrestApps.OrchardCore.AI.Documents.OpenXml/                    # OpenXml document parsing
│   ├── CrestApps.OrchardCore.AI.Documents.Pdf/                        # PDF document parsing
│   ├── CrestApps.OrchardCore.AI.Mcp/                                  # Model Context Protocol server
│   ├── CrestApps.OrchardCore.AI.Mcp.Resources.Ftp/                    # MCP FTP resource handler
│   ├── CrestApps.OrchardCore.AI.Mcp.Resources.Sftp/                   # MCP SFTP resource handler
│   ├── CrestApps.OrchardCore.AzureAIInference/     # Azure AI Inference / GitHub Models provider
│   ├── CrestApps.OrchardCore.ContentAccessControl/ # Content item access control
│   ├── CrestApps.OrchardCore.Ollama/               # Ollama (local LLM) provider
│   ├── CrestApps.OrchardCore.Omnichannel/          # Omnichannel communication base
│   ├── CrestApps.OrchardCore.Omnichannel.EventGrid/ # Event Grid omnichannel integration
│   ├── CrestApps.OrchardCore.Omnichannel.Managements/ # Omnichannel management UI
│   ├── CrestApps.OrchardCore.Omnichannel.Sms/      # SMS omnichannel channel
│   ├── CrestApps.OrchardCore.OpenAI/               # OpenAI provider
│   ├── CrestApps.OrchardCore.OpenAI.Azure/         # Azure OpenAI provider
│   ├── CrestApps.OrchardCore.Recipes/              # Recipe enhancements
│   ├── CrestApps.OrchardCore.Resources/            # Shared frontend resources
│   ├── CrestApps.OrchardCore.Roles/                # Enhanced roles management
│   ├── CrestApps.OrchardCore.SignalR/              # SignalR integration
│   └── CrestApps.OrchardCore.Users/               # Enhanced user management
├── CrestApps.OrchardCore.Documentations/  # Docusaurus documentation site
├── Startup/                    # Runnable applications
│   ├── CrestApps.Aspire.AppHost/                   # .NET Aspire orchestration host
│   ├── CrestApps.OrchardCore.Cms.Web/              # Main CMS web application
│   └── CrestApps.OrchardCore.Samples.McpClient/    # MCP client sample application
└── Targets/                    # MSBuild package bundle targets
    └── CrestApps.OrchardCore.Cms.Core.Targets/

tests/
└── CrestApps.OrchardCore.Tests/    # Unit test project

.github/workflows/              # CI/CD pipelines
```

### Important Files
- `CrestApps.OrchardCore.slnx` - Main solution file (Visual Studio XML format)
- `global.json` - .NET SDK version (10.0.100)
- `Directory.Build.props` - Common MSBuild properties (TFM, versioning, analysis rules)
- `Directory.Packages.props` - Centralized NuGet package versions
- `NuGet.config` - Package source configuration (includes CloudSmith feed for Orchard Core previews)
- `package.json` - npm dependencies and scripts
- `gulpfile.js` - Asset build configuration
- `.editorconfig` - Code style and formatting rules

## Common Development Tasks

### Adding a New Module
1. Create new folder in `src/Modules/CrestApps.OrchardCore.{ModuleName}/`
2. Add module project file following existing patterns
3. Include `Manifest.cs` with module definition
4. Add module reference to appropriate target package
5. **Update Targets**: Add a reference to the new module in the targets project `src/Targets/CrestApps.OrchardCore.Cms.Core.Targets/CrestApps.OrchardCore.Cms.Core.Targets.targets` so it is discoverable by Orchard Core.

### Working with AI Modules
- **Base AI Module**: `CrestApps.OrchardCore.AI` - start here for AI-related changes; manages deployments and provider connections
- **Chat Interface**: `CrestApps.OrchardCore.AI.Chat` - AI chat profiles and UI
- **Real-time Chat**: `CrestApps.OrchardCore.AI.Chat.Interactions` - SignalR-based interactive chat hub
- **Copilot Chat**: `CrestApps.OrchardCore.AI.Chat.Copilot` - GitHub Copilot-style embedded chat experience
- **Document Indexing**: `CrestApps.OrchardCore.AI.Documents` - indexes documents for AI retrieval
- **Data Sources**: `CrestApps.OrchardCore.AI.DataSources` - configures external AI data sources
- **MCP Server**: `CrestApps.OrchardCore.AI.Mcp` - exposes Orchard Core content as MCP resources
- **AI Agents**: `CrestApps.OrchardCore.AI.Agent` - defines reusable AI agents/tools
- **Provider modules**: `CrestApps.OrchardCore.OpenAI`, `CrestApps.OrchardCore.OpenAI.Azure`, `CrestApps.OrchardCore.Ollama`, `CrestApps.OrchardCore.AzureAIInference`

### Working with Omnichannel Modules
- **Base Module**: `CrestApps.OrchardCore.Omnichannel` - unified communication layer
- **SMS Channel**: `CrestApps.OrchardCore.Omnichannel.Sms` - SMS messaging support
- **Event Grid**: `CrestApps.OrchardCore.Omnichannel.EventGrid` - Azure Event Grid integration
- **Management UI**: `CrestApps.OrchardCore.Omnichannel.Managements` - admin management interface

### Frontend Development
- CSS/SCSS files are in individual module `Assets/` directories
- TypeScript/JavaScript files are built using the gulp pipeline
- Run `npm run rebuild` after any frontend changes
- Use `npm run watch` for development with auto-rebuild

### Testing Workflow
1. Make code changes
2. Run `npm run rebuild` for frontend changes
3. Run `dotnet build` for backend changes (if network allows)
4. Run `dotnet test` for unit tests (if build succeeds)
5. Start web application and test manually

### Documentation Workflow

Whenever code is modified, you MUST update the documentation project located at `src/CrestApps.OrchardCore.Documentations`:

1. **Update feature documentation first** – find the relevant page under `src/CrestApps.OrchardCore.Documentations/docs/` and keep it accurate with the latest behavior.
2. **Add a changelog entry** – add an entry to the changelog in the same documentation project describing what changed, why it changed, and any breaking or behavioral impact.
3. **Documentation changes are NOT optional** – code changes without documentation updates are considered incomplete.
4. **Validate the docs build** – after updating documentation, verify the Docusaurus site builds successfully and all internal links resolve correctly. The CI pipeline runs link-checking; failing to validate locally will cause workflow failures.

## Troubleshooting

### Build Issues
- **NU1301 errors**: Network connectivity to CloudSmith required, expected in restricted environments
- **SDK version errors**: Ensure .NET 10.0.100+ is installed via `dotnet --version`
- **npm install failures**: Node.js 15+ required (check with `node --version`)

### Runtime Issues  
- **Database**: Uses SQLite by default, no external DB required for development
- **Modules not appearing**: Enable modules in Orchard Core admin dashboard
- **Permission errors**: Check content access control module configuration

### Network Dependencies
This project requires network access to:
- `https://api.nuget.org/v3/index.json` (public NuGet)
- `https://nuget.cloudsmith.io/orchardcore/preview/v3/index.json` (Orchard Core previews)

If CloudSmith is inaccessible, only asset builds and code analysis are possible.

## Coding Standards and Conventions

### C# Code Style (enforced via .editorconfig)

#### Naming Conventions
- **Interfaces**: Prefix with `I` (e.g., `IAICompletionService`, `IUserCacheService`)
- **Services**: Suffix with `Service` for service implementations (e.g., `DefaultAIToolsService`, `DefaultUserCacheService`)
- **Drivers**: Suffix with `Driver` for display drivers (e.g., `AIProfileDisplayDriver`, `AIToolInstanceDisplayDriver`)
- **Handlers**: Suffix with `Handler` for handlers (e.g., `AIProviderConnectionHandler`, `FunctionInvocationAICompletionServiceHandler`)
- **Providers**: Suffix with `Provider` for providers (e.g., `AIConnectionsAdminMenu`, `AIPermissionsProvider`)
- **Tests**: Suffix test classes with `Tests` (e.g., `OrchardCoreHelpersTests`)

#### Code Formatting
- **Indentation**: 4 spaces for C#, 2 spaces for JSON/YAML/XML
- **Line endings**: CRLF
- **Charset**: UTF-8
- **Braces**: Always use braces for code blocks, opening brace on new line (Allman style)
- **var usage**: Prefer `var` everywhere (built-in types, apparent types, and elsewhere)
- **this.**: Avoid using `this.` qualifier unless necessary
- **Language keywords**: Use language keywords (e.g., `int`, `string`) over framework types (e.g., `Int32`, `String`)
- **Namespaces**: Use file-scoped namespace declarations (C# 10+)
- **Using statements**: 
  - Sort System directives first
  - Prefer simple using statements over braces when possible
  
#### Code Preferences  
- **Range/Index operators**: Avoid using range/index operators (enforced as warning)
- **Code Analysis**: `AnalysisLevel` is set to `latest-Recommended`
- **Implicit usings**: Enabled globally
- **Date/time**: Never use `DateTime.UtcNow`. Always inject `IClock` in the constructor (e.g., `IClock clock`) and store it as `private readonly IClock _clock = clock;`, then call `_clock.UtcNow` in methods
- **One type per file**: Every public type must live in its own file. The file name must always match the type name (e.g., `MyService.cs` for `class MyService`)
- **sealed classes**: Seal all classes by default (`sealed class`), **except** ViewModel classes that are consumed by any Orchard Core display driver — those must remain unsealed because the framework creates runtime proxies for them and proxies cannot be created from sealed types

### Module Structure Conventions

Every Orchard Core module in this repository follows a standard structure:

```
CrestApps.OrchardCore.{ModuleName}/
├── Assets/              # Frontend assets (JS, CSS, SCSS)
├── Controllers/         # MVC Controllers
├── Drivers/            # Display drivers
├── Handlers/           # Event handlers
├── Indexes/            # YesSql indexes
├── Migrations/         # Data migrations
├── Models/             # Domain models
├── Recipes/            # Recipe steps
├── Services/           # Business logic services
├── ViewModels/         # View models
├── Views/              # Razor views
├── Workflows/          # Workflow activities (if applicable)
├── Manifest.cs         # Module manifest (required)
├── Startup.cs          # Service registration (required)
├── README.md           # Module documentation
├── package.json        # npm dependencies (if has assets)
└── wwwroot/            # Compiled static files
```

### Startup Class Patterns

Modules use `StartupBase` classes for service registration:
- Main `Startup` class for core services
- Feature-specific startup classes with `[Feature("FeatureName")]` attribute
- `[RequireFeatures()]` attribute for conditional features
- Separate startup classes for Recipes, Deployment, Workflows integrations

Example pattern:
```csharp
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        // Register services
    }
}

[Feature(Constants.Feature.FeatureName)]
public sealed class FeatureStartup : StartupBase
{
    // Feature-specific registration
}
```

### Namespace Conventions

- Namespace matches folder structure
- Pattern: `CrestApps.OrchardCore.{ModuleName}.{FolderName}`
- Examples:
  - `CrestApps.OrchardCore.AI.Services`
  - `CrestApps.OrchardCore.AI.Recipes`
  - `CrestApps.OrchardCore.Users.Core`

## Testing Practices

### Test Structure
- Tests are located in `tests/CrestApps.OrchardCore.Tests/`
- Use xUnit for all tests
- Test class naming: `{ClassUnderTest}Tests`
- Use `[Theory]` and `[InlineData]` for parameterized tests
- Use `sealed` modifier for test classes

### Test Naming
- Test method pattern: `{MethodName}_{Scenario}_{ExpectedBehavior}`
- Example: `IsVersionGreaterOrEqual_WhenVersionIsGreater_ShouldReturnTrue`
- Be descriptive and explicit about what is being tested

### Test Organization
- Group related tests in nested folders matching source structure
- Mock interfaces with test implementations (e.g., `TestCatalogEntryHandler<T>`)
- Use Func<> delegates for flexible test behavior

### Test Coverage
- Add tests for new features and bug fixes
- Focus on business logic and service implementations
- Test edge cases and error conditions

## Documentation Standards

### Module README Files
Every module MUST have a README.md file with:
- Module purpose and features
- Installation instructions
- Configuration details
- Usage examples
- Dependencies on other modules

### Documentation Project
The Docusaurus documentation site is located at `src/CrestApps.OrchardCore.Documentations`. It contains:
- Feature documentation under `docs/`
- Module-specific guides under `docs/modules/`, `docs/ai/`, `docs/omnichannel/`, `docs/providers/`
- A changelog under `docs/changelog/`
- Getting started guide and samples

### Code Documentation
- XML documentation comments for public APIs
- Inline comments for complex logic only
- Keep comments up-to-date with code changes
- Avoid obvious comments that duplicate code

## CI/CD Integration

Before committing:
1. Run `npm run rebuild` - asset build must complete cleanly
2. Run `dotnet build -warnaserror` (if network allows) - the build must produce **zero warnings**; fix every warning across the entire project, not only in files you changed, before running with `-warnaserror`
3. Run `dotnet test` (if build succeeds) - all tests must pass
4. Verify no uncommitted asset changes with `git status`

The CI pipeline validates builds on both Ubuntu and Windows, so test locally on similar environments when possible.

---

**Remember: Always build and validate your changes thoroughly. The modular architecture means changes can affect multiple modules, so comprehensive testing is essential.**

## Frontend Development Guidelines

### Asset Management
- Frontend assets are managed using **Gulp** build system
- Assets are defined in `Assets.json` files within each module
- Built assets are output to `wwwroot/` directory

### Build Commands
```bash
# Install dependencies (run once or when package.json changes)
npm install

# Build assets incrementally (only changed files)
npm run build

# Full rebuild (all assets)
npm run rebuild

# Watch mode for development (auto-rebuild on changes)
npm run watch
```

### Supported Asset Types
- **JavaScript**: Transpiled with Babel, minified with Terser
- **TypeScript**: Compiled to JavaScript
- **SCSS/Sass**: Compiled to CSS with Dart Sass
- **LESS**: Compiled to CSS
- **CSS**: PostCSS with RTL support

### Asset Build Process
1. Source files are in module `Assets/` directory
2. Gulp processes files based on `Assets.json` configuration
3. Compiled output goes to module `wwwroot/` directory
4. Source maps are generated for debugging
5. Minification is applied to production builds

### Frontend Best Practices
- Always run `npm run rebuild` after modifying frontend code
- Commit compiled assets (wwwroot) along with source files
- Use `npm run watch` during active development
- Check for TypeScript/JavaScript errors before committing
- Follow existing patterns for module-specific assets

## Pull Request Guidelines

### Before Submitting
1. **Build Validation**: Ensure both .NET and asset builds succeed
2. **Test Coverage**: Add tests for new features and bug fixes
3. **Code Quality**: Follow coding standards and conventions
4. **Documentation**: Update README files, code comments, and the Docusaurus docs in `src/CrestApps.OrchardCore.Documentations`
5. **Commit Messages**: Write clear, descriptive commit messages
6. **Branch Naming**: Use descriptive branch names (e.g., `feature/ai-chat-improvements`, `fix/user-avatar-bug`)

### PR Description Template
- Link to related issue using `Fix #issue_number` or `Closes #issue_number`
- Describe what changed and why
- Include screenshots for UI changes
- List any breaking changes
- Note any migration or deployment considerations

### Review Process
- Address feedback promptly
- Don't manually resolve conversations - let reviewers do that
- Use "Re-request review" when changes are ready
- Keep discussions within the PR thread
- Allow maintainers to edit your PR branch

## Security Considerations

### Secure Coding Practices
- Validate all user inputs
- Use parameterized queries (YesSql handles this)
- Implement proper authentication and authorization checks
- Never commit secrets or sensitive data
- Follow OWASP guidelines for web security

### Permission Checks
- Always check permissions before accessing restricted resources
- Use `IAuthorizationService` for authorization
- Define permissions in `PermissionProvider` classes
- Test permission boundaries

### Sensitive Data
- Never log sensitive information
- Use secure storage for secrets (e.g., Azure Key Vault, environment variables)
- Encrypt sensitive data at rest and in transit

## Common Patterns

### Service Registration
```csharp
services.AddScoped<IMyService, MyService>();        // Scoped per request
services.AddTransient<IMyService, MyService>();     // New instance each time
services.AddSingleton<IMyService, MyService>();     // Single instance
```

### Display Drivers
```csharp
services.AddDisplayDriver<TModel, TDriver>();
```

### Handlers
```csharp
services.AddScoped<IEventHandler, MyEventHandler>();
```

### Migrations
```csharp
services.AddDataMigration<MyMigrations>();
```

### Navigation
```csharp
services.AddNavigationProvider<MyAdminMenu>();
```

## Anti-Patterns to Avoid

- ❌ Don't use static mutable state
- ❌ Don't create tight coupling between modules
- ❌ Don't bypass Orchard Core's dependency injection
- ❌ Don't hardcode connection strings or secrets
- ❌ Don't use synchronous I/O operations (use async/await)
- ❌ Don't ignore compiler warnings (TreatWarningsAsErrors is enabled) — fix all warnings in the entire project, not just changed files
- ❌ Don't skip writing tests for new features
- ❌ Don't commit commented-out code
- ❌ Don't use `System.Range` or `System.Index` operators (enforced as warning)
- ❌ Don't leave unused services injected through dependency injection
- ❌ Don't leave unused `using` statements in source files
- ❌ Don't use `DateTime.UtcNow` — inject `IClock` and use `_clock.UtcNow` instead
- ❌ Don't seal ViewModel classes that are used by any Orchard Core display driver — the framework requires unsealed types to generate runtime proxies
- ❌ Don't put multiple public types in a single file — each public type must be in its own file whose name matches the type name

## Code Cleanup (Required After Completing Work)

After completing any code change, always clean up:

- Remove any **unused services** that were injected through dependency injection but are no longer referenced in the class body.
- Remove any **unused `using` statements** from all modified source files.
- Prefer clarity over cleverness; do not introduce undocumented behavior.

## Useful Resources

- [Orchard Core Documentation](https://docs.orchardcore.net/)
- [Orchard Core GitHub](https://github.com/OrchardCMS/OrchardCore)
- [Project Repository](https://github.com/CrestApps/CrestApps.OrchardCore)
- [Contributing Guidelines](.github/CONTRIBUTING.md)
- [MIT License](https://opensource.org/licenses/MIT)