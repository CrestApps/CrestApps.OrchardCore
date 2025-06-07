var builder = DistributedApplication.CreateBuilder(args);
const string elasticsearchSecret = "crestapps123";

const string OllamaModelName = "deepseek-v2:16b";

var ollama = builder.AddOllama("Ollama")
    .WithDataVolume()
    .WithGPUSupport();

ollama.AddModel(OllamaModelName);

var password = builder.AddParameter("Password", elasticsearchSecret, secret: true);

var elasticsearch = builder.AddElasticsearch("Elasticsearch", password)
    .WithDataVolume();

var redis = builder.AddRedis("Redis");

builder.AddProject<Projects.CrestApps_OrchardCore_Cms_Web>("OrchardCoreCMS")
    .WithReference(redis)
    .WithReference(ollama)
    .WaitFor(redis)
    .WithHttpsEndpoint(5001)
    .WithEnvironment((options) =>
    {
        // Configure the Redis connection.
        options.EnvironmentVariables.Add("OrchardCore__OrchardCore_Redis__Configuration", "localhost,allowAdmin=true");

        var elasticsearchEndpoint = elasticsearch.GetEndpoint("http");

        // Configure the Elasticsearch connection.
        options.EnvironmentVariables.Add("OrchardCore__OrchardCore_Elasticsearch__ConnectionType", "SingleNodeConnectionPool");
        options.EnvironmentVariables.Add("OrchardCore__OrchardCore_Elasticsearch__Url", elasticsearchEndpoint.Url);
        options.EnvironmentVariables.Add("OrchardCore__OrchardCore_Elasticsearch__Username", "elastic");
        options.EnvironmentVariables.Add("OrchardCore__OrchardCore_Elasticsearch__Password", elasticsearchSecret);
        if (elasticsearchEndpoint.TargetPort.HasValue)
        {
            options.EnvironmentVariables.Add("OrchardCore__OrchardCore_Elasticsearch__Ports__0", elasticsearchEndpoint.TargetPort);
        }

        // Configure the AI connection.
        options.EnvironmentVariables.Add("OrchardCore__CrestApps_AI__Providers__Ollama__DefaultConnectionName", "Default");
        options.EnvironmentVariables.Add("OrchardCore__CrestApps_AI__Providers__Ollama__DefaultDeploymentName", OllamaModelName);

        // Here we are using a connection names 'Default', you can also add other connections if needed.
        options.EnvironmentVariables.Add("OrchardCore__CrestApps_AI__Providers__Ollama__Connections__Default__Endpoint", ollama.GetEndpoint("http").Url);
        options.EnvironmentVariables.Add("OrchardCore__CrestApps_AI__Providers__Ollama__Connections__Default__DefaultDeploymentName", OllamaModelName);
    });

var app = builder.Build();

await app.RunAsync();
