using CrestApps;
using CrestApps.AI;
using CrestApps.AI.A2A;
using CrestApps.AI.A2A.Models;
using CrestApps.AI.AzureAIInference;
using CrestApps.AI.Chat;
using CrestApps.AI.Chat.Endpoints;
using CrestApps.AI.Copilot;
using CrestApps.AI.DataSources;
using CrestApps.AI.Indexing;
using CrestApps.AI.Mcp;
using CrestApps.AI.Mcp.Models;
using CrestApps.AI.Mcp.Services;
using CrestApps.AI.Memory;
using CrestApps.AI.Models;
using CrestApps.AI.Ollama;
using CrestApps.AI.OpenAI;
using CrestApps.AI.OpenAI.Azure;
using CrestApps.AI.Profiles;
using CrestApps.AI.Tooling;
using CrestApps.AI.Tools;
using CrestApps.Azure.AISearch;
using CrestApps.Data.YesSql;
using CrestApps.Elasticsearch;
using CrestApps.Infrastructure.Indexing;
using CrestApps.Mvc.Web.Areas.A2A.Indexes;
using CrestApps.Mvc.Web.Areas.Admin.Handlers;
using CrestApps.Mvc.Web.Areas.Admin.Indexes;
using CrestApps.Mvc.Web.Areas.Admin.Models;
using CrestApps.Mvc.Web.Areas.Admin.Services;
using CrestApps.Mvc.Web.Areas.AI.Indexes;
using CrestApps.Mvc.Web.Areas.AI.Services;
using CrestApps.Mvc.Web.Areas.AIChat.BackgroundServices;
using CrestApps.Mvc.Web.Areas.AIChat.Endpoints;
using CrestApps.Mvc.Web.Areas.AIChat.Hubs;
using CrestApps.Mvc.Web.Areas.AIChat.Indexes;
using CrestApps.Mvc.Web.Areas.AIChat.Services;
using CrestApps.Mvc.Web.Areas.ChatInteractions.Hubs;
using CrestApps.Mvc.Web.Areas.ChatInteractions.Indexes;
using CrestApps.Mvc.Web.Areas.ChatInteractions.Services;
using CrestApps.Mvc.Web.Areas.DataSources.BackgroundServices;
using CrestApps.Mvc.Web.Areas.DataSources.Indexes;
using CrestApps.Mvc.Web.Areas.DataSources.Services;
using CrestApps.Mvc.Web.Areas.Indexing.Indexes;
using CrestApps.Mvc.Web.Areas.Indexing.Services;
using CrestApps.Mvc.Web.Areas.Mcp.Indexes;
using CrestApps.Mvc.Web.Services;
using CrestApps.Mvc.Web.Tools;
using CrestApps.Services;
using CrestApps.SignalR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using NLog.Web;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

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
builder.Services.AddSignalR()
    .AddJsonProtocol(options =>
    {
        options.PayloadSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });

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

// Agent-to-agent protocol support so remote agents can participate as tools.
.AddCrestAppsA2AClient()

// MCP client support for connecting to remote MCP servers.
.AddCrestAppsMcpClient()

