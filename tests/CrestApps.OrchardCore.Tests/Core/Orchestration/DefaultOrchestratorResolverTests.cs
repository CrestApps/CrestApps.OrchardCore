using CrestApps.AI.Prompting.Models;
using CrestApps.AI.Prompting.Services;
using CrestApps.OrchardCore.AI;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Orchestration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

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
        services.AddSingleton<IAITemplateService, NullAITemplateService>();
        services.AddSingleton<IToolRegistry, NullToolRegistry>();
        services.AddSingleton<ITextTokenizer, LuceneTextTokenizer>();
        services.AddLogging(builder => builder.ClearProviders());
    }

    private sealed class NullAITemplateService : IAITemplateService
    {
        public Task<IReadOnlyList<AITemplate>> ListAsync()
            => Task.FromResult<IReadOnlyList<AITemplate>>([]);

        public Task<AITemplate> GetAsync(string id)
            => Task.FromResult<AITemplate>(null);

        public Task<string> RenderAsync(string id, IDictionary<string, object> arguments = null)
            => Task.FromResult<string>(null);

        public Task<string> MergeAsync(IEnumerable<string> ids, IDictionary<string, object> arguments = null, string separator = "\n\n")
            => Task.FromResult<string>(null);
    }
}
