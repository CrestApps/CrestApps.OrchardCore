using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using OrchardCore.Localization;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers the kinds of work, besides the messaging workspace, that use reply templates.
/// </summary>
public static class MessageTemplateScopeServiceCollectionExtensions
{
    /// <summary>
    /// Adds a scope of reply templates, such as tickets', so the template editor offers it.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <param name="name">The scope, such as <c>Tickets</c>.</param>
    /// <param name="title">Its title in the template editor.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddMessageTemplateScope(this IServiceCollection services, string name, LocalizationSource title)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(title);

        services.Configure<MessageTemplateScopeOptions>(options => options.Add(name, title));

        return services;
    }
}