// MCP server support for exposing prompts, tools, and resources from this app.
.AddCrestAppsMcpServer()

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

})
.WithHttpTransport()
.WithListToolsHandler((request, cancellationToken) =>
{
    var toolDefinitions = request.Services.GetRequiredService<IOptions<AIToolDefinitionOptions>>().Value;

    var tools = new List<Tool>();

    foreach (var (name, _) in toolDefinitions.Tools)
    {
        if (request.Services.GetKeyedService<AITool>(name) is AIFunction aiFunction)
        {
            tools.Add(new Tool
            {
                Name = aiFunction.Name,
                Description = aiFunction.Description,
                InputSchema = aiFunction.JsonSchema,
            });

        }
    }

    var sdkTools = request.Services.GetService<IEnumerable<McpServerTool>>();

    if (sdkTools is not null)
    {

        foreach (var sdkTool in sdkTools)
        {
            if (!tools.Any(tool => tool.Name == sdkTool.ProtocolTool.Name))
            {
                tools.Add(sdkTool.ProtocolTool);
            }

        }
    }

    return ValueTask.FromResult(new ListToolsResult { Tools = tools });

})
.WithCallToolHandler(async (request, cancellationToken) =>

{
    var toolDefinitions = request.Services.GetRequiredService<IOptions<AIToolDefinitionOptions>>().Value;

    if (toolDefinitions.Tools.ContainsKey(request.Params.Name))
    {
        if (request.Services.GetKeyedService<AITool>(request.Params.Name) is not AIFunction aiFunction)
        {
            throw new ModelContextProtocol.McpException($"Failed to create tool '{request.Params.Name}'.");

        }

        var arguments = new AIFunctionArguments
        {
            Services = request.Services,
            Context = new Dictionary<object, object> { ["mcpRequest"] = request },
        };

        if (request.Params.Arguments is not null)
        {

            foreach (var kvp in request.Params.Arguments)
            {
                arguments[kvp.Key] = kvp.Value;
            }

        }

        var result = await aiFunction.InvokeAsync(arguments, cancellationToken);

        return new CallToolResult
        {
            Content = [new TextContentBlock { Text = result?.ToString() ?? string.Empty }],
        };
    }

    var sdkTools = request.Services.GetService<IEnumerable<McpServerTool>>();
    var sdkTool = sdkTools?.FirstOrDefault(tool => tool.ProtocolTool.Name == request.Params.Name);

    if (sdkTool is not null)
    {
        return await sdkTool.InvokeAsync(request, cancellationToken);
    }

    throw new ModelContextProtocol.McpException($"Tool '{request.Params.Name}' not found.");
})
.WithListPromptsHandler(async (request, cancellationToken) =>
{
    var promptService = request.Services.GetRequiredService<IMcpServerPromptService>();

    return new ListPromptsResult { Prompts = await promptService.ListAsync() };

})
.WithGetPromptHandler(async (request, cancellationToken) =>
{
    var promptService = request.Services.GetRequiredService<IMcpServerPromptService>();

    return await promptService.GetAsync(request, cancellationToken);
})
.WithListResourcesHandler(async (request, cancellationToken) =>
{

    var resourceService = request.Services.GetRequiredService<IMcpServerResourceService>();

    return new ListResourcesResult { Resources = await resourceService.ListAsync() };

})
.WithListResourceTemplatesHandler(async (request, cancellationToken) =>
{
    var resourceService = request.Services.GetRequiredService<IMcpServerResourceService>();

    return new ListResourceTemplatesResult { ResourceTemplates = await resourceService.ListTemplatesAsync() };

})
.WithReadResourceHandler(async (request, cancellationToken) =>
{
    var resourceService = request.Services.GetRequiredService<IMcpServerResourceService>();

    return await resourceService.ReadAsync(request, cancellationToken);
});

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

builder.Services.AddAITool<DataSourceSearchTool>(DataSourceSearchTool.TheName)
    .WithPurpose(AIToolPurposes.DataSourceSearch);

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
builder.Services.AddSingleton(sp =>
{
    var dbPath = Path.Combine(appDataPath, "crestapps.db");

    var store = StoreFactory.CreateAndInitializeAsync(
        new Configuration()
    .UseSqLite($"Data Source={dbPath};Cache=Shared")
    .SetTablePrefix("CA_"))
    .GetAwaiter().GetResult();

    store.RegisterIndexes<AIProfileIndexProvider>();
    store.RegisterIndexes<AIProviderConnectionIndexProvider>();
    store.RegisterIndexes<A2AConnectionIndexProvider>();
    store.RegisterIndexes<McpConnectionIndexProvider>();
    store.RegisterIndexes<McpPromptIndexProvider>();

    store.RegisterIndexes<McpResourceIndexProvider>();
    store.RegisterIndexes<AIDeploymentIndexProvider>();

    store.RegisterIndexes<AIProfileTemplateIndexProvider>();

    store.RegisterIndexes<AIChatSessionIndexProvider>();
    store.RegisterIndexes<AIChatSessionPromptIndexProvider>();
    store.RegisterIndexes<AIDocumentIndexProvider>();
    store.RegisterIndexes<AIDocumentChunkIndexProvider>();
    store.RegisterIndexes<SearchIndexProfileIndexProvider>();
    store.RegisterIndexes<AIDataSourceIndexProvider>();
    store.RegisterIndexes<AIMemoryEntryIndexProvider>();
    store.RegisterIndexes<ChatInteractionIndexProvider>();
    store.RegisterIndexes<ChatInteractionPromptIndexProvider>();
    store.RegisterIndexes<ArticleIndexProvider>();

    return store;
}).AddScoped(sp => sp.GetRequiredService<IStore>().CreateSession());

