var builder = DistributedApplication.CreateBuilder(args);

const string ollamaModelName = "deepseek-v2:16b";

var ollama = builder.AddOllama("Ollama")
    .WithDataVolume()
    .WithGPUSupport()
    .WithHttpEndpoint(port: 11434, targetPort: 11434, name: "HttpOllama");

ollama.AddModel(ollamaModelName);

// PostgreSQL, running the pgvector image, provides a local vector store for AI data sources and
// indexes. The image and the database name match the CrestApps.Core Aspire host.
// The credentials are declared as parameters so they can be overridden from user secrets,
// appsettings or the Parameters__PostgresUser / Parameters__PostgresPassword environment variables.
var postgresUser = builder.AddParameter("PostgresUser", "postgres");
var postgresPassword = builder.AddParameter("PostgresPassword", "postgres", secret: true);

// Containers keep their data under the CMS App_Data folder rather than in Docker volumes, so
// sharing App_Data with another developer shares the vector store and the indexes with it.
var appDataPath = Path.GetFullPath(Path.Combine(
    builder.AppHostDirectory,
    "..",
    "CrestApps.OrchardCore.Cms.Web",
    "App_Data"));

// A container only mounts a directory that already exists, so each data folder is created up front.
string CreateDataPath(string name)
{
    var path = Path.Combine(appDataPath, name);

    Directory.CreateDirectory(path);

    return path;
}

var postgres = builder.AddPostgres("PostgreSQL", postgresUser, postgresPassword, port: 5432)
    .WithImage("pgvector/pgvector", "pg16")
    .WithDataBindMount(CreateDataPath("PostgreSQL"));

// Uncomment to browse the vector store from the Aspire dashboard.
// postgres.WithPgAdmin();

// The database that holds the locally stored vectors. The resource name is also the
// connection string name, so it surfaces as the ConnectionStrings__vectordb variable.
var vectorStore = postgres.AddDatabase("vectordb");

// Elasticsearch backs the Orchard Core search indexes and the Elasticsearch AI data sources.
// 'elastic' is the built-in superuser, so only the password is a parameter.
var elasticsearchPassword = builder.AddParameter("ElasticsearchPassword", "elasticsearch", secret: true);

// The port argument of AddElasticsearch is applied to the internal transport endpoint, so the HTTP
// endpoint is pinned separately to keep Elasticsearch on its usual port for external tools.
var elasticsearch = builder.AddElasticsearch("Elasticsearch", elasticsearchPassword)
    .WithEndpoint("http", endpoint => endpoint.Port = 9200)
    .WithDataBindMount(CreateDataPath("Elasticsearch"));

var redis = builder.AddRedis("Redis");

var elasticsearchEndpoint = elasticsearch.Resource.PrimaryEndpoint;

var orchardCore = builder.AddProject<Projects.CrestApps_OrchardCore_Cms_Web>("OrchardCoreCMS")
    // Injects the ConnectionStrings__vectordb, ConnectionStrings__PostgreSQL and
    // ConnectionStrings__Elasticsearch environment variables.
    .WithReference(vectorStore)
    .WithReference(postgres)
    .WithReference(elasticsearch)
    .WaitFor(vectorStore)
    .WaitFor(elasticsearch)
