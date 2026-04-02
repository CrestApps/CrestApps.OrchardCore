using CrestApps.AI.Chat;
using CrestApps.AI.Clients;
using CrestApps.AI.Completions;
using CrestApps.AI.Deployments;
using CrestApps.AI.Handlers;
using CrestApps.AI.Models;
using CrestApps.AI.Orchestration;
using CrestApps.AI.ResponseHandling;
using CrestApps.AI.Services;
using CrestApps.AI.Speech;
using CrestApps.AI.Tooling;
using CrestApps.AI.Tools;
using CrestApps.Templates;

using CrestApps.Templates.Extensions;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DataIngestion;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;

namespace CrestApps.AI;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the reusable templating services plus the built-in AI template source definitions.
    /// </summary>
    public static IServiceCollection AddAITemplating(
        this IServiceCollection services,

        Action<TemplateOptions> configure = null)

    {
        services
            .AddTemplating(configure)
            .AddAITemplateSource(AITemplateSources.Profile, entry =>
            {

                entry.DisplayName = new LocalizedString(AITemplateSources.Profile, "Profile");
                entry.Description = new LocalizedString(AITemplateSources.Profile, "Create a template that can be applied to AI profiles.");
            })
            .AddAITemplateSource(AITemplateSources.SystemPrompt, entry =>
            {
                entry.DisplayName = new LocalizedString(AITemplateSources.SystemPrompt, "System Prompt");

                entry.Description = new LocalizedString(AITemplateSources.SystemPrompt, "Create a reusable system prompt template.");
            });

        return services;
    }
    /// <summary>
    /// Registers an AI tool with the builder pattern for fluent configuration.
    /// By default, tools are registered as system tools (hidden from UI).
    /// Call <see cref="AIToolBuilder{TTool}.Selectable"/> to make the tool visible for user selection.
    /// </summary>
    /// <typeparam name="TTool">The tool type implementing <see cref="AITool"/>.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="name">The unique name for this tool.</param>
    /// <returns>A builder for fluent configuration of the tool.</returns>
    public static AIToolBuilder<TTool> AddAITool<TTool>(this IServiceCollection services, string name)
        where TTool : AITool
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        services.AddCoreAITool<TTool>(name);

        var entry = new AIToolDefinitionEntry(typeof(TTool))

        {
            Name = name,
            IsSystemTool = true,
        };

        services.Configure<AIToolDefinitionOptions>(o =>
        {
            if (string.IsNullOrEmpty(entry.Title))
            {
                entry.Title = name;
            }

            if (string.IsNullOrEmpty(entry.Description))
            {
                entry.Description = name;
            }

            o.SetTool(name, entry);
        });

        return new AIToolBuilder<TTool>(entry);

    }
    /// <summary>
    /// Registers the core DI services for an AI tool (singleton and keyed singleton)
    /// without adding it to the tool definition options. Use this for tools that
    /// should only be resolved programmatically (e.g., MCP invoke function).
    /// </summary>
    public static IServiceCollection AddCoreAITool<TTool>(this IServiceCollection services, string name)
        where TTool : AITool
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        services.AddSingleton<TTool>();
        services.AddKeyedSingleton<AITool>(name, (sp, key) => sp.GetRequiredService<TTool>());

        return services;
    }
    /// <summary>
    /// Adds core CrestApps AI services to the service collection.
    /// This is the main entry point for any ASP.NET Core application to use CrestApps AI.
    /// </summary>
    public static IServiceCollection AddCrestAppsAI(this IServiceCollection services)
    {
        // Ensure IHttpContextAccessor is available for services that need HTTP context.

        services.TryAddSingleton<IHttpContextAccessor, HttpContextAccessor>();

        services
            .AddAITemplating()
            .AddCrestAppsCoreServices()
            .AddScoped<IAIClientFactory, DefaultAIClientFactory>();

        services.TryAddScoped<IAICompletionService, DefaultAICompletionService>();
        services.TryAddScoped<IAICompletionContextBuilder, DefaultAICompletionContextBuilder>();

        services.TryAddEnumerable(ServiceDescriptor.Scoped<IAICompletionContextBuilderHandler, AIProfileCompletionContextBuilderHandler>());

        return services;

    }


    public static IServiceCollection AddAIProfile<TClient>(this IServiceCollection services, string implementationName, string providerName, Action<AIProfileProviderEntry> configure = null)

        where TClient : class, IAICompletionClient
    {
        return services
            .Configure<AIOptions>(o =>
            {
                o.AddProfileSource(implementationName, providerName, configure);

            })
            .AddAICompletionClient<TClient>(implementationName);

    }

    public static IServiceCollection AddAIDeploymentProvider(this IServiceCollection services, string providerName, Action<AIDeploymentProviderEntry> configure = null)
    {
        services
            .Configure<AIOptions>(o =>
            {

                o.AddDeploymentProvider(providerName, configure);
            });

        return services;
    }

    public static IServiceCollection AddAICompletionClient<TClient>(this IServiceCollection services, string clientName)
        where TClient : class, IAICompletionClient
    {
        services.Configure<AIOptions>(o =>
        {
            o.AddClient<TClient>(clientName);
        });

        services.TryAddScoped<TClient>();
        services.AddScoped<IAICompletionClient>(sp => sp.GetService<TClient>());

        return services;
    }

    public static IServiceCollection AddAIConnectionSource(this IServiceCollection services, string providerName, Action<AIProviderConnectionOptionsEntry> configure = null)
    {

        services.Configure<AIOptions>(o =>
        {
            o.AddConnectionSource(providerName, configure);
        });

        return services;
    }

    public static IServiceCollection AddAITemplateSource(this IServiceCollection services, string sourceName, Action<AITemplateSourceEntry> configure = null)
    {

        services.Configure<AIOptions>(o =>
        {
            o.AddTemplateSource(sourceName, configure);
        });

        return services;
    }
    /// <summary>
    /// Registers an <see cref="IngestionDocumentReader"/> implementation as a keyed singleton
    /// for each supported file extension.
    /// </summary>
    public static IServiceCollection AddIngestionDocumentReader<T>(this IServiceCollection services, params ExtractorExtension[] supportedExtensions)
        where T : IngestionDocumentReader
    {
        services.Configure<ChatDocumentsOptions>(options =>
        {
            foreach (var extension in supportedExtensions)

            {
                options.Add(extension);
            }
        });

        services.TryAddSingleton<T>();

        foreach (var extension in supportedExtensions)
        {

            services.AddKeyedSingleton<IngestionDocumentReader>(
                extension.Extension,

                (sp, _) => sp.GetRequiredService<T>());
        }

        return services;
    }
    /// <summary>
    /// Adds the orchestration services including the default progressive tool orchestrator,
    /// tool registry, orchestration context builder, and orchestrator resolver.
    /// </summary>

    public static IServiceCollection AddOrchestrationServices(this IServiceCollection services)
    {
        // Register embedded templates from this assembly so they are available
        // regardless of the host (OrchardCore, MVC, or any ASP.NET Core app).

        services.AddTemplatesFromAssembly(typeof(ServiceCollectionExtensions).Assembly);

        services.TryAddSingleton(TimeProvider.System);
        services.AddOptions<OrchestratorOptions>();
        services.AddOptions<DefaultOrchestratorOptions>();

        services.AddOptions<DefaultOrchestratorSettings>();
        services.AddOptions<DefaultAIDeploymentSettings>();
        services.AddOptions<InteractionDocumentSettings>();
        services.AddOptions<AIDataSourceSettings>();

        // Register DefaultAIOptions as a scoped service that reads from IOptionsSnapshot

        // and applies GeneralAISettings overrides. Host applications (OrchardCore, MVC, etc.)

        // can replace this with their own implementation (e.g., reading from ISiteService).
        services.TryAddScoped(sp =>

        {


            var snapshot = sp.GetRequiredService<IOptionsSnapshot<DefaultAIOptions>>();
            var settings = sp.GetRequiredService<IOptionsSnapshot<GeneralAISettings>>();

            return snapshot.Value.ApplySiteOverrides(settings.Value);
        });

        // Register the Framework-level deployment manager.

        // OrchardCore overrides this with its ISiteService-backed implementation.
        services.TryAddScoped<IAIDeploymentManager, DefaultAIDeploymentManager>();
        services.TryAddScoped<IInteractionDocumentSettingsProvider, DefaultInteractionDocumentSettingsProvider>();
        services.TryAddScoped<IAIDataSourceSettingsProvider, DefaultAIDataSourceSettingsProvider>();

        services.TryAddSingleton<IExternalChatRelayManager, ExternalChatRelayConnectionManager>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IChatResponseHandler, AIChatResponseHandler>());
        services.TryAddScoped<IChatResponseHandlerResolver, DefaultChatResponseHandlerResolver>();

        services.TryAddScoped<IAIToolsService, DefaultAIToolsService>();
        services.TryAddSingleton<ITextTokenizer, LuceneTextTokenizer>();

        services.TryAddScoped<IAIToolAccessEvaluator, DefaultAIToolAccessEvaluator>();
        services.TryAddScoped<PreemptiveSearchQueryProvider>();

        services.TryAddEnumerable(ServiceDescriptor.Scoped<IToolRegistryProvider, SystemToolRegistryProvider>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IToolRegistryProvider, ProfileToolRegistryProvider>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IToolRegistryProvider, AgentToolRegistryProvider>());
        services.AddScoped<IToolRegistry, DefaultToolRegistry>();

        services.TryAddEnumerable(ServiceDescriptor.Scoped<IOrchestrationContextBuilderHandler, CompletionContextOrchestrationHandler>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IOrchestrationContextBuilderHandler, PreemptiveRagOrchestrationHandler>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IOrchestrationContextBuilderHandler, AIToolExecutionContextOrchestrationHandler>());

        services.TryAddScoped<IOrchestrationContextBuilder, DefaultOrchestrationContextBuilder>();

        services.TryAddEnumerable(ServiceDescriptor.Scoped<IAICompletionServiceHandler, FunctionInvocationAICompletionServiceHandler>());

        services.TryAddEnumerable(ServiceDescriptor.Scoped<IAICompletionContextBuilderHandler, DataSourceAICompletionContextBuilderHandler>());

        services.AddOrchestrator<DefaultOrchestrator>(DefaultOrchestrator.OrchestratorName)
            .WithTitle("Default Orchestrator");

        services.AddScoped<IOrchestratorResolver, DefaultOrchestratorResolver>();


        // Register content generation system tools.
        services.AddAITool<GenerateImageTool>(GenerateImageTool.TheName)
            .WithTitle("Generate Image")

            .WithDescription("Generates an image from a text description using an AI image generation model.")
            .WithPurpose(AIToolPurposes.ContentGeneration);

        services.AddAITool<GenerateChartTool>(GenerateChartTool.TheName)
            .WithTitle("Generate Chart")
            .WithDescription("Generates a Chart.js configuration from a data description.")
            .WithPurpose(AIToolPurposes.ContentGeneration);

        services.AddAITool<CurrentDateTimeTool>(CurrentDateTimeTool.TheName)
            .WithTitle("Current Date & Time")
            .WithDescription("Returns the current date and time, optionally in a specific timezone.")
            .WithCategory("Utilities")
            .Selectable();

        return services;
    }
    /// <summary>
    /// Registers an orchestrator implementation with the given name.
    /// </summary>
    public static OrchestratorBuilder<TOrchestrator> AddOrchestrator<TOrchestrator>(this IServiceCollection services, string name)
        where TOrchestrator : class, IOrchestrator
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        services.TryAddScoped<TOrchestrator>();

        var entry = new OrchestratorEntry
        {
            Type = typeof(TOrchestrator),
        };

        services.Configure<OrchestratorOptions>(options =>
        {
            options.Orchestrators[name] = entry;
        });

        return new OrchestratorBuilder<TOrchestrator>(entry);
    }
}
