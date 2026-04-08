using CrestApps.Core.AI;
using CrestApps.Core.AI.Clients;
using CrestApps.Core.AI.Completions;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Orchestration;
using CrestApps.Core.AI.Speech;
using CrestApps.Core.AI.Tooling;
using CrestApps.Core.Templates.Models;
using CrestApps.Core.Templates.Services;

using Microsoft.Extensions.DependencyInjection;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace CrestApps.OrchardCore.Tests.Core.Orchestration;

public sealed class DefaultOrchestratorResolverTests
{

    [Fact]
    public void Resolve_NullName_ReturnsDefaultOrchestrator()
    {

        var resolver = CreateResolver();

        var orchestrator = resolver.Resolve(null);

        Assert.NotNull(orchestrator);
        Assert.IsType<DefaultOrchestrator>(orchestrator);

    }

    [Fact]
    public void Resolve_EmptyName_ReturnsDefaultOrchestrator()
    {

        var resolver = CreateResolver();

        var orchestrator = resolver.Resolve("");

        Assert.NotNull(orchestrator);
        Assert.IsType<DefaultOrchestrator>(orchestrator);

    }

    [Fact]
    public void Resolve_DefaultName_ReturnsDefaultOrchestrator()
    {

        var resolver = CreateResolver();

        var orchestrator = resolver.Resolve(DefaultOrchestrator.OrchestratorName);

        Assert.NotNull(orchestrator);
        Assert.IsType<DefaultOrchestrator>(orchestrator);
    }

    [Fact]
    public void Resolve_RegisteredName_ReturnsCorrectOrchestrator()
    {
        var options = new OrchestratorOptions();
        options.Orchestrators["custom"] = new OrchestratorEntry { Type = typeof(TestOrchestrator) };

        var services = new ServiceCollection();
        services.AddScoped<TestOrchestrator>();
        services.AddScoped<DefaultOrchestrator>();
        RegisterDependencies(services);

        var sp = services.BuildServiceProvider().CreateScope().ServiceProvider;

        var resolver = new DefaultOrchestratorResolver(
            sp,
            Options.Create(options),

        NullLogger<DefaultOrchestratorResolver>.Instance);
        var orchestrator = resolver.Resolve("custom");

        Assert.NotNull(orchestrator);
        Assert.IsType<TestOrchestrator>(orchestrator);

    }

    [Fact]
    public void Resolve_UnknownName_FallsBackToDefault()
    {

        var resolver = CreateResolver();

        var orchestrator = resolver.Resolve("nonexistent");

        Assert.NotNull(orchestrator);
        Assert.IsType<DefaultOrchestrator>(orchestrator);
    }

    [Fact]
    public void Resolve_CaseInsensitive()
    {
        var options = new OrchestratorOptions();

        options.Orchestrators["Default"] = new OrchestratorEntry { Type = typeof(DefaultOrchestrator) };

        var services = new ServiceCollection();
        services.AddScoped<DefaultOrchestrator>();
        RegisterDependencies(services);

        var sp = services.BuildServiceProvider().CreateScope().ServiceProvider;

        var resolver = new DefaultOrchestratorResolver(
            sp,

            Options.Create(options),
        NullLogger<DefaultOrchestratorResolver>.Instance);

        var orchestrator = resolver.Resolve("DEFAULT");

        Assert.NotNull(orchestrator);

    }

    private static DefaultOrchestratorResolver CreateResolver()
    {
        var options = new OrchestratorOptions();

        options.Orchestrators[DefaultOrchestrator.OrchestratorName] = new OrchestratorEntry { Type = typeof(DefaultOrchestrator) };

        var services = new ServiceCollection();
        services.AddScoped<DefaultOrchestrator>();
        RegisterDependencies(services);
        var sp = services.BuildServiceProvider().CreateScope().ServiceProvider;

        return new DefaultOrchestratorResolver(
            sp,
            Options.Create(options),
        NullLogger<DefaultOrchestratorResolver>.Instance);
    }

    private static void RegisterDependencies(IServiceCollection services)
    {
        services.AddSingleton<IAICompletionService, NullCompletionService>();
        services.AddSingleton<IAIClientFactory, NullAIClientFactory>();
        services.AddSingleton<ITemplateService, NullAITemplateService>();

        services.AddSingleton(Mock.Of<IAIDeploymentManager>());
        services.AddSingleton<IToolRegistry, NullToolRegistry>();
        services.AddSingleton<ITextTokenizer, LuceneTextTokenizer>();
        services.AddLogging(builder => builder.ClearProviders());

    }

    private sealed class NullAITemplateService : ITemplateService
    {
        public Task<IReadOnlyList<Template>> ListAsync()

            => Task.FromResult<IReadOnlyList<Template>>([]);

        public Task<Template> GetAsync(string id)
            => Task.FromResult<Template>(null);

        public Task<string> RenderAsync(string id, IDictionary<string, object> arguments = null)
            => Task.FromResult<string>(null);

        public Task<string> MergeAsync(IEnumerable<string> ids, IDictionary<string, object> arguments = null, string separator = "\n\n")
            => Task.FromResult<string>(null);
    }
}
