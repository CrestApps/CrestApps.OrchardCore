using CrestApps;
using CrestApps.AI;
using CrestApps.AI.A2A;
using CrestApps.AI.A2A.Models;
using CrestApps.AI.AzureAIInference;
using CrestApps.AI.Chat;
using CrestApps.AI.Chat.Tools;
using CrestApps.AI.DataSources.AzureAI;
using CrestApps.AI.DataSources.Elasticsearch;
using CrestApps.AI.Mcp;
using CrestApps.AI.Mcp.Handlers;
using CrestApps.AI.Models;
using CrestApps.AI.Ollama;
using CrestApps.AI.OpenAI;
using CrestApps.AI.OpenAI.Azure;
using CrestApps.AI.Mcp.Models;
using CrestApps.AI.Mcp.Services;
using CrestApps.AI.Tools;
using CrestApps.Data.YesSql;
using CrestApps.Data.YesSql.Services;
using CrestApps.Mvc.Web.Hubs;
using CrestApps.Mvc.Web.Indexes;
using CrestApps.Mvc.Web.Services;
using CrestApps.Mvc.Web.Tools;
using CrestApps.Services;
using CrestApps.SignalR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using NLog.Web;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Logging — NLog writes daily log files to App_Data/logs/.
// ---------------------------------------------------------------------------
builder.Logging.ClearProviders();
builder.Logging.SetMinimumLevel(LogLevel.Debug);
builder.WebHost.UseNLog();

var appDataPath = Path.Combine(builder.Environment.ContentRootPath, "App_Data");
Directory.CreateDirectory(appDataPath);

// ---------------------------------------------------------------------------
// Default AI Deployment Settings — persisted in a dedicated JSON file so that
// IOptionsMonitor<DefaultAIDeploymentSettings> reflects admin changes
// automatically via the configuration reload-on-change mechanism.
// ---------------------------------------------------------------------------
var deploymentDefaultsService = new JsonFileDeploymentDefaultsService(appDataPath);
var interactionDocumentSettingsService = new JsonFileInteractionDocumentSettingsService(appDataPath);
var aiDataSourceSettingsService = new JsonFileAIDataSourceSettingsService(appDataPath);
var mcpServerSettingsService = new JsonFileMcpServerSettingsService(appDataPath);

builder.Configuration.AddJsonFile(
    deploymentDefaultsService.FilePath, optional: true, reloadOnChange: true);
builder.Configuration.AddJsonFile(
    interactionDocumentSettingsService.FilePath, optional: true, reloadOnChange: true);
builder.Configuration.AddJsonFile(
    aiDataSourceSettingsService.FilePath, optional: true, reloadOnChange: true);

builder.Services.Configure<DefaultAIDeploymentSettings>(
    builder.Configuration.GetSection(JsonFileDeploymentDefaultsService.SectionKey));
builder.Services.Configure<InteractionDocumentSettings>(
    builder.Configuration.GetSection(JsonFileInteractionDocumentSettingsService.SectionKey));
builder.Services.Configure<AIDataSourceSettings>(
    builder.Configuration.GetSection(JsonFileAIDataSourceSettingsService.SectionKey));

builder.Services.AddSingleton(deploymentDefaultsService);
builder.Services.AddSingleton(interactionDocumentSettingsService);
builder.Services.AddSingleton(aiDataSourceSettingsService);
builder.Services.AddSingleton(mcpServerSettingsService);

// ---------------------------------------------------------------------------
// Authentication & Authorization
// ---------------------------------------------------------------------------
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy("Admin", policy => policy.RequireRole("Administrator"));

// ---------------------------------------------------------------------------
// CrestApps AI Framework — core services, orchestration, and SignalR.
// These registrations are required regardless of the data store you choose.
// ---------------------------------------------------------------------------
builder.Services
    .AddCrestAppsCoreServices()
    .AddCrestAppsAI()
    .AddCrestAppsA2AClient()
    .AddOrchestrationServices()
    .AddChatInteractionHandlers()
    .AddDefaultDocumentProcessingServices()
    .AddCrestAppsSignalR();

// ---------------------------------------------------------------------------
// AI Providers — register the completion clients you want to use.
// Each provider reads its configuration from appsettings.json automatically.
// ---------------------------------------------------------------------------
builder.Services
    .AddOpenAIProvider()
    .AddAzureOpenAIProvider()
    .AddOllamaProvider()
    .AddAzureAIInferenceProvider();

builder.Services.Configure<AIProviderOptions>(
    builder.Configuration.GetSection("CrestApps:AI:Providers"));
builder.Services.AddSingleton<MvcAIProviderOptionsStore>();
builder.Services.AddTransient<IConfigureOptions<AIProviderOptions>, MvcAIProviderOptionsConfiguration>();

// ---------------------------------------------------------------------------
// Search Providers — configure Elasticsearch and/or Azure AI Search for
// vector search, data sources, and document indexing.
// Connection settings are read from appsettings.json under "CrestApps:Search".
// ---------------------------------------------------------------------------
builder.Services
    .AddElasticsearchDataSourceServices(builder.Configuration.GetSection("CrestApps:Search:Elasticsearch"))
    .AddAzureAISearchDataSourceServices(builder.Configuration.GetSection("CrestApps:Search:AzureAISearch"));

