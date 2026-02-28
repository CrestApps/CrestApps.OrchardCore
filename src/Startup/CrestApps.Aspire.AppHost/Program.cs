var builder = DistributedApplication.CreateBuilder(args);

const string ollamaModelName = "deepseek-v2:16b";

var ollama = builder.AddOllama("Ollama")
    .WithDataVolume()
    .WithGPUSupport()
    .WithHttpEndpoint(port: 11434, targetPort: 11434, name: "HttpOllama");

ollama.AddModel(ollamaModelName);

var password = builder.AddParameter("Password", secret: true);

// var elasticsearch = builder.AddElasticsearch("Elasticsearch", password)
//     .WithDataVolume()
//     .WithEndpoint(9200, 9200);

var redis = builder.AddRedis("Redis");

var orchardCore = builder.AddProject<Projects.CrestApps_OrchardCore_Cms_Web>("OrchardCoreCMS")
    // .WithReference(redis)
    // .WithReference(ollama)
    // .WaitFor(redis)
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

        // Configure the AI connection.
        options.EnvironmentVariables.Add("OrchardCore__CrestApps_AI__Providers__Ollama__DefaultConnectionName", "Default");
        options.EnvironmentVariables.Add("OrchardCore__CrestApps_AI__Providers__Ollama__DefaultDeploymentName", ollamaModelName);

        // Here we are using a connection names 'Default', you can also add other connections if needed.
        options.EnvironmentVariables.Add("OrchardCore__CrestApps_AI__Providers__Ollama__Connections__Default__Endpoint", "http://localhost:11434");
        options.EnvironmentVariables.Add("OrchardCore__CrestApps_AI__Providers__Ollama__Connections__Default__ChatDeploymentName", ollamaModelName);

        // ── Copilot BYOK (API Key) configuration ──
        // Uncomment the following lines to configure the Copilot orchestrator with BYOK authentication.
        // This bypasses GitHub OAuth and uses your own API key from a model provider.
        //
        // options.EnvironmentVariables.Add("OrchardCore__CrestApps_OrchardCore_AI_Chat_Copilot__AuthenticationType", "ApiKey");
        // options.EnvironmentVariables.Add("OrchardCore__CrestApps_OrchardCore_AI_Chat_Copilot__ProviderType", "openai");
        // options.EnvironmentVariables.Add("OrchardCore__CrestApps_OrchardCore_AI_Chat_Copilot__BaseUrl", "http://localhost:11434/v1");
        // options.EnvironmentVariables.Add("OrchardCore__CrestApps_OrchardCore_AI_Chat_Copilot__DefaultModel", ollamaModelName);
        // options.EnvironmentVariables.Add("OrchardCore__CrestApps_OrchardCore_AI_Chat_Copilot__WireApi", "completions");
        //
        // For Azure AI Foundry:
        // options.EnvironmentVariables.Add("OrchardCore__CrestApps_OrchardCore_AI_Chat_Copilot__ProviderType", "azure");
        // options.EnvironmentVariables.Add("OrchardCore__CrestApps_OrchardCore_AI_Chat_Copilot__BaseUrl", "https://your-resource.openai.azure.com");
        // options.EnvironmentVariables.Add("OrchardCore__CrestApps_OrchardCore_AI_Chat_Copilot__ApiKey", "<your-api-key>");
        // options.EnvironmentVariables.Add("OrchardCore__CrestApps_OrchardCore_AI_Chat_Copilot__DefaultModel", "gpt-4o");
        // options.EnvironmentVariables.Add("OrchardCore__CrestApps_OrchardCore_AI_Chat_Copilot__AzureApiVersion", "2024-10-21");
    });

builder.AddProject<Projects.CrestApps_OrchardCore_Samples_McpClient>("McpClientSample")
    .WithReference(orchardCore)
    .WaitFor(orchardCore)
    .WithHttpsEndpoint(5002, name: "HttpsMcpClient")
    .WithEnvironment("Mcp__Endpoint", "https://localhost:5001/mcp/sse");

var app = builder.Build();

await app.RunAsync();