// YesSql-backed catalogs and managers.
builder.Services
    .AddNamedSourceDocumentCatalog<AIProfile, AIProfileIndex>()
    .AddNamedSourceDocumentCatalog<AIProviderConnection, AIProviderConnectionIndex>()
    .AddDocumentCatalog<A2AConnection, A2AConnectionIndex>()
    .AddSourceDocumentCatalog<McpConnection, McpConnectionIndex>()
    .AddNamedDocumentCatalog<McpPrompt, McpPromptIndex>()
    .AddSourceDocumentCatalog<McpResource, McpResourceIndex>()
    .AddNamedSourceDocumentCatalog<AIDeployment, AIDeploymentIndex>()
    .AddNamedSourceDocumentCatalog<AIProfileTemplate, AIProfileTemplateIndex>()
    .AddScoped<IAIProfileManager, SimpleAIProfileManager>()
    .AddScoped<IAIChatSessionManager, YesSqlAIChatSessionManager>()
    .AddScoped<IAIChatSessionPromptStore, YesSqlAIChatSessionPromptStore>()
    .AddScoped<IAIDocumentStore, YesSqlAIDocumentStore>()
    .AddScoped<IAIDocumentChunkStore, YesSqlAIDocumentChunkStore>()
    .AddScoped<ISearchIndexProfileStore, YesSqlSearchIndexProfileStore>()

    .AddScoped<IAIDataSourceStore, YesSqlAIDataSourceStore>()
    .AddScoped<ICatalog<AIDataSource>>(sp => sp.GetRequiredService<IAIDataSourceStore>())
    .AddScoped<IAIMemoryStore, YesSqlAIMemoryStore>()
    .AddScoped<MvcAIDocumentIndexingService>()
    .AddScoped<ISearchIndexProfileManager, SearchIndexProfileManager>()

    .AddScoped<IAIChatDocumentAuthorizationService, MvcAIChatDocumentAuthorizationService>()
    .AddScoped<IAIChatDocumentEventHandler, MvcAIChatDocumentEventHandler>()
    .AddDocumentCatalog<ChatInteraction, ChatInteractionIndex>()

    .AddScoped<ICatalogManager<ChatInteraction>, CatalogManager<ChatInteraction>>()
    .AddScoped<IChatInteractionPromptStore, YesSqlChatInteractionPromptStore>()
    .AddDocumentCatalog<Article, ArticleIndex>()
    .AddScoped<ICatalogManager<Article>, CatalogManager<Article>>()
    .AddScoped<ICatalogEntryHandler<Article>, ArticleIndexingHandler>()
    .AddScoped<ArticleIndexingService>();

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

builder.Services.AddHostedService<DataSourceSyncBackgroundService>();
builder.Services.AddHostedService<DataSourceAlignmentBackgroundService>();


var app = builder.Build();

// YesSql schema initialization — creates tables on first run.

await InitializeYesSqlSchemaAsync(app.Services);

// Seed sample articles on first run.
await SeedArticlesAsync(app.Services);

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
        var settings = await context.RequestServices.GetRequiredService<AppDataSettingsService<CrestApps.AI.Mcp.Models.McpServerOptions>>().GetAsync();

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