builder.Services.AddMemoryCache();
builder.Services.AddHttpClient();
builder.Services.AddScoped<McpService>();
builder.Services.AddScoped<IOAuth2TokenService, DefaultOAuth2TokenService>();
builder.Services.AddScoped<IMcpClientTransportProvider, SseClientTransportProvider>();
builder.Services.AddScoped<IMcpClientTransportProvider, StdioClientTransportProvider>();
builder.Services.AddScoped<IMcpServerPromptService, DefaultMcpServerPromptService>();
builder.Services.AddScoped<IMcpServerResourceService, DefaultMcpServerResourceService>();
builder.Services.Configure<McpClientAIOptions>(options =>
{
    options.AddTransportType(McpConstants.TransportTypes.Sse, entry =>
    {
        entry.DisplayName = new LocalizedString("Server-Sent Events", "Server-Sent Events");
        entry.Description = new LocalizedString("Server-Sent Events Description", "Uses a remote MCP server over HTTP.");
    });
    options.AddTransportType(McpConstants.TransportTypes.StdIo, entry =>
    {
        entry.DisplayName = new LocalizedString("Standard Input/Output", "Standard Input/Output");
        entry.Description = new LocalizedString("Standard Input/Output Description", "Uses a local MCP process over standard input/output.");
    });
});
builder.Services.AddMcpResourceType<FtpResourceTypeHandler>(FtpResourceConstants.Type, entry =>
{
    entry.DisplayName = new LocalizedString("FTP", "FTP/FTPS");
    entry.Description = new LocalizedString("FTP Description", "Reads content from FTP/FTPS servers.");
    entry.SupportedVariables =
    [
        new McpResourceVariable("path") { Description = new LocalizedString("FTP Path", "The remote file path on the FTP server.") },
    ];
});
builder.Services.AddMcpResourceType<SftpResourceTypeHandler>(SftpResourceConstants.Type, entry =>
{
    entry.DisplayName = new LocalizedString("SFTP", "SFTP");
    entry.Description = new LocalizedString("SFTP Description", "Reads content from SFTP servers.");
    entry.SupportedVariables =
    [
        new McpResourceVariable("path") { Description = new LocalizedString("SFTP Path", "The remote file path on the SFTP server.") },
    ];
});

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

// ---------------------------------------------------------------------------
// AI Tools — register custom tools that AI profiles can invoke.
// ---------------------------------------------------------------------------
builder.Services.AddAITool<CalculatorTool>(CalculatorTool.TheName)
    .WithTitle("Calculator")
    .WithDescription("Performs basic arithmetic: add, subtract, multiply, or divide two numbers.")
    .WithCategory("Utilities")
    .Selectable();

builder.Services.AddAITool<DataSourceSearchTool>(DataSourceSearchTool.TheName)
    .WithPurpose(AIToolPurposes.DataSourceSearch);

// ---------------------------------------------------------------------------
// Data Store — YesSql with SQLite (default).
// If you prefer a different ORM (e.g., EF Core), replace this entire section
// with your own implementations of IAIProfileManager, IAIChatSessionManager,
// IAIChatSessionPromptStore, and IAIDocumentStore.
// ---------------------------------------------------------------------------
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
    .AddScoped<IAIMemoryStore, YesSqlAIMemoryStore>()
    .AddScoped<MvcAIDocumentIndexingService>()
    .AddDocumentCatalog<ChatInteraction, ChatInteractionIndex>()
    .AddScoped<ICatalogManager<ChatInteraction>, CatalogManager<ChatInteraction>>()
    .AddScoped<IChatInteractionPromptStore, YesSqlChatInteractionPromptStore>();

// Local file store for uploaded documents.
builder.Services.AddSingleton(new FileSystemFileStore(
    Path.Combine(appDataPath, "Documents")));

// Settings service for managing AI settings.
builder.Services.AddSingleton(new JsonFileSettingsService(appDataPath));

// ---------------------------------------------------------------------------
// MVC & SignalR
// ---------------------------------------------------------------------------
builder.Services.AddControllersWithViews();
builder.Services.AddSignalR()
    .AddJsonProtocol(options =>
    {
        options.PayloadSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });

var app = builder.Build();

// ---------------------------------------------------------------------------
// YesSql Schema Initialization — creates tables on first run.
// This block is only needed for YesSql; remove it if using another ORM.
// ---------------------------------------------------------------------------
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

// ---------------------------------------------------------------------------
// Middleware Pipeline
// ---------------------------------------------------------------------------
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

app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

await app.RunAsync();

// ---------------------------------------------------------------------------
// YesSql schema helper — creates index tables if they do not already exist.
// ---------------------------------------------------------------------------
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
            .Column<string>(nameof(AIChatSessionIndex.UserId), c => c.WithLength(255))));

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
