using CrestApps;
using CrestApps.AI;
using CrestApps.AI.A2A;
using CrestApps.AI.AzureAIInference;
using CrestApps.AI.Chat;
using CrestApps.AI.Chat.Endpoints;
using CrestApps.AI.Copilot;
using CrestApps.AI.Ftp;
using CrestApps.AI.Markdown;
using CrestApps.AI.Mcp;
using CrestApps.AI.Mcp.Models;
using CrestApps.AI.Models;
using CrestApps.AI.Ollama;
using CrestApps.AI.OpenAI;
using CrestApps.AI.OpenAI.Azure;
using CrestApps.AI.OpenXml;
using CrestApps.AI.Pdf;
using CrestApps.AI.Profiles;
using CrestApps.AI.Services;
using CrestApps.AI.Sftp;
using CrestApps.Azure.AISearch;
using CrestApps.Elasticsearch;
using CrestApps.Infrastructure.Indexing;
using CrestApps.Mvc.Web.Areas.Admin.Handlers;
using CrestApps.Mvc.Web.Areas.AI.Services;
using CrestApps.Mvc.Web.Areas.AIChat.BackgroundServices;
using CrestApps.Mvc.Web.Areas.AIChat.Endpoints;
using CrestApps.Mvc.Web.Areas.AIChat.Hubs;
using CrestApps.Mvc.Web.Areas.AIChat.Services;
using CrestApps.Mvc.Web.Areas.ChatInteractions.Hubs;
using CrestApps.Mvc.Web.Areas.DataSources.BackgroundServices;
using CrestApps.Mvc.Web.Areas.DataSources.Services;
using CrestApps.Mvc.Web.Services;
using CrestApps.Mvc.Web.Tools;
using CrestApps.Services;
using CrestApps.SignalR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using NLog.Web;

// =============================================================================
// CrestApps AI Framework — MVC Example Application
// =============================================================================
// This Program.cs demonstrates how to bootstrap an ASP.NET Core MVC application
// using the CrestApps AI Framework. It shows each feature registration step in
// the order they should be applied, with comments explaining what each extension
// does and why it is needed.
//
// Sections:
//   1. Logging
//   2. Application Configuration (App_Data appsettings override)
//   3. ASP.NET Core MVC setup
//   4. Authentication & Authorization
//   5. CrestApps foundation + AI services
//   6. AI Providers (OpenAI, Azure OpenAI, Ollama, Azure AI Inference)
//   7. Elasticsearch services

//   8. Azure AI Search services

//   9. MCP — Model Context Protocol (client + server)
//  10. Custom AI Tools
//  11. Data Store (YesSql / SQLite — replaceable with any ORM)
//  12. Background Tasks
//  13. Middleware Pipeline
// =============================================================================

var builder = WebApplication.CreateBuilder(args);

// =============================================================================
// 1. LOGGING
// =============================================================================
// NLog writes daily log files to App_Data/logs/. Replace with your preferred

// logging provider (Serilog, Application Insights, etc.) if desired.
// =============================================================================
builder.Logging.ClearProviders();
builder.Logging.SetMinimumLevel(LogLevel.Debug);
builder.WebHost.UseNLog();

var appDataPath = Path.Combine(builder.Environment.ContentRootPath, "App_Data");
Directory.CreateDirectory(appDataPath);

// =============================================================================
// 2. APPLICATION CONFIGURATION
// =============================================================================
// Three-layer configuration: base → environment override → App_Data override.
// The App_Data/appsettings.json file always wins so local secrets and

// machine-specific changes stay out of source control while still flowing
// through IConfiguration reload-on-change.
//
// AppDataConfigurationFileService writes admin-managed sections back into that
// same App_Data/appsettings.json file so runtime changes remain durable and the
// existing reloadOnChange configuration pipeline can refresh them automatically.
// =============================================================================

builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment}.json", optional: true, reloadOnChange: true)
    .AddJsonFile("App_Data/appsettings.json", optional: true, reloadOnChange: true);

// Persist settings into the same reloadable App_Data file that IConfiguration watches.
builder.Services.AddSingleton(new AppDataConfigurationFileService(appDataPath));

// Register typed wrappers over the App_Data-backed settings sections used by the MVC admin UI.
builder.Services.AddMvcAppDataSettings(builder.Configuration);

