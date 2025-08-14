# CrestApps.OrchardCore Development Instructions

**ALWAYS reference these instructions first and fallback to search or bash commands only when you encounter unexpected information that does not match the info here.**

## Project Overview

CrestApps.OrchardCore is a collection of open-source modules for **Orchard Core CMS**, a modular application framework built on **ASP.NET Core/.NET 9**. The repository contains AI modules, user management enhancements, content access control, and other CMS extensions.

## Working Effectively

### Prerequisites and Environment Setup

Install .NET 9.0 SDK first:
```bash
# Add Microsoft package repository (Ubuntu/Debian)
wget https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb
rm packages-microsoft-prod.deb

# Install .NET 9.0 SDK - TAKES 1-2 MINUTES
sudo apt-get update
sudo apt-get install -y dotnet-sdk-9.0

# Verify installation
dotnet --version  # Should show 9.0.x
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
- `global.json` - .NET SDK version (9.0.100)
- `Directory.Build.props` - Common MSBuild properties
- `NuGet.config` - Package source configuration (includes CloudSmith feed)
- `package.json` - npm dependencies and scripts
- `gulpfile.js` - Asset build configuration

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
- **SDK version errors**: Ensure .NET 9.0.100+ is installed via `dotnet --version`
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

## CI/CD Integration

Before committing:
1. Run `npm run rebuild` - asset build must complete cleanly
2. Run `dotnet build` (if network allows) - solution must build without warnings
3. Run `dotnet test` (if build succeeds) - all tests must pass
4. Verify no uncommitted asset changes with `git status`

The CI pipeline validates builds on both Ubuntu and Windows, so test locally on similar environments when possible.

---

**Remember: Always build and validate your changes thoroughly. The modular architecture means changes can affect multiple modules, so comprehensive testing is essential.**