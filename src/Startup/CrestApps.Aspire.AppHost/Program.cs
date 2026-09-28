using System.Net.Sockets;

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

var asterisk = builder.AddContainer("Asterisk", "crestapps/asterisk-webrtc", "22.6.0-dev")
    .WithDockerfile("Asterisk")
    .WithHttpEndpoint(port: 8088, targetPort: 8088, name: "HttpAsterisk")
    .WithEndpoint(port: 8089, targetPort: 8089, scheme: "https", name: "WssAsterisk")
    .WithBindMount("Asterisk/http.conf", "/etc/asterisk/http.conf", isReadOnly: true)
    .WithBindMount("Asterisk/ari.conf", "/etc/asterisk/ari.conf", isReadOnly: true)
    .WithBindMount("Asterisk/extensions.conf", "/etc/asterisk/extensions.conf", isReadOnly: true)
    .WithBindMount("Asterisk/pjsip.conf", "/etc/asterisk/pjsip.conf", isReadOnly: true);

var coturn = builder.AddContainer("Coturn", "coturn/coturn", "4.6.3")
    .WithEndpoint(port: 3478, targetPort: 3478, scheme: "turn", name: "TurnTcp")
    .WithEndpoint(port: 3478, targetPort: 3478, scheme: "turn", name: "TurnUdp", protocol: ProtocolType.Udp)
    .WithEndpoint(port: 5349, targetPort: 5349, scheme: "turns", name: "TurnsTcp")
    .WithEndpoint(port: 5349, targetPort: 5349, scheme: "turns", name: "TurnsUdp", protocol: ProtocolType.Udp)
    // Aspire 13.4.6 builds dashboard URL snapshots by matching each resource URL to a single DCP endpoint
    // (ResourceSnapshotBuilder.GetUrls uses SingleOrDefault). Coturn publishes the same port over both TCP and
    // UDP, which resolves to more than one endpoint and throws, killing the DCP watch tasks and preventing every
    // resource in the app host from starting. Clearing the generated URLs skips that lookup. The endpoints are
    // still published, so TURN keeps working; only the clickable dashboard links for Coturn are omitted.
    .WithUrls(context => context.Urls.Clear())
    .WithBindMount("Coturn/turnserver.conf", "/etc/coturn/turnserver.conf", isReadOnly: true);

var elasticsearchEndpoint = elasticsearch.Resource.PrimaryEndpoint;

var orchardCore = builder.AddProject<Projects.CrestApps_OrchardCore_Cms_Web>("OrchardCoreCMS")
    // Injects the ConnectionStrings__vectordb, ConnectionStrings__PostgreSQL and
    // ConnectionStrings__Elasticsearch environment variables.
    .WithReference(vectorStore)
    .WithReference(postgres)
    .WithReference(elasticsearch)
    .WaitFor(vectorStore)
    .WaitFor(elasticsearch)
    .WithReference(redis)
// .WithReference(ollama)
    .WaitFor(redis)
    .WaitFor(asterisk)
    .WaitFor(coturn)
    .WithHttpsEndpoint(5001, name: "HttpsOrchardCore")
    .WithEnvironment("OrchardCore__OrchardCore_Redis__Configuration", ReferenceExpression.Create($"{redis.Resource.ConnectionStringExpression},allowAdmin=true"))
    .WithEnvironment((options) =>
    {
        // The Redis connection is configured above from the Redis resource itself, so it is not set here.

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
        //
        // The slot is deliberately not 0. Orchard Core appends the environment variable provider after
        // the tenant's App_Data/appsettings.json, and these keys address array positions, so writing to
        // slot 0 does not add a connection beside the developer's own: it overwrites whichever one they
        // happen to have listed first. Its name and client silently become this Ollama pair while their
        // endpoint and key stay behind, and every deployment pointing at the original name then fails to
        // resolve. Appending past the end leaves their connections untouched.

        options.EnvironmentVariables.Add($"OrchardCore__CrestApps__AI__Connections__90__Name", "Default");
        options.EnvironmentVariables.Add($"OrchardCore__CrestApps__AI__Connections__90__ClientName", "Ollama");
        options.EnvironmentVariables.Add($"OrchardCore__CrestApps__AI__Connections__90__Endpoint", "http://localhost:11434");
        options.EnvironmentVariables.Add($"OrchardCore__CrestApps__AI__Connections__90__ChatDeploymentName", ollamaModelName);

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

        // Configure the configuration-backed default Asterisk telephony provider.
        // The Cms.Web launch profile also injects these keys, and Aspire applies the launch profile
        // before this callback runs, so assign by indexer instead of Add to avoid a duplicate-key crash.
        options.EnvironmentVariables["OrchardCore__CrestApps__Asterisk__Default__BaseUrl"] = "http://localhost:8088/ari/";
        options.EnvironmentVariables["OrchardCore__CrestApps__Asterisk__Default__UserName"] = "crestapps";
        options.EnvironmentVariables["OrchardCore__CrestApps__Asterisk__Default__Password"] = "crestapps-dev";
        options.EnvironmentVariables["OrchardCore__CrestApps__Asterisk__Default__ApplicationName"] = "crestapps-telephony";
        options.EnvironmentVariables["OrchardCore__CrestApps__Asterisk__Default__EndpointTemplate"] = "Local/{number}@default";
        options.EnvironmentVariables["OrchardCore__CrestApps__Asterisk__Default__TimeoutSeconds"] = "30";
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

builder.AddProject<Projects.CrestApps_OrchardCore_Asterisk_Web>("AsteriskWeb")
    .WithReference(orchardCore)
    .WaitFor(orchardCore)
    .WaitFor(asterisk)
    .WithHttpsEndpoint(5004, name: "HttpsAsteriskWeb")
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
    .WithEnvironment("AsteriskWeb__OrchardBaseUrl", "https://localhost:5001")
    .WithEnvironment("AsteriskWeb__LoginPath", "/Login")
    .WithEnvironment("AsteriskWeb__InboundPath", "/api/contact-center/voice/inbound")
    .WithEnvironment("AsteriskWeb__ProviderName", "Default Asterisk")
    .WithEnvironment("AsteriskWeb__AsteriskDestination", "1000")
    .WithEnvironment("AsteriskWeb__TwoPartyEndpointA", "Local/2001@crestapps-simulation")
    .WithEnvironment("AsteriskWeb__TwoPartyCallerIdA", "Simulation Party A <2001>")
    .WithEnvironment("AsteriskWeb__TwoPartyEndpointB", "Local/2002@crestapps-simulation")
    .WithEnvironment("AsteriskWeb__TwoPartyCallerIdB", "Simulation Party B <2002>")
    .WithEnvironment("AsteriskWeb__AsteriskBaseUrl", "http://localhost:8088/ari/")
    .WithEnvironment("AsteriskWeb__AsteriskEndpointTemplate", "Local/{number}@default")
    .WithEnvironment("AsteriskWeb__AsteriskApplicationName", "crestapps-dashboard")
    .WithEnvironment("AsteriskWeb__AsteriskTimeoutSeconds", "30")
    .WithEnvironment("AsteriskWeb__SimulationTimeoutSeconds", "45")
    .WithEnvironment("AsteriskWeb__AsteriskUserName", "crestapps")
    .WithEnvironment("AsteriskWeb__AsteriskPassword", "crestapps-dev");

var app = builder.Build();

await app.RunAsync();
