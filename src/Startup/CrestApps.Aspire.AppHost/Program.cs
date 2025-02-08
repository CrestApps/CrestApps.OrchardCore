var builder = DistributedApplication.CreateBuilder(args);

const string OllamaModelName = "deepseek-v2:16b";

var ollama = builder.AddOllama("Ollama", 11434)
    .WithDataVolume()
    .WithGPUSupport();

ollama.AddModel(OllamaModelName);

var redis = builder.AddRedis("Redis");

builder.AddProject<Projects.CrestApps_OrchardCore_Cms_Web>("OrchardCoreCMS")
    .WithReference(redis)
    .WithReference(ollama)
    .WaitFor(ollama)
    .WaitFor(redis)
    .WithHttpsEndpoint(5001)
    .WithEnvironment((options) =>
    {
        // Configure the Redis connection.
        options.EnvironmentVariables.Add("OrchardCore__OrchardCore_Redis__Configuration", "localhost,allowAdmin=true");

        // Configure the AI connection.
        options.EnvironmentVariables.Add("OrchardCore__CrestApps_AI__Providers__Ollama__DefaultConnectionName", "Default");
        options.EnvironmentVariables.Add("OrchardCore__CrestApps_AI__Providers__Ollama__DefaultDeploymentName", OllamaModelName);

        // Here we are using a connection names 'Default', you can also add other connections if needed.
        options.EnvironmentVariables.Add("OrchardCore__CrestApps_AI__Providers__Ollama__Connections__Default__Endpoint", "http://localhost:11434/");
        options.EnvironmentVariables.Add("OrchardCore__CrestApps_AI__Providers__Ollama__Connections__Default__DefaultDeploymentName", OllamaModelName);
    });

var app = builder.Build();

await app.RunAsync();
