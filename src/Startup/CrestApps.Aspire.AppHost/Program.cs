var builder = DistributedApplication.CreateBuilder(args);

const string ollamaModelName = "deepseek-v2:16b";

var ollama = builder.AddOllama("Ollama")
    .WithDataVolume()
    .WithGPUSupport()
    .WithHttpEndpoint(port: 11434, targetPort: 11434, name: "HttpOllama");

ollama.AddModel(ollamaModelName);

// var password = builder.AddParameter("Password", secret: true);

// var elasticsearch = builder.AddElasticsearch("Elasticsearch", password)
//     .WithDataVolume()
//     .WithEndpoint(9200, 9200);

var redis = builder.AddRedis("Redis");

var asterisk = builder.AddContainer("Asterisk", "andrius/asterisk", "latest")
    .WithHttpEndpoint(port: 8088, targetPort: 8088, name: "HttpAsterisk")
    .WithBindMount("Asterisk/http.conf", "/etc/asterisk/http.conf", isReadOnly: true)
    .WithBindMount("Asterisk/ari.conf", "/etc/asterisk/ari.conf", isReadOnly: true)
    .WithBindMount("Asterisk/extensions.conf", "/etc/asterisk/extensions.conf", isReadOnly: true);

var orchardCore = builder.AddProject<Projects.CrestApps_OrchardCore_Cms_Web>("OrchardCoreCMS")
// .WithReference(redis)
// .WithReference(ollama)
// .WaitFor(redis)
    .WaitFor(asterisk)
    .WithHttpsEndpoint(5001, name: "HttpsOrchardCore")
    .WithEnvironment((options) =>
    {
        // Configure the Redis connection.
        options.EnvironmentVariables.Add("OrchardCore__OrchardCore_Redis__Configuration", "localhost,allowAdmin=true");

        // Configure the Elasticsearch connection.
        // options.EnvironmentVariables.Add("OrchardCore__OrchardCore_Elasticsearch__ConnectionType", "SingleNodeConnectionPool");
        // options.EnvironmentVariables.Add("OrchardCore__OrchardCore_Elasticsearch__Url", "http://localhost");
        // options.EnvironmentVariables.Add("OrchardCore__OrchardCore_Elasticsearch__Username", "elastic");
        // options.EnvironmentVariables.Add("OrchardCore__OrchardCore_Elasticsearch__Ports__0", "9200");

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

        // Configure the configuration-backed default Asterisk telephony provider.
        options.EnvironmentVariables.Add("OrchardCore__CrestApps__Asterisk__Default__BaseUrl", "http://localhost:8088/ari/");
        options.EnvironmentVariables.Add("OrchardCore__CrestApps__Asterisk__Default__UserName", "crestapps");
        options.EnvironmentVariables.Add("OrchardCore__CrestApps__Asterisk__Default__Password", "crestapps-dev");
        options.EnvironmentVariables.Add("OrchardCore__CrestApps__Asterisk__Default__ApplicationName", "crestapps-telephony");
        options.EnvironmentVariables.Add("OrchardCore__CrestApps__Asterisk__Default__EndpointTemplate", "Local/{number}@default");
        options.EnvironmentVariables.Add("OrchardCore__CrestApps__Asterisk__Default__TimeoutSeconds", "30");
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
    .WithEnvironment("AsteriskWeb__OrchardBaseUrl", "https://localhost:5001")
    .WithEnvironment("AsteriskWeb__LoginPath", "/Login")
    .WithEnvironment("AsteriskWeb__InboundPath", "/api/contact-center/voice/inbound")
    .WithEnvironment("AsteriskWeb__ProviderName", "Default Asterisk")
    .WithEnvironment("AsteriskWeb__AsteriskDestination", "1000")
    .WithEnvironment("AsteriskWeb__AsteriskBaseUrl", "http://localhost:8088/ari/")
    .WithEnvironment("AsteriskWeb__AsteriskEndpointTemplate", "Local/{number}@default")
    .WithEnvironment("AsteriskWeb__AsteriskApplicationName", "crestapps-dashboard")
    .WithEnvironment("AsteriskWeb__AsteriskTimeoutSeconds", "30")
    .WithEnvironment("AsteriskWeb__SimulationTimeoutSeconds", "45")
    .WithEnvironment("AsteriskWeb__AsteriskUserName", "crestapps")
    .WithEnvironment("AsteriskWeb__AsteriskPassword", "crestapps-dev");

var app = builder.Build();

await app.RunAsync();
