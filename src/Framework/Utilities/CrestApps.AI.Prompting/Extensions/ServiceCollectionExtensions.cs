using System.Reflection;
using CrestApps.AI.Prompting.Parsing;
using CrestApps.AI.Prompting.Providers;
using CrestApps.AI.Prompting.Rendering;
using CrestApps.AI.Prompting.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CrestApps.AI.Prompting.Extensions;

/// <summary>
/// Extension methods for registering AI template services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the core AI template services to the service collection.
    /// Registers the Markdown front matter parser by default.
    /// Additional parsers (e.g., YAML, JSON) can be registered via
    /// <see cref="ServiceCollectionDescriptorExtensions.TryAddEnumerable(IServiceCollection, ServiceDescriptor)"/>
    /// for <see cref="IAITemplateParser"/>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional action to configure <see cref="AITemplateOptions"/>.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAIPrompting(
        this IServiceCollection services,
        Action<AITemplateOptions> configure = null)
    {
        // Register the built-in Markdown parser. Additional parsers can be added
        // by registering more IAITemplateParser implementations.
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IAITemplateParser, DefaultMarkdownAITemplateParser>());

        services.TryAddSingleton<IAITemplateEngine, FluidAITemplateEngine>();
        services.TryAddScoped<IAITemplateService, DefaultAITemplateService>();

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IAITemplateProvider, OptionsAITemplateProvider>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IAITemplateProvider, FileSystemAITemplateProvider>());

        if (configure != null)
        {
            services.Configure(configure);
        }

        return services;
    }

    /// <summary>
    /// Registers an assembly's embedded <c>AITemplates/Prompts/*.md</c> resources as AI templates.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="assembly">The assembly containing embedded template resources.</param>
    /// <param name="source">Optional source name. Defaults to the assembly name.</param>
    /// <param name="featureId">Optional feature ID to associate with all templates from this assembly.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAITemplatesFromAssembly(
        this IServiceCollection services,
        Assembly assembly,
        string source = null,
        string featureId = null)
    {
        services.AddSingleton<IAITemplateProvider>(sp =>
        {
            var parsers = sp.GetServices<IAITemplateParser>();

            return new EmbeddedResourceAITemplateProvider(assembly, parsers, source, featureId);
        });

        return services;
    }
}