// =============================================================================
// 3. ASP.NET CORE MVC SETUP
// =============================================================================
// Start with the standard ASP.NET Core building blocks before adding CrestApps-
// specific features. This keeps the host framework registrations easy to find.
// =============================================================================
builder.Services.AddLocalization();
builder.Services.AddControllersWithViews();
builder.Services.AddHttpContextAccessor();
builder.Services.AddCrestAppsSignalR();

// =============================================================================
// 4. AUTHENTICATION & AUTHORIZATION
// =============================================================================
// Cookie-based authentication with a simple "Admin" policy. Replace with your
// preferred auth scheme (JWT, OpenID Connect, etc.).
// =============================================================================
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy("Admin", policy => policy.RequireRole("Administrator"));

// =============================================================================
// 5. CRESTAPPS FOUNDATION + AI SERVICES
// =============================================================================
// These are the shared CrestApps service registrations that sit on top of the
// normal ASP.NET Core host. Keep them together so consumers can clearly see the
// minimum CrestApps foundation, then remove optional features as needed.
// =============================================================================
builder.Services
    // Foundation services shared across the framework (for example OData validation).
    .AddCrestAppsCoreServices()

    // Core AI services: client factory, completion service, context builders, and AI options.
    .AddCrestAppsAI()
    .AddMarkdownServices()

    // Orchestration pipeline: IOrchestrator, tool registry, response handlers, and RAG flow.
    .AddOrchestrationServices()

    // GitHub Copilot-based orchestrator implementation for teams that want that experience.
    .AddCopilotOrchestrator()

    // Ad-hoc chat session handling and interaction orchestration.
    .AddChatInteractionServices()

    // Configure standard hub timeouts and message sizes for the chat interaction hub.
    .ConfigureChatHubOptions<ChatInteractionHub>()

    // Shared document ingestion, extraction, tabular processing, and RAG over attachments.
    .AddDefaultDocumentProcessingServices()
    .AddOpenXmlDocumentProcessingServices()
    .AddPdfDocumentProcessingServices()

    // Shared user-memory orchestration, tools, and preemptive retrieval behavior.
    .AddAIMemoryServices()

    // Agent-to-agent protocol support so remote agents can participate as tools.
    .AddCrestAppsA2AClient()

    // MCP client support for connecting to remote MCP servers.
    .AddCrestAppsMcpClient()

    // MCP server support for exposing prompts, tools, and resources from this app.
    .AddCrestAppsMcpServer()
    .AddFtpMcpResourceServices()
    .AddSftpMcpResourceServices()

    // A2A host support for exposing AI Agent profiles via Agent-to-Agent protocol.
    .AddA2AHost()

    // Real-time hub management for SignalR-based chat experiences.
    .AddCrestAppsSignalR();

// =============================================================================
// 6. AI PROVIDERS
// =============================================================================
// Register the AI completion providers you want to use. Each provider adds an
// IAICompletionClient implementation that knows how to communicate with its
// platform. Provider connection settings are read from appsettings.json under
// "CrestApps:AI:Providers". You only need to register the providers you use.
//
//   AddOpenAIProvider()                 — OpenAI (api.openai.com)
//   AddAzureOpenAIProvider()            — Azure OpenAI Service
//   AddOllamaProvider()                 — Ollama (local/self-hosted models)
//   AddAzureAIInferenceProvider()       — Azure AI Inference / GitHub Models
// =============================================================================
builder.Services
    .AddOpenAIProvider()
    .AddAzureOpenAIProvider()
    .AddOllamaProvider()
    .AddAzureAIInferenceProvider();

// Application-specific provider options configuration.
builder.Services.Configure<AIProviderOptions>(builder.Configuration.GetSection("CrestApps:AI:Providers"));

builder.Services.AddSingleton<MvcAIProviderOptionsStore>();
builder.Services.AddTransient<IConfigureOptions<AIProviderOptions>, MvcAIProviderOptionsConfiguration>();

// =============================================================================
// 7. ELASTICSEARCH SERVICES
// =============================================================================
// Keep each vector-search backend in its own group so it is obvious which block
// to remove when the application does not use that provider.
// =============================================================================
builder.Services
    // Register the Elasticsearch client and keyed search/indexing services.
    .AddElasticsearchServices(builder.Configuration.GetSection("CrestApps:Elasticsearch"))
    .AddElasticsearchAIDocumentSource()
    .AddElasticsearchAIDataSource()
    .AddElasticsearchAIMemorySource();

