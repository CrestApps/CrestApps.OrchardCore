var builder = DistributedApplication.CreateBuilder(args);
const string elasticsearchSecret = "CrestApps!2023";

const string ollamaModelName = "deepseek-v2:16b";

var ollama = builder.AddOllama("Ollama")
    .WithDataVolume()
    .WithGPUSupport()
    .WithHttpEndpoint(port: 11434, targetPort: 11434, name: "HttpOllama");

ollama.AddModel(ollamaModelName);

var password = builder.AddParameter("Password", elasticsearchSecret, secret: true);

var elasticsearch = builder.AddElasticsearch("Elasticsearch", password)
    .WithDataVolume()
    .WithEndpoint(9200, 9200);

var redis = builder.AddRedis("Redis");

var orchardCore = builder.AddProject<Projects.CrestApps_OrchardCore_Cms_Web>("OrchardCoreCMS")
    .WithReference(redis)
    .WithReference(ollama)
    .WaitFor(redis)
    .WithHttpsEndpoint(5001, name: "HttpsOrchardCore")
    .WithEnvironment((options) =>
    {
        // Configure the Redis connection.
        options.EnvironmentVariables.Add("OrchardCore__OrchardCore_Redis__Configuration", "localhost,allowAdmin=true");

        // Configure the Elasticsearch connection.
        options.EnvironmentVariables.Add("OrchardCore__OrchardCore_Elasticsearch__ConnectionType", "SingleNodeConnectionPool");
        options.EnvironmentVariables.Add("OrchardCore__OrchardCore_Elasticsearch__Url", "http://localhost");
        options.EnvironmentVariables.Add("OrchardCore__OrchardCore_Elasticsearch__Username", "elastic");
        options.EnvironmentVariables.Add("OrchardCore__OrchardCore_Elasticsearch__Password", elasticsearchSecret);
        options.EnvironmentVariables.Add("OrchardCore__OrchardCore_Elasticsearch__Ports__0", "9200");

        // Configure the AI connection.
        options.EnvironmentVariables.Add("OrchardCore__CrestApps_AI__Providers__Ollama__DefaultConnectionName", "Default");
        options.EnvironmentVariables.Add("OrchardCore__CrestApps_AI__Providers__Ollama__DefaultDeploymentName", ollamaModelName);

        // Here we are using a connection names 'Default', you can also add other connections if needed.
        options.EnvironmentVariables.Add("OrchardCore__CrestApps_AI__Providers__Ollama__Connections__Default__Endpoint", "http://localhost:11434");
        options.EnvironmentVariables.Add("OrchardCore__CrestApps_AI__Providers__Ollama__Connections__Default__DefaultDeploymentName", ollamaModelName);
    });

builder.AddProject<Projects.CrestApps_OrchardCore_Samples_McpClient>("McpClientSample")
    .WithReference(orchardCore)
    .WaitFor(orchardCore)
    .WithHttpsEndpoint(5002, name: "HttpsMcpClient")
    .WithEnvironment("Mcp__Endpoint", "https://localhost:5001/mcp/sse");

var app = builder.Build();

await app.RunAsync();
