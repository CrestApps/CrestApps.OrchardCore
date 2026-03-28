using CrestApps;
using CrestApps.AI;
using CrestApps.AI.AzureAIInference;
using CrestApps.AI.Chat;
using CrestApps.AI.DataSources.AzureAI;
using CrestApps.AI.DataSources.Elasticsearch;
using CrestApps.AI.Models;
using CrestApps.AI.Ollama;
using CrestApps.AI.OpenAI;
using CrestApps.AI.OpenAI.Azure;
using CrestApps.Data.YesSql;
using CrestApps.Data.YesSql.Services;
using CrestApps.Mvc.Web.Hubs;
using CrestApps.Mvc.Web.Indexes;
using CrestApps.Mvc.Web.Services;
using CrestApps.Mvc.Web.Tools;
using CrestApps.Services;
using CrestApps.SignalR;
using Microsoft.AspNetCore.Authentication.Cookies;
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
    .AddOrchestrationServices()
    .AddChatInteractionHandlers()
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

// ---------------------------------------------------------------------------
// Search Providers — configure Elasticsearch and/or Azure AI Search for
// vector search, data sources, and document indexing.
// Connection settings are read from appsettings.json under "CrestApps:Search".
// ---------------------------------------------------------------------------
builder.Services
    .AddElasticsearchDataSourceServices(builder.Configuration.GetSection("CrestApps:Search:Elasticsearch"))
    .AddAzureAISearchDataSourceServices(builder.Configuration.GetSection("CrestApps:Search:AzureAISearch"));

// ---------------------------------------------------------------------------
// AI Tools — register custom tools that AI profiles can invoke.
// ---------------------------------------------------------------------------
builder.Services.AddAITool<CalculatorTool>(CalculatorTool.TheName)
    .WithTitle("Calculator")
    .WithDescription("Performs basic arithmetic: add, subtract, multiply, or divide two numbers.")
    .WithCategory("Utilities")
    .Selectable();

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

app.MapHub<AIChatHub>("/hubs/ai-chat");
app.MapHub<ChatInteractionHub>("/hubs/chat-interaction");

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