// =============================================================================
// 8. AZURE AI SEARCH SERVICES
// =============================================================================
// This block mirrors the Elasticsearch group so each provider's registrations
// stay together and are easy to remove independently.
// =============================================================================
builder.Services
    // Register the Azure AI Search client and keyed search/indexing services.
    .AddAzureAISearchServices(builder.Configuration.GetSection("CrestApps:AzureAISearch"))
    .AddAzureAISearchAIDocumentSource()
    .AddAzureAISearchAIDataSource()
    .AddAzureAISearchAIMemorySource();

// Add Articles support to show document support example.
builder.Services.TryAddEnumerable(ServiceDescriptor.Scoped<IIndexProfileHandler, ArticleIndexProfileHandler>());
builder.Services.AddKeyedScoped<IAIReferenceLinkResolver, ArticleAIReferenceLinkResolver>(IndexProfileTypes.Articles);
builder.Services.AddScoped<MvcCitationReferenceCollector>();
builder.Services.AddScoped<CompositeAIReferenceLinkResolver>();
builder.Services.AddScoped<IAIDataSourceIndexingService, DefaultAIDataSourceIndexingService>();
builder.Services.Configure<IndexProfileSourceOptions>(options =>
    options.AddOrUpdate(CrestApps.Elasticsearch.ServiceCollectionExtensions.ProviderName, "Elasticsearch", IndexProfileTypes.Articles, descriptor =>
    {
        descriptor.DisplayName = "Articles";
        descriptor.Description = "Create an Elasticsearch index for sample article records managed in the MVC app.";
    }));
builder.Services.Configure<IndexProfileSourceOptions>(options =>
    options.AddOrUpdate(CrestApps.Azure.AISearch.ServiceCollectionExtensions.ProviderName, "Azure AI Search", IndexProfileTypes.Articles, descriptor =>
    {
        descriptor.DisplayName = "Articles";
        descriptor.Description = "Create an Azure AI Search index for sample article records managed in the MVC app.";
    }));

// =============================================================================
// 9. MCP — MODEL CONTEXT PROTOCOL
// =============================================================================
// MCP server endpoint configuration (using the ModelContextProtocol SDK).
// This wires the CrestApps tool registry, prompt service, and resource service
// into the MCP protocol handlers served at the /mcp endpoint.

_ = builder.Services.AddMcpServer(options =>
{
    options.ServerInfo = new()
    {
        Name = "CrestApps MVC MCP Server",
        Version = "1.0",
    };
}).WithHttpTransport()
.WithCrestAppsHandlers();

// =============================================================================
// 10. CUSTOM AI TOOLS
// =============================================================================
// Register application-specific AI tools using the fluent builder pattern.
// Tools marked as Selectable() are visible in the UI for user assignment to
// profiles; system tools (no Selectable call) are used automatically by the
// orchestrator based on their Purpose.
// =============================================================================
builder.Services.AddAITool<CalculatorTool>(CalculatorTool.TheName)
    .WithTitle("Calculator")
    .WithDescription("Performs basic arithmetic: add, subtract, multiply, or divide two numbers.")
    .WithCategory("Utilities")
    .Selectable();

builder.Services.AddAITool<SendEmailTool>(SendEmailTool.TheName)
    .WithTitle("Send email")
    .WithDescription("Logs an email request with the supplied recipient, subject, and message.")
    .WithCategory("Communications")
    .Selectable();

// =============================================================================
// 11. DATA STORE — YesSql with SQLite
// =============================================================================
// The framework does not impose a specific data store. You must provide
// implementations of the store interfaces (IAIProfileManager,
// IAIChatSessionManager, IAIChatSessionPromptStore, IAIDocumentStore, etc.).
//
// This example uses YesSql with SQLite. To use Entity Framework Core or another
// ORM, replace this entire section with your own implementations.
// =============================================================================
builder.Services.AddYesSqlDataStore(appDataPath);

// Local file store for uploaded documents.
builder.Services.AddSingleton(new FileSystemFileStore(Path.Combine(appDataPath, "Documents")));

