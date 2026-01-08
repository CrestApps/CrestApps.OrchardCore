# CrestApps.OrchardCore Development Instructions

**ALWAYS reference these instructions first and fallback to search or bash commands only when you encounter unexpected information that does not match the info here.**

## Project Overview

CrestApps.OrchardCore is a collection of open-source modules for **Orchard Core CMS**, a modular application framework built on **ASP.NET Core/.NET 10**. The repository contains AI modules, user management enhancements, content access control, and other CMS extensions.

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
├── Modules/                    # All CrestApps modules
│   ├── CrestApps.OrchardCore.AI/           # AI core services
│   ├── CrestApps.OrchardCore.AI.Chat/      # AI chat interface  
│   ├── CrestApps.OrchardCore.AI.Agent/     # AI agents
│   ├── CrestApps.OrchardCore.AI.Mcp/       # Model Context Protocol
│   ├── CrestApps.OrchardCore.OpenAI/       # OpenAI integration
│   ├── CrestApps.OrchardCore.OpenAI.Azure/ # Azure OpenAI
│   ├── CrestApps.OrchardCore.Ollama/       # Ollama integration
│   ├── CrestApps.OrchardCore.Users/        # Enhanced user management
│   ├── CrestApps.OrchardCore.Roles/        # Enhanced roles
│   ├── CrestApps.OrchardCore.ContentAccessControl/ # Content permissions
│   ├── CrestApps.OrchardCore.SignalR/      # SignalR integration
│   └── CrestApps.OrchardCore.Resources/    # Shared resources
├── Core/                       # Core libraries
├── Abstractions/              # Interface definitions
├── Startup/                   # Web applications
│   └── CrestApps.OrchardCore.Cms.Web/     # Main CMS web app
└── Targets/                   # Package bundles

tests/
└── CrestApps.OrchardCore.Tests/    # Unit test project

.github/workflows/              # CI/CD pipelines
```

### Important Files
- `CrestApps.OrchardCore.sln` - Main solution file
- `global.json` - .NET SDK version (10.0.100)
- `Directory.Build.props` - Common MSBuild properties
- `NuGet.config` - Package source configuration (includes CloudSmith feed)
- `package.json` - npm dependencies and scripts
- `gulpfile.js` - Asset build configuration
- `.editorconfig` - Code style and formatting rules

## Common Development Tasks

### Adding a New Module
1. Create new folder in `src/Modules/CrestApps.OrchardCore.{ModuleName}/`
2. Add module project file following existing patterns
3. Include `Manifest.cs` with module definition
4. Add module reference to appropriate target package

### Working with AI Modules
- **Base AI Module**: `CrestApps.OrchardCore.AI` - start here for AI-related changes
- **Chat Interface**: `CrestApps.OrchardCore.AI.Chat` - UI and chat functionality
- **Integrations**: Specific provider modules (OpenAI, Azure, Ollama)

### Frontend Development
- CSS/SCSS files are in individual module directories
- TypeScript/JavaScript files are built using gulp pipeline
- Run `npm run rebuild` after any frontend changes
- Use `npm run watch` for development with auto-rebuild

### Testing Workflow
1. Make code changes
2. Run `npm run rebuild` for frontend changes
3. Run `dotnet build` for backend changes (if network allows)
4. Run `dotnet test` for unit tests (if build succeeds)
5. Start web application and test manually

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

### Code Documentation
- XML documentation comments for public APIs
- Inline comments for complex logic only
- Keep comments up-to-date with code changes
- Avoid obvious comments that duplicate code

## CI/CD Integration

Before committing:
1. Run `npm run rebuild` - asset build must complete cleanly
2. Run `dotnet build` (if network allows) - solution must build without warnings
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
4. **Documentation**: Update README files and code comments
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
- ❌ Don't ignore compiler warnings (TreatWarningsAsErrors is enabled)
- ❌ Don't skip writing tests for new features
- ❌ Don't commit commented-out code
- ❌ Don't use `System.Range` or `System.Index` operators (enforced as warning)

## Useful Resources

- [Orchard Core Documentation](https://docs.orchardcore.net/)
- [Orchard Core GitHub](https://github.com/OrchardCMS/OrchardCore)
- [Project Repository](https://github.com/CrestApps/CrestApps.OrchardCore)
- [Contributing Guidelines](.github/CONTRIBUTING.md)
- [MIT License](https://opensource.org/licenses/MIT)