// .WithReference(redis)
// .WithReference(ollama)
// .WaitFor(redis)
    .WithHttpsEndpoint(5001, name: "HttpsOrchardCore")
    .WithEnvironment((options) =>
    {
        // Configure the Redis connection.
        options.EnvironmentVariables.Add("OrchardCore__OrchardCore_Redis__Configuration", "localhost,allowAdmin=true");

        // Configure the PostgreSQL connection shared by every PostgreSQL feature, such as the
        // connection PostgreSQL AI data sources use when they do not define their own. It is read
        // from the OrchardCore:CrestApps:PostgreSQL configuration section.
        options.EnvironmentVariables["OrchardCore__CrestApps__PostgreSQL__ConnectionString"] = vectorStore.Resource.ConnectionStringExpression;

        // Configure the Elasticsearch connection shared by every Elasticsearch feature, such as the
        // connection Elasticsearch AI data sources use when they do not define their own. It is read
        // from the OrchardCore:CrestApps:Elasticsearch configuration section.
        options.EnvironmentVariables["OrchardCore__CrestApps__Elasticsearch__Url"] = elasticsearchEndpoint.Property(EndpointProperty.Url);
        options.EnvironmentVariables["OrchardCore__CrestApps__Elasticsearch__AuthenticationType"] = "Basic";
        options.EnvironmentVariables["OrchardCore__CrestApps__Elasticsearch__Username"] = "elastic";
        options.EnvironmentVariables["OrchardCore__CrestApps__Elasticsearch__Password"] = elasticsearchPassword.Resource;

        // Configure the Orchard Core Elasticsearch feature, which keeps the host and the ports apart.
        options.EnvironmentVariables["OrchardCore__OrchardCore_Elasticsearch__ConnectionType"] = "SingleNodeConnectionPool";
        options.EnvironmentVariables["OrchardCore__OrchardCore_Elasticsearch__Url"] = ReferenceExpression.Create($"http://{elasticsearchEndpoint.Property(EndpointProperty.Host)}");
        options.EnvironmentVariables["OrchardCore__OrchardCore_Elasticsearch__Ports__0"] = elasticsearchEndpoint.Property(EndpointProperty.Port);
        options.EnvironmentVariables["OrchardCore__OrchardCore_Elasticsearch__AuthenticationType"] = "Basic";
        options.EnvironmentVariables["OrchardCore__OrchardCore_Elasticsearch__Username"] = "elastic";
        options.EnvironmentVariables["OrchardCore__OrchardCore_Elasticsearch__Password"] = elasticsearchPassword.Resource;

        // Configure the AI connection using the flat connections format.
        options.EnvironmentVariables.Add("OrchardCore__CrestApps__AI__Connections__0__Name", "Default");
        options.EnvironmentVariables.Add("OrchardCore__CrestApps__AI__Connections__0__ClientName", "Ollama");
        options.EnvironmentVariables.Add("OrchardCore__CrestApps__AI__Connections__0__Endpoint", "http://localhost:11434");
        options.EnvironmentVariables.Add("OrchardCore__CrestApps__AI__Connections__0__ChatDeploymentName", ollamaModelName);

        // Uncomment the following lines to configure the Copilot orchestrator with BYOK authentication.
        // This bypasses GitHub OAuth and uses your own API key from a model provider.
        //
        // options.EnvironmentVariables.Add("OrchardCore__CrestApps__AI__Copilot__AuthenticationType", "ApiKey");
        // options.EnvironmentVariables.Add("OrchardCore__CrestApps__AI__Copilot__ProviderType", "openai");
        // options.EnvironmentVariables.Add("OrchardCore__CrestApps__AI__Copilot__BaseUrl", "http://localhost:11434/v1");
        // options.EnvironmentVariables.Add("OrchardCore__CrestApps__AI__Copilot__DefaultModel", ollamaModelName);
        // options.EnvironmentVariables.Add("OrchardCore__CrestApps__AI__Copilot__WireApi", "completions");
        //
        // For Azure AI Foundry:
        // options.EnvironmentVariables.Add("OrchardCore__CrestApps__AI__Copilot__ProviderType", "azure");
        // options.EnvironmentVariables.Add("OrchardCore__CrestApps__AI__Copilot__BaseUrl", "https://your-resource.openai.azure.com");
        // options.EnvironmentVariables.Add("OrchardCore__CrestApps__AI__Copilot__ApiKey", "<your-api-key>");
        // options.EnvironmentVariables.Add("OrchardCore__CrestApps__AI__Copilot__DefaultModel", "gpt-4o");
        // options.EnvironmentVariables.Add("OrchardCore__CrestApps__AI__Copilot__AzureApiVersion", "2024-10-21");

        // Disable auth so local clients can connect to the host endpoints during development.
        options.EnvironmentVariables.Add("OrchardCore__CrestApps__AI__McpServer__AuthenticationType", "None");
        options.EnvironmentVariables.Add("OrchardCore__CrestApps__AI__A2AHost__AuthenticationType", "None");
        options.EnvironmentVariables.Add("OrchardCore__CrestApps__AI__A2AHost__ExposeAgentsAsSkill", "false");
    });

builder.AddProject<Projects.CrestApps_OrchardCore_Samples_McpClient>("McpClientSample")
    .WithReference(orchardCore)
    .WaitFor(orchardCore)
    .WithHttpsEndpoint(5002, name: "HttpsMcpClient")
    .WithEnvironment("Mcp__Endpoint", "https://localhost:5001/mcp");

builder.AddProject<Projects.CrestApps_OrchardCore_Samples_A2AClient>("A2AClientSample")
    .WithReference(orchardCore)
    .WaitFor(orchardCore)
    .WithHttpsEndpoint(5003, name: "HttpsA2AClient")
    .WithEnvironment("A2A__Endpoint", "https://localhost:5001");

var app = builder.Build();

await app.RunAsync();
