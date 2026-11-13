using CrestApps.OrchardCore.Core.Emails;
using CrestApps.OrchardCore.Emails;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers the site's templated emails.
/// </summary>
public static class TemplatedEmailServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="ITemplatedEmailSender"/> and <see cref="ITemplatedEmailTemplates"/>. A module that keeps
    /// the site's templates of its own kinds of email adds an <see cref="ITemplatedEmailTemplateSource"/> with
    /// <see cref="AddTemplatedEmailTemplateSource{TSource}"/>, and lists its kinds with <c>AddTemplatedEmailKind</c>.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddTemplatedEmails(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddScoped<ITemplatedEmailSender, TemplatedEmailSender>();
        services.TryAddScoped<ITemplatedEmailTemplates, LiquidTemplatedEmailTemplates>();

        return services;
    }

    /// <summary>
    /// Adds a source of the site's templates of a group of email kinds.
    /// </summary>
    /// <typeparam name="TSource">The source.</typeparam>
    /// <param name="services">The services.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddTemplatedEmailTemplateSource<TSource>(this IServiceCollection services)
        where TSource : class, ITemplatedEmailTemplateSource
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddTemplatedEmails();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ITemplatedEmailTemplateSource, TSource>());

        return services;
    }
}
