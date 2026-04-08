using System.Reflection;
using CrestApps.Core.Templates.Parsing;
using CrestApps.Core.Templates.Providers;
using CrestApps.Core.Templates.Rendering;
using CrestApps.Core.Templates.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CrestApps.Core.Templates.Extensions;

/// <summary>
/// Extension methods for registering template services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the core template services to the service collection.
    /// Registers the Markdown front matter parser by default.
    /// Additional parsers (e.g., YAML, JSON) can be registered via
    /// <see cref="ServiceCollectionDescriptorExtensions.TryAddEnumerable(IServiceCollection, ServiceDescriptor)"/>
    /// for <see cref="ITemplateParser"/>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional action to configure <see cref="TemplateOptions"/>.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddTemplating(
        this IServiceCollection services,
        Action<TemplateOptions> configure = null)
    {
        // Register the built-in Markdown parser. Additional parsers can be added
        // by registering more ITemplateParser implementations.
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ITemplateParser, DefaultMarkdownTemplateParser>());

        services.TryAddSingleton<ITemplateEngine, FluidTemplateEngine>();
        services.TryAddScoped<ITemplateService, DefaultTemplateService>();

        services.TryAddEnumerable(ServiceDescriptor.Singleton<ITemplateProvider, OptionsTemplateProvider>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ITemplateProvider, FileSystemTemplateProvider>());

        if (configure != null)
        {
            services.Configure(configure);
        }

        return services;
    }
    /// <summary>
    /// Registers an assembly's embedded <c>Templates/Prompts/*.md</c> resources as templates.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="assembly">The assembly containing embedded template resources.</param>
    /// <param name="source">Optional source name. Defaults to the assembly name.</param>
    /// <param name="featureId">Optional feature ID to associate with all templates from this assembly.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddTemplatesFromAssembly(
        this IServiceCollection services,
        Assembly assembly,
        string source = null,
        string featureId = null)
    {
        services.AddSingleton<ITemplateProvider>(sp =>
        {
            var parsers = sp.GetServices<ITemplateParser>();

            return new EmbeddedResourceTemplateProvider(assembly, parsers, source, featureId);
        });

        return services;
    }
}