// =============================================================================
// YesSql Schema Helper
// =============================================================================
// Creates index tables if they do not already exist. Remove this section if
// you are using a different ORM that handles its own schema management.
// =============================================================================
async Task InitializeYesSqlSchemaAsync(IServiceProvider services)
{
    var store = services.GetRequiredService<IStore>();
    await using var connection = store.Configuration.ConnectionFactory.CreateConnection();

    await connection.OpenAsync();
    await using var transaction = await connection.BeginTransactionAsync();
    var schemaBuilder = new SchemaBuilder(store.Configuration, transaction);

    await TryCreateTableAsync(schemaBuilder, () =>
    schemaBuilder.CreateMapIndexTableAsync<AIProfileIndex>(t => t

        .Column<string>(nameof(AIProfileIndex.ItemId), c => c.WithLength(26))
        .Column<string>(nameof(AIProfileIndex.Name), c => c.WithLength(255))
        .Column<string>(nameof(AIProfileIndex.Source), c => c.WithLength(255))));

    await TryCreateTableAsync(schemaBuilder, () =>
    schemaBuilder.CreateMapIndexTableAsync<AIProviderConnectionIndex>(t => t

        .Column<string>(nameof(AIProviderConnectionIndex.ItemId), c => c.WithLength(26))
        .Column<string>(nameof(AIProviderConnectionIndex.Name), c => c.WithLength(255))
        .Column<string>(nameof(AIProviderConnectionIndex.Source), c => c.WithLength(255))));

    await TryCreateTableAsync(schemaBuilder, () =>

    schemaBuilder.CreateMapIndexTableAsync<A2AConnectionIndex>(t => t
        .Column<string>(nameof(A2AConnectionIndex.ItemId), c => c.WithLength(26))
        .Column<string>(nameof(A2AConnectionIndex.DisplayText), c => c.WithLength(255))));

    await TryCreateTableAsync(schemaBuilder, () =>
    schemaBuilder.CreateMapIndexTableAsync<McpConnectionIndex>(t => t

        .Column<string>(nameof(McpConnectionIndex.ItemId), c => c.WithLength(26))
        .Column<string>(nameof(McpConnectionIndex.DisplayText), c => c.WithLength(255))
        .Column<string>(nameof(McpConnectionIndex.Source), c => c.WithLength(50))));

    await TryCreateTableAsync(schemaBuilder, () =>

    schemaBuilder.CreateMapIndexTableAsync<McpPromptIndex>(t => t
        .Column<string>(nameof(McpPromptIndex.ItemId), c => c.WithLength(26))
        .Column<string>(nameof(McpPromptIndex.Name), c => c.WithLength(255))));

    await TryCreateTableAsync(schemaBuilder, () =>
    schemaBuilder.CreateMapIndexTableAsync<McpResourceIndex>(t => t

        .Column<string>(nameof(McpResourceIndex.ItemId), c => c.WithLength(26))
        .Column<string>(nameof(McpResourceIndex.DisplayText), c => c.WithLength(255))
        .Column<string>(nameof(McpResourceIndex.Source), c => c.WithLength(50))));

    await TryCreateTableAsync(schemaBuilder, () =>
    schemaBuilder.CreateMapIndexTableAsync<AIDeploymentIndex>(t => t

        .Column<string>(nameof(AIDeploymentIndex.ItemId), c => c.WithLength(26))
        .Column<string>(nameof(AIDeploymentIndex.Name), c => c.WithLength(255))
        .Column<string>(nameof(AIDeploymentIndex.Source), c => c.WithLength(255))));

    await TryCreateTableAsync(schemaBuilder, () =>
    schemaBuilder.CreateMapIndexTableAsync<AIProfileTemplateIndex>(t => t

        .Column<string>(nameof(AIProfileTemplateIndex.ItemId), c => c.WithLength(26))
        .Column<string>(nameof(AIProfileTemplateIndex.Name), c => c.WithLength(255))
        .Column<string>(nameof(AIProfileTemplateIndex.Source), c => c.WithLength(255))));

    await TryCreateTableAsync(schemaBuilder, () =>
    schemaBuilder.CreateMapIndexTableAsync<AIChatSessionIndex>(t => t
        .Column<string>(nameof(AIChatSessionIndex.ItemId), c => c.WithLength(44))
        .Column<string>(nameof(AIChatSessionIndex.SessionId), c => c.WithLength(44))
        .Column<string>(nameof(AIChatSessionIndex.ProfileId), c => c.WithLength(26))

        .Column<string>(nameof(AIChatSessionIndex.UserId), c => c.WithLength(255))
        .Column<int>(nameof(AIChatSessionIndex.Status))
        .Column<DateTime>(nameof(AIChatSessionIndex.LastActivityUtc))));

    await TryCreateTableAsync(schemaBuilder, () =>
    schemaBuilder.CreateMapIndexTableAsync<AIChatSessionPromptIndex>(t => t

        .Column<string>(nameof(AIChatSessionPromptIndex.ItemId), c => c.WithLength(26))
        .Column<string>(nameof(AIChatSessionPromptIndex.SessionId), c => c.WithLength(44))
        .Column<string>(nameof(AIChatSessionPromptIndex.Role), c => c.WithLength(50))));

    await TryCreateTableAsync(schemaBuilder, () =>
    schemaBuilder.CreateMapIndexTableAsync<AIDocumentIndex>(t => t
        .Column<string>(nameof(AIDocumentIndex.ItemId), c => c.WithLength(26))

        .Column<string>(nameof(AIDocumentIndex.ReferenceId), c => c.WithLength(26))
        .Column<string>(nameof(AIDocumentIndex.ReferenceType), c => c.WithLength(50))
        .Column<string>(nameof(AIDocumentIndex.FileName), c => c.WithLength(255))));

    await TryCreateTableAsync(schemaBuilder, () =>
    schemaBuilder.CreateMapIndexTableAsync<AIDocumentChunkIndex>(t => t
        .Column<string>(nameof(AIDocumentChunkIndex.ItemId), c => c.WithLength(26))
        .Column<string>(nameof(AIDocumentChunkIndex.AIDocumentId), c => c.WithLength(26))

        .Column<string>(nameof(AIDocumentChunkIndex.ReferenceId), c => c.WithLength(26))
        .Column<string>(nameof(AIDocumentChunkIndex.ReferenceType), c => c.WithLength(50))
        .Column<int>(nameof(AIDocumentChunkIndex.Index))));

    await TryCreateTableAsync(schemaBuilder, () =>
    schemaBuilder.CreateMapIndexTableAsync<SearchIndexProfileIndex>(t => t
        .Column<string>(nameof(SearchIndexProfileIndex.ItemId), c => c.WithLength(26))

        .Column<string>(nameof(SearchIndexProfileIndex.Name), c => c.WithLength(255))
        .Column<string>(nameof(SearchIndexProfileIndex.ProviderName), c => c.WithLength(50))
        .Column<string>(nameof(SearchIndexProfileIndex.Type), c => c.WithLength(50))));

    await TryCreateTableAsync(schemaBuilder, () =>
    schemaBuilder.CreateMapIndexTableAsync<AIDataSourceIndex>(t => t

        .Column<string>(nameof(AIDataSourceIndex.ItemId), c => c.WithLength(26))
        .Column<string>(nameof(AIDataSourceIndex.DisplayText), c => c.WithLength(255))
        .Column<string>(nameof(AIDataSourceIndex.SourceIndexProfileName), c => c.WithLength(255))));

    await TryCreateTableAsync(schemaBuilder, () =>
    schemaBuilder.CreateMapIndexTableAsync<AIMemoryEntryIndex>(t => t

        .Column<string>(nameof(AIMemoryEntryIndex.ItemId), c => c.WithLength(26))
        .Column<string>(nameof(AIMemoryEntryIndex.UserId), c => c.WithLength(255))
        .Column<string>(nameof(AIMemoryEntryIndex.Name), c => c.WithLength(255))));

    await TryCreateTableAsync(schemaBuilder, () =>
    schemaBuilder.CreateMapIndexTableAsync<ChatInteractionIndex>(t => t
        .Column<string>(nameof(ChatInteractionIndex.ItemId), c => c.WithLength(26))

        .Column<string>(nameof(ChatInteractionIndex.UserId), c => c.WithLength(255))
        .Column<string>(nameof(ChatInteractionIndex.Title), c => c.WithLength(255))
        .Column<DateTime>(nameof(ChatInteractionIndex.CreatedUtc))));

    await TryCreateTableAsync(schemaBuilder, () =>
    schemaBuilder.CreateMapIndexTableAsync<ChatInteractionPromptIndex>(t => t
        .Column<string>(nameof(ChatInteractionPromptIndex.ItemId), c => c.WithLength(26))

        .Column<string>(nameof(ChatInteractionPromptIndex.ChatInteractionId), c => c.WithLength(26))
        .Column<string>(nameof(ChatInteractionPromptIndex.Role), c => c.WithLength(50))
        .Column<DateTime>(nameof(ChatInteractionPromptIndex.CreatedUtc))));

    await TryCreateTableAsync(schemaBuilder, () =>

    schemaBuilder.CreateMapIndexTableAsync<ArticleIndex>(t => t
        .Column<string>(nameof(ArticleIndex.ItemId), c => c.WithLength(26))

        .Column<string>(nameof(ArticleIndex.Title), c => c.WithLength(255))));

    await transaction.CommitAsync();
}