// Copilot orchestrator: credential store and options configuration.
builder.Services.AddScoped<ICopilotCredentialStore, JsonFileCopilotCredentialStore>();
builder.Services.ConfigureOptions<MvcCopilotOptionsConfiguration>();

// =============================================================================
// 12. BACKGROUND TASKS
// =============================================================================
// These hosted services run periodic maintenance work. Implement your own
// IHostedService or use these as reference implementations.
// =============================================================================

builder.Services.AddHostedService<AIChatSessionCloseBackgroundService>();
builder.Services.AddSingleton<MvcAIChatDocumentIndexingQueue>();
builder.Services.AddSingleton<IMvcAIChatDocumentIndexingQueue>(sp => sp.GetRequiredService<MvcAIChatDocumentIndexingQueue>());
builder.Services.AddHostedService<AIChatDocumentIndexingBackgroundService>();
builder.Services.AddSingleton<MvcAIDataSourceIndexingQueue>();
builder.Services.AddSingleton<IMvcAIDataSourceIndexingQueue>(sp => sp.GetRequiredService<MvcAIDataSourceIndexingQueue>());
builder.Services.AddHostedService<AIDataSourceIndexingBackgroundService>();
builder.Services.AddHostedService<DataSourceSyncBackgroundService>();
builder.Services.AddHostedService<DataSourceAlignmentBackgroundService>();

var app = builder.Build();

// YesSql schema initialization — creates tables on first run.
await app.Services.InitializeYesSqlSchemaAsync();

// Seed sample articles on first run.
await app.Services.SeedArticlesAsync();

using (var scope = app.Services.CreateScope())
{
    var providerConnections = await scope.ServiceProvider
        .GetRequiredService<ICatalog<AIProviderConnection>>()
        .GetAllAsync();

    app.Services.GetRequiredService<MvcAIProviderOptionsStore>()
        .Replace(providerConnections);
}

app.Services.GetRequiredService<IOptionsMonitorCache<AIProviderOptions>>()
    .TryRemove(Options.DefaultName);
_ = app.Services.GetRequiredService<IOptions<AIProviderOptions>>().Value;

// =============================================================================
// 13. MIDDLEWARE PIPELINE
// =============================================================================

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error")
        .UseHsts();
}

app.UseHttpsRedirection()
    .UseStaticFiles()
    .UseRouting()
    .UseAuthentication()
    .UseAuthorization();

app.UseWhen(context => context.Request.Path.StartsWithSegments("/mcp"), branch =>
{
    branch.Use(async (context, next) =>
    {
        var settings = await context.RequestServices.GetRequiredService<AppDataSettingsService<McpServerOptions>>().GetAsync();

        if (settings.AuthenticationType == McpServerAuthenticationType.None)
        {
            await next();

            return;
        }

        if (settings.AuthenticationType == McpServerAuthenticationType.ApiKey)
        {
            var authorization = context.Request.Headers.Authorization.ToString();
            var providedKey = authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                ? authorization["Bearer ".Length..]
                : authorization.StartsWith("ApiKey ", StringComparison.OrdinalIgnoreCase)
                    ? authorization["ApiKey ".Length..]
                    : authorization;

            if (!string.IsNullOrEmpty(settings.ApiKey) && string.Equals(providedKey, settings.ApiKey, StringComparison.Ordinal))
            {
                await next();

                return;
            }

            context.Response.StatusCode = StatusCodes.Status401Unauthorized;

            return;
        }

        if (context.User.Identity?.IsAuthenticated != true)
        {
            await context.ChallengeAsync();

            return;
        }

        if (settings.RequireAccessPermission && !context.User.IsInRole("Administrator"))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;

            return;
        }

        await next();
    });
});

app.MapHub<AIChatHub>("/hubs/ai-chat");
app.MapHub<ChatInteractionHub>("/hubs/chat-interaction");
app.MapMcp("mcp");
app.MapA2AHost();
app.AddChatApiEndpoints()
    .AddUploadChatInteractionDocumentEndpoint()
    .AddRemoveChatInteractionDocumentEndpoint()
    .AddUploadChatSessionDocumentEndpoint()
    .AddRemoveChatSessionDocumentEndpoint();

app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

await app.RunAsync();
