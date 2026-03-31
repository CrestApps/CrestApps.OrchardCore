using CrestApps;
using CrestApps.AI;
using CrestApps.AI.A2A;
using CrestApps.AI.A2A.Models;
using CrestApps.AI.AzureAIInference;
using CrestApps.AI.Chat;
using CrestApps.AI.Chat.Copilot;
using CrestApps.AI.Endpoints;
using CrestApps.AI.DataSources.AzureAI;
using CrestApps.AI.DataSources.Elasticsearch;
using CrestApps.AI.Mcp;
using CrestApps.AI.Mcp.Models;
using CrestApps.AI.Mcp.Services;
using CrestApps.AI.Models;
using CrestApps.AI.Ollama;
using CrestApps.AI.OpenAI;
using CrestApps.AI.OpenAI.Azure;
using CrestApps.AI.Tools;
using CrestApps.Data.YesSql;
using CrestApps.Mvc.Web.BackgroundTasks;
using CrestApps.Mvc.Web.Endpoints.Chat;
using CrestApps.Mvc.Web.Hubs;
using CrestApps.Mvc.Web.Indexes;
using CrestApps.Mvc.Web.Models;
using CrestApps.Mvc.Web.Services;
using CrestApps.Mvc.Web.Tools;
using CrestApps.Services;
using CrestApps.SignalR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.AI;
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
//   2. Application Configuration (JSON-file-backed settings)
//   3. Authentication & Authorization
//   4. CrestApps AI Framework (core + orchestration + chat + documents + SignalR)
//   5. AI Providers (OpenAI, Azure OpenAI, Ollama, Azure AI Inference)
//   6. Data Sources (Elasticsearch, Azure AI Search)
//   7. MCP — Model Context Protocol (client + server)
//   8. Custom AI Tools
//   9. Data Store (YesSql / SQLite — replaceable with any ORM)
//  10. Background Tasks
//  11. MVC & SignalR
//  12. Middleware Pipeline
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
// Settings are persisted in dedicated JSON files so that IOptionsMonitor<T>
// reflects admin changes automatically via the configuration reload-on-change
// mechanism. This is an application-level concern — replace these services with
// your own persistence strategy (database, Azure App Configuration, etc.).
// =============================================================================
var deploymentDefaultsService = new JsonFileDeploymentDefaultsService(appDataPath);
var interactionDocumentSettingsService = new JsonFileInteractionDocumentSettingsService(appDataPath);
var aiDataSourceSettingsService = new JsonFileAIDataSourceSettingsService(appDataPath);
var mcpServerSettingsService = new JsonFileMcpServerSettingsService(appDataPath);
var chatInteractionSettingsService = new JsonFileChatInteractionSettingsService(appDataPath);
var copilotSettingsService = new JsonFileCopilotSettingsService(appDataPath);

builder.Configuration.AddJsonFile(
    deploymentDefaultsService.FilePath, optional: true, reloadOnChange: true);
builder.Configuration.AddJsonFile(
    interactionDocumentSettingsService.FilePath, optional: true, reloadOnChange: true);
builder.Configuration.AddJsonFile(
    aiDataSourceSettingsService.FilePath, optional: true, reloadOnChange: true);
builder.Configuration.AddJsonFile(
    chatInteractionSettingsService.FilePath, optional: true, reloadOnChange: true);

builder.Services.Configure<DefaultAIDeploymentSettings>(
    builder.Configuration.GetSection(JsonFileDeploymentDefaultsService.SectionKey));
builder.Services.Configure<InteractionDocumentSettings>(
    builder.Configuration.GetSection(JsonFileInteractionDocumentSettingsService.SectionKey));
builder.Services.Configure<AIDataSourceSettings>(
    builder.Configuration.GetSection(JsonFileAIDataSourceSettingsService.SectionKey));
builder.Services.Configure<ChatInteractionSettings>(
    builder.Configuration.GetSection(JsonFileChatInteractionSettingsService.SectionKey));

builder.Services.AddSingleton(deploymentDefaultsService);
builder.Services.AddSingleton(interactionDocumentSettingsService);
builder.Services.AddSingleton(aiDataSourceSettingsService);
builder.Services.AddSingleton(mcpServerSettingsService);
builder.Services.AddSingleton(chatInteractionSettingsService);
builder.Services.AddSingleton(copilotSettingsService);