async Task TryCreateTableAsync(SchemaBuilder _, Func<Task> createTable)
{

    try { await createTable(); }
    catch { /* Table already exists. */ }
}

// =============================================================================
// Article Seed Data
// =============================================================================
// Seeds the database with sample articles on first run. Subsequent runs skip
// seeding because articles already exist.
// =============================================================================
async Task SeedArticlesAsync(IServiceProvider services)
{

    using var scope = services.CreateScope();
    var catalog = scope.ServiceProvider.GetRequiredService<ICatalog<Article>>();
    var existing = await catalog.GetAllAsync();

    if (existing.Count > 0)

    {
        return;
    }

    var articles = new[]
    {
        new Article
        {
            ItemId = UniqueId.GenerateId(),
            Title = "What Are Large Language Models?",

            CreatedUtc = DateTime.UtcNow,

            Description = """

# What Are Large Language Models?

Large Language Models (LLMs) are deep learning models trained on vast corpora of text data. They learn statistical patterns in language and can generate coherent, context-aware text.

## Key Characteristics


- **Scale**: Billions of parameters trained on terabytes of text.

- **Generalization**: Capable of performing many tasks without task-specific training.
- **Context Window**: The amount of text the model can consider at once.

## Common Use Cases


1. Conversational AI and chatbots

2. Content generation and summarization

3. Code assistance and generation
4. Translation and language understanding

LLMs form the backbone of modern AI assistants and are the foundation for tools like GitHub Copilot.
""",

        },
        new Article
        {
            ItemId = UniqueId.GenerateId(),
            Title = "Understanding Embeddings and Vector Search",

            CreatedUtc = DateTime.UtcNow,

            Description = """

# Understanding Embeddings and Vector Search

Embeddings are numerical representations of text (or other data) in a high-dimensional vector space. Similar concepts end up close together in this space.

## How Embeddings Work

An embedding model converts text into a fixed-length array of floating-point numbers. For example, the sentence "The cat sat on the mat" might become a 1536-dimensional vector.

## Vector Search

Vector search (also called semantic search) finds documents whose embeddings are closest to a query embedding using distance metrics like **cosine similarity** or **dot product**.

### Why It Matters


- Traditional keyword search misses synonyms and paraphrases.

- Vector search understands meaning, not just exact words.
- Combining both approaches (hybrid search) gives the best results.

## Providers


Popular embedding providers include OpenAI (`text-embedding-3-small`), Azure OpenAI, and open-source models like Sentence Transformers.

""",
        },
        new Article
        {
            ItemId = UniqueId.GenerateId(),
            Title = "Retrieval-Augmented Generation (RAG) Explained",

            CreatedUtc = DateTime.UtcNow,

            Description = """

# Retrieval-Augmented Generation (RAG) Explained

RAG is an architecture that combines information retrieval with text generation. Instead of relying solely on the model's training data, RAG retrieves relevant documents at query time and includes them in the prompt.

## The RAG Pipeline


1. **User Query** — The user asks a question.

2. **Retrieval** — The system searches a knowledge base (using vector search) for relevant documents.

3. **Augmentation** — Retrieved documents are added to the prompt as context.
4. **Generation** — The LLM generates a response grounded in the retrieved context.

## Benefits


- **Accuracy**: Responses are grounded in actual data, reducing hallucinations.

- **Freshness**: The knowledge base can be updated without retraining the model.
- **Transparency**: Sources can be cited alongside the generated answer.

## Implementation Tips


- Use chunk sizes of 500–1000 tokens for best retrieval quality.

- Overlap chunks by 10–20% to preserve context boundaries.
- Always include metadata (source, date) with each chunk.
""",
        },
        new Article
        {
            ItemId = UniqueId.GenerateId(),
            Title = "Search Indexing with Elasticsearch",

            CreatedUtc = DateTime.UtcNow,

            Description = """
                # Search Indexing with Elasticsearch

                Elasticsearch is a distributed search and analytics engine built on Apache Lucene. It supports full-text search, structured queries, and dense vector search for AI workloads.

                ## Core Concepts

                - **Index**: A collection of documents with a defined schema (mapping).
                - **Document**: A JSON object stored in an index.
                - **Mapping**: Defines field types (keyword, text, dense_vector, date, etc.).

                ## Setting Up an Articles Index

                ```json
                {
                  "mappings": {
                    "properties": {
                      "article_id": { "type": "keyword" },
                      "title":      { "type": "text" },
                      "description": { "type": "text" },
                      "author":     { "type": "keyword" },
                      "created_utc": { "type": "date" }
                    }
                  }
                }
                ```

                ## Best Practices

                1. Use `keyword` for IDs and exact-match filters.
                2. Use `text` for full-text searchable fields.
                3. Keep index mappings minimal — only index fields you need to query.
                4. Use bulk operations for efficient batch indexing.
                """,
        },
        new Article
        {
            ItemId = UniqueId.GenerateId(),
            Title = "Azure AI Search for Knowledge Bases",

            CreatedUtc = DateTime.UtcNow,

            Description = """

# Azure AI Search for Knowledge Bases

Azure AI Search (formerly Azure Cognitive Search) is a fully managed cloud search service that supports full-text search, vector search, and hybrid queries.

## Key Features


- **Vector Search**: Built-in support for HNSW-based approximate nearest neighbor search.

- **Semantic Ranking**: AI-powered re-ranking of search results for better relevance.

- **Integrated Vectorization**: Automatic embedding generation during indexing.
- **Hybrid Search**: Combine keyword and vector search in a single query.

## Creating an Index


Define fields with appropriate types:

- `Edm.String` with `searchable: true` for text fields.

- `Edm.String` with `filterable: true` for keyword fields.
- `Collection(Edm.Single)` for vector fields with HNSW configuration.

## Integration Patterns


Azure AI Search integrates naturally with Azure OpenAI for RAG scenarios. The "On Your Data" feature allows direct connection between Azure OpenAI chat completions and an Azure AI Search index.

""",

        },
        new Article
        {
            ItemId = UniqueId.GenerateId(),
            Title = "Building Custom Data Sources for AI",

            CreatedUtc = DateTime.UtcNow,

            Description = """

# Building Custom Data Sources for AI

A data source connects your application's structured data to AI search indexes. This allows AI assistants to answer questions using your specific domain knowledge.

## Architecture Overview

```
Application Data → Indexing Service → Search Index → Data Source → AI Profile
```

1. **Application Data**: Your domain models (articles, products, tickets, etc.).
2. **Indexing Service**: Transforms models into search documents with defined field mappings.

3. **Search Index**: Stores documents in Elasticsearch or Azure AI Search.

4. **Data Source**: Maps the search index to the AI system with field name configuration.

5. **AI Profile**: Uses data sources as knowledge bases during chat.

## Keeping Data in Sync

Use catalog handlers (`ICatalogEntryHandler<T>`) to automatically trigger re-indexing whenever a record is created, updated, or deleted. This ensures the search index always reflects the current state of your data.

## Field Mapping


Define your index fields carefully:
- **Key field**: Unique identifier (usually the record ID).
- **Searchable fields**: Title, description, content — fields users will search.
- **Filterable fields**: Author, category, date — fields used for filtering.

""",

        },
    };

    foreach (var article in articles)

    {
        await catalog.CreateAsync(article);

    }

    await catalog.SaveChangesAsync();
}
