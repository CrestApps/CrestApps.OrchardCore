using CrestApps.AI;
using CrestApps.AI.Deployments;
using CrestApps.AI.Models;
using CrestApps.AI.Services;
using CrestApps.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.Tests.Framework.Mvc;

public sealed class ConfigurationAIDeploymentCatalogTests
{
    [Fact]
    public async Task GetAllAsync_ShouldMergeStoredAndConfiguredStandaloneDeployments()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                ["CrestApps:AI:Deployments:0:ClientName"] = "AzureSpeech",
                ["CrestApps:AI:Deployments:0:Name"] = "whisper",
                ["CrestApps:AI:Deployments:0:Type"] = "SpeechToText",
                ["CrestApps:AI:Deployments:0:IsDefault"] = "true",
                ["CrestApps:AI:Deployments:0:Endpoint"] = "https://eastus.stt.speech.microsoft.com",
                ["CrestApps:AI:Deployments:0:AuthenticationType"] = "ApiKey",
                ["CrestApps:AI:Deployments:0:ApiKey"] = "secret",
            })
            .Build();

        var aiOptions = new AIOptions();
        aiOptions.AddDeploymentProvider("AzureSpeech", entry => entry.SupportsContainedConnection = true);

        var innerStore = new TestAIDeploymentStore(
        [
            new AIDeployment
            {
                ItemId = "ui-deployment",
                Name = "ui-chat",
                ClientName = "OpenAI",
                Type = AIDeploymentType.Chat,
            },
        ]);

        var catalog = new ConfigurationAIDeploymentCatalog(
            innerStore,
            configuration,
            Options.Create(new AIProviderOptions()),
            Options.Create(aiOptions),
            NullLogger<ConfigurationAIDeploymentCatalog>.Instance);

        var deployments = await catalog.GetAllAsync();

        Assert.Contains(deployments, deployment => deployment.ItemId == "ui-deployment");

        var configuredDeployment = Assert.Single(deployments, deployment => deployment.Name == "whisper");
        Assert.Equal("AzureSpeech", configuredDeployment.ClientName);
        Assert.True(configuredDeployment.IsDefault);
        Assert.Equal(AIDeploymentType.SpeechToText, configuredDeployment.Type);
        Assert.NotNull(configuredDeployment.Properties);
        Assert.Equal("https://eastus.stt.speech.microsoft.com", configuredDeployment.Properties["Endpoint"]?.ToString());
    }

    [Fact]
    public async Task FindByNameAsync_ShouldReturnConfiguredDeploymentWhenNotInStore()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                ["CrestApps:AI:Deployments:0:ClientName"] = "AzureSpeech",
                ["CrestApps:AI:Deployments:0:Name"] = "AzureTextToSpeech",
                ["CrestApps:AI:Deployments:0:Type"] = "TextToSpeech",
                ["CrestApps:AI:Deployments:0:IsDefault"] = "true",
            })
            .Build();

        var aiOptions = new AIOptions();
        aiOptions.AddDeploymentProvider("AzureSpeech", entry => entry.SupportsContainedConnection = true);

        var catalog = new ConfigurationAIDeploymentCatalog(
            new TestAIDeploymentStore([]),
            configuration,
            Options.Create(new AIProviderOptions()),
            Options.Create(aiOptions),
            NullLogger<ConfigurationAIDeploymentCatalog>.Instance);

        var deployment = await catalog.FindByNameAsync("AzureTextToSpeech");

        Assert.NotNull(deployment);
        Assert.Equal("AzureSpeech", deployment.ClientName);
        Assert.Equal(AIDeploymentType.TextToSpeech, deployment.Type);
    }

    [Fact]
    public async Task GetAllAsync_ShouldReadProviderGroupedStandaloneDeployments()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                ["CrestApps:AI:Deployments:AzureSpeech:0:Name"] = "grouped-whisper",
                ["CrestApps:AI:Deployments:AzureSpeech:0:Type"] = "SpeechToText",
                ["CrestApps:AI:Deployments:AzureSpeech:0:IsDefault"] = "true",
            })
            .Build();

        var aiOptions = new AIOptions();
        aiOptions.AddDeploymentProvider("AzureSpeech", entry => entry.SupportsContainedConnection = true);

        var catalog = new ConfigurationAIDeploymentCatalog(
            new TestAIDeploymentStore([]),
            configuration,
            Options.Create(new AIProviderOptions()),
            Options.Create(aiOptions),
            NullLogger<ConfigurationAIDeploymentCatalog>.Instance);

        var deployment = Assert.Single(await catalog.GetAllAsync());

        Assert.Equal("AzureSpeech", deployment.ClientName);
        Assert.Equal("grouped-whisper", deployment.Name);
        Assert.Equal(AIDeploymentType.SpeechToText, deployment.Type);
    }

    [Fact]
    public async Task GetAllAsync_ShouldNormalizeAzureOpenAIStandaloneDeployments()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                ["CrestApps:AI:Deployments:0:ClientName"] = "AzureOpenAI",
                ["CrestApps:AI:Deployments:0:Name"] = "text-embedding-3-small",
                ["CrestApps:AI:Deployments:0:ModelName"] = "text-embedding-3-small",
                ["CrestApps:AI:Deployments:0:Type"] = "Embedding",
                ["CrestApps:AI:Deployments:0:Endpoint"] = "https://example.openai.azure.com/",
                ["CrestApps:AI:Deployments:0:AuthenticationType"] = "ApiKey",
                ["CrestApps:AI:Deployments:0:ApiKey"] = "secret",
            })
            .Build();

        var aiOptions = new AIOptions();
        aiOptions.AddDeploymentProvider("Azure", entry => entry.SupportsContainedConnection = true);

        var catalog = new ConfigurationAIDeploymentCatalog(
            new TestAIDeploymentStore([]),
            configuration,
            Options.Create(new AIProviderOptions()),
            Options.Create(aiOptions),
            NullLogger<ConfigurationAIDeploymentCatalog>.Instance);

        var deployment = Assert.Single(await catalog.GetAllAsync());

        Assert.Equal("Azure", deployment.ClientName);
        Assert.Equal(AIDeploymentType.Embedding, deployment.Type);
    }

    private sealed class TestAIDeploymentStore(List<AIDeployment> deployments) : IAIDeploymentStore
    {
        public ValueTask CreateAsync(AIDeployment entry)
        {
            deployments.Add(entry);
            return ValueTask.CompletedTask;
        }

        public ValueTask<bool> DeleteAsync(AIDeployment entry)
        {
            deployments.Remove(entry);
            return ValueTask.FromResult(true);
        }

        public ValueTask<AIDeployment> FindByIdAsync(string id)
            => ValueTask.FromResult(deployments.FirstOrDefault(deployment => deployment.ItemId == id));

        public ValueTask<AIDeployment> FindByNameAsync(string name)
            => ValueTask.FromResult(deployments.FirstOrDefault(deployment => deployment.Name == name));

        public ValueTask<IReadOnlyCollection<AIDeployment>> GetAllAsync()
            => ValueTask.FromResult<IReadOnlyCollection<AIDeployment>>(deployments.ToArray());

        public ValueTask<IReadOnlyCollection<AIDeployment>> GetAsync(IEnumerable<string> ids)
            => ValueTask.FromResult<IReadOnlyCollection<AIDeployment>>(deployments.Where(deployment => ids.Contains(deployment.ItemId)).ToArray());

        public ValueTask<IReadOnlyCollection<AIDeployment>> GetAsync(string source)
            => ValueTask.FromResult<IReadOnlyCollection<AIDeployment>>(deployments.Where(deployment => deployment.Source == source).ToArray());

        public ValueTask<AIDeployment> GetAsync(string name, string source)
            => ValueTask.FromResult(deployments.FirstOrDefault(deployment => deployment.Name == name && deployment.Source == source));

        public ValueTask<PageResult<AIDeployment>> PageAsync<TQuery>(int page, int pageSize, TQuery context)
            where TQuery : QueryContext
            => ValueTask.FromResult(new PageResult<AIDeployment>
            {
                Count = deployments.Count,
                Entries = deployments.ToArray(),
            });

        public ValueTask SaveChangesAsync() => ValueTask.CompletedTask;

        public ValueTask UpdateAsync(AIDeployment entry) => ValueTask.CompletedTask;
    }
}