// =============================================================================
// 3. AUTHENTICATION & AUTHORIZATION
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
// 4. CRESTAPPS AI FRAMEWORK
// =============================================================================
// These are the core framework registrations that every CrestApps AI application
// needs. Each extension adds a distinct feature — see the framework docs for
// details on what each one provides and the interfaces you can implement.
//
//   AddCrestAppsCoreServices()          — Foundation services (OData validation).
//   AddCrestAppsAI()                    — Core AI services: IAIClientFactory for
//                                         creating chat/embedding/image clients,
//                                         IAICompletionService, context builders.
//   AddOrchestrationServices()          — The orchestration pipeline: IOrchestrator,
//                                         tool registry, response handlers, RAG,
//                                         and built-in tools (image/chart gen).
//   AddChatInteractionHandlers()        — Chat interaction support: ad-hoc chat
//                                         sessions with configurable parameters.
//   AddDefaultDocumentProcessingServices() — Document processing tools: upload,
//                                         text extraction, tabular data, and RAG
//                                         search over attached documents.
//   AddCrestAppsA2AClient()             — Agent-to-Agent (A2A) protocol: discover
//                                         remote agents and use them as tools.
//   AddCrestAppsSignalR()               — Real-time hub management for SignalR-
//                                         based chat experiences.
// =============================================================================
builder.Services
    .AddCrestAppsCoreServices()
    .AddCrestAppsAI()
    .AddOrchestrationServices()
    .AddCopilotOrchestrator()
    .AddChatInteractionHandlers()
    .AddDefaultDocumentProcessingServices()
    .AddCrestAppsA2AClient()
    .AddCrestAppsSignalR();

// =============================================================================
// 5. AI PROVIDERS
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
// 6. DATA SOURCES (Vector Search)
// =============================================================================
// Data sources enable retrieval-augmented generation (RAG) by connecting to
// search backends. Each provider registers keyed services for index management,
// document reading, and vector search. Connection settings are read from
// appsettings.json under "CrestApps:Search".
//
//   AddElasticsearchDataSourceServices()   — Elasticsearch vector search
//   AddAzureAISearchDataSourceServices()   — Azure AI Search vector search
// =============================================================================
builder.Services
    .AddElasticsearchDataSourceServices(builder.Configuration.GetSection("CrestApps:Search:Elasticsearch"))
    .AddAzureAISearchDataSourceServices(builder.Configuration.GetSection("CrestApps:Search:AzureAISearch"));

// =============================================================================
// 7. MCP — MODEL CONTEXT PROTOCOL
// =============================================================================
// MCP enables your application to connect to remote MCP servers (client mode)
// and to expose your AI tools, prompts, and resources to MCP clients (server
// mode). The client and server features are independent — enable what you need.
//
//   AddCrestAppsMcpClient()             — MCP client: transport providers (SSE,
//                                         StdIO), OAuth2, and the McpService for
//                                         connecting to remote MCP servers.
//   AddCrestAppsMcpServer()             — MCP server: prompt and resource serving,
//                                         built-in resource types (FTP, SFTP).
// =============================================================================
builder.Services
    .AddCrestAppsMcpClient()
    .AddCrestAppsMcpServer();

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
// 8. CUSTOM AI TOOLS
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
// 9. DATA STORE — YesSql with SQLite
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

    return store;
});

builder.Services.AddScoped(sp => sp.GetRequiredService<IStore>().CreateSession());

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
    .AddScoped<IAIChatDocumentAuthorizationService, MvcAIChatDocumentAuthorizationService>()
    .AddScoped<IAIChatDocumentEventHandler, MvcAIChatDocumentEventHandler>()
    .AddDocumentCatalog<ChatInteraction, ChatInteractionIndex>()
    .AddScoped<ICatalogManager<ChatInteraction>, CatalogManager<ChatInteraction>>()
    .AddScoped<IChatInteractionPromptStore, YesSqlChatInteractionPromptStore>();

// Local file store for uploaded documents.
builder.Services.AddSingleton(new FileSystemFileStore(
    Path.Combine(appDataPath, "Documents")));

// Settings service for managing AI settings.
builder.Services.AddSingleton(new JsonFileSettingsService(appDataPath));

// Copilot orchestrator: credential store and options configuration.
builder.Services.AddScoped<ICopilotCredentialStore, JsonFileCopilotCredentialStore>();
builder.Services.ConfigureOptions<MvcCopilotOptionsConfiguration>();

// =============================================================================
// 10. BACKGROUND TASKS
// =============================================================================
// These hosted services run periodic maintenance work. Implement your own
// IHostedService or use these as reference implementations.
// =============================================================================
builder.Services.AddHostedService<AIChatSessionCloseBackgroundService>();
builder.Services.AddHostedService<DataSourceSyncBackgroundService>();
builder.Services.AddHostedService<DataSourceAlignmentBackgroundService>();

// =============================================================================
// 11. MVC & SIGNALR
// =============================================================================
builder.Services.AddControllersWithViews();
builder.Services.AddSignalR()
    .AddJsonProtocol(options =>
    {
        options.PayloadSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });

var app = builder.Build();

// YesSql schema initialization — creates tables on first run.
await InitializeYesSqlSchemaAsync(app.Services);
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
// 12. MIDDLEWARE PIPELINE
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
        var settings = await context.RequestServices.GetRequiredService<JsonFileMcpServerSettingsService>().GetAsync();

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

    await transaction.CommitAsync();
}

async Task TryCreateTableAsync(SchemaBuilder _, Func<Task> createTable)
{
    try { await createTable(); }
    catch { /* Table already exists. */ }
}
