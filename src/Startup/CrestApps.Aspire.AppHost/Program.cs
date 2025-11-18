using Aspire.Hosting.Azure;
using Microsoft.Extensions.Configuration;

var builder = DistributedApplication.CreateBuilder(args);

const string elasticsearchSecret = "CrestApps!2023";
const string ollamaModelName = "deepseek-v2:16b";
var foundryModelName = AIFoundryModel.Microsoft.Phi4MiniInstruct;
const string foundryModelId = "Phi-3.5-mini-instruct-cuda-gpu:1";

var enableOllama = builder.Configuration.GetValue("Aspire:EnableOllama", true);
var enableFoundry = builder.Configuration.GetValue("Aspire:EnableFoundry", true); ;

// ----------------------------
// Ollama setup
// ----------------------------
IResourceBuilder<OllamaResource> ollama = null;

if (enableOllama)
{
    ollama = builder
        .AddOllama("Ollama")
        .WithDataVolume()
        .WithGPUSupport();

    ollama.AddModel(ollamaModelName);

    // Allocate a custom HTTP endpoint for Ollama
    ollama.WithHttpEndpoint(port: 11434, targetPort: 11434, name: "HttpOllama");
}

// ----------------------------
// Elasticsearch setup
// ----------------------------
var password = builder.AddParameter("Password", elasticsearchSecret, secret: true);

var elasticsearch = builder.AddElasticsearch("Elasticsearch", password)
    .WithDataVolume()
    .WithEndpoint(9200, 9200);

// ----------------------------
// Redis setup
// ----------------------------
var redis = builder.AddRedis("Redis");

// ----------------------------
// OrchardCoreCMS project
// ----------------------------
var resources = builder.AddProject<Projects.CrestApps_OrchardCore_Cms_Web>("OrchardCoreCMS");

// ----------------------------
// Foundry setup
// ----------------------------
IResourceBuilder<AzureAIFoundryResource> foundry = null;

if (enableFoundry)
{
    foundry = builder
        .AddAzureAIFoundry("Foundry")
        .WithEndpoint(63455)
        .RunAsFoundryLocal();

    var chat = foundry.AddDeployment("chat", foundryModelName);

    resources
        .WithReference(foundry)
        .WaitFor(chat);
}

// ----------------------------
// Wire resources to OrchardCoreCMS
// ----------------------------
resources
    .WithReference(redis)
    .WithReference(ollama)
    .WithReference(elasticsearch)
    .WaitFor(redis)
    .WithHttpsEndpoint(5001, name: "HttpsOrchardCore")
    .WithEnvironment(options =>
    {
        // Redis
        options.EnvironmentVariables.Add("OrchardCore__OrchardCore_Redis__Configuration", "localhost,allowAdmin=true");

        // Elasticsearch
        options.EnvironmentVariables.Add("OrchardCore__OrchardCore_Elasticsearch__ConnectionType", "SingleNodeConnectionPool");
        options.EnvironmentVariables.Add("OrchardCore__OrchardCore_Elasticsearch__Url", "http://localhost");
        options.EnvironmentVariables.Add("OrchardCore__OrchardCore_Elasticsearch__Username", "elastic");
        options.EnvironmentVariables.Add("OrchardCore__OrchardCore_Elasticsearch__Password", elasticsearchSecret);
        options.EnvironmentVariables.Add("OrchardCore__OrchardCore_Elasticsearch__Ports__0", "9200");

        if (foundry is not null)
        {
            // Foundry
            options.EnvironmentVariables.Add("OrchardCore__CrestApps_AI__Providers__OpenAI__DefaultConnectionName", "FoundryLocal");
            options.EnvironmentVariables.Add("OrchardCore__CrestApps_AI__Providers__OpenAI__DefaultDeploymentName", foundryModelId);
            options.EnvironmentVariables.Add("OrchardCore__CrestApps_AI__Providers__OpenAI__Connections__FoundryLocal__Endpoint", "http://localhost:63455");
            options.EnvironmentVariables.Add("OrchardCore__CrestApps_AI__Providers__OpenAI__Connections__FoundryLocal__DefaultDeploymentName", foundryModelId);
        }

        if (ollama is not null)
        {
            // Ollama
            options.EnvironmentVariables.Add("OrchardCore__CrestApps_AI__Providers__Ollama__DefaultConnectionName", "Default");
            options.EnvironmentVariables.Add("OrchardCore__CrestApps_AI__Providers__Ollama__DefaultDeploymentName", ollamaModelName);
            options.EnvironmentVariables.Add("OrchardCore__CrestApps_AI__Providers__Ollama__Connections__Default__Endpoint", "http://localhost:11434");
            options.EnvironmentVariables.Add("OrchardCore__CrestApps_AI__Providers__Ollama__Connections__Default__DefaultDeploymentName", ollamaModelName);
        }
    });

// ----------------------------
// Build and run
// ----------------------------
var app = builder.Build();

await app.RunAsync();
