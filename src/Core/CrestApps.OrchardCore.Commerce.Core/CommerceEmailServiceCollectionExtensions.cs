using CrestApps.OrchardCore.Commerce.Core.Emails;
using CrestApps.OrchardCore.Commerce.Emails;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CrestApps.OrchardCore.Commerce.Core;

/// <summary>
/// Registers the store's email sender and its templates.
/// </summary>
public static class CommerceEmailServiceCollectionExtensions
{
    /// <summary>
    /// Adds the <see cref="ICommerceEmailSender"/> and the store's Liquid templates of its emails. Without the Email
    /// feature nothing is sent, and without the Liquid feature every email goes as built. Each feature that sends a kind
    /// of email registers it with <c>AddCommerceEmailKind</c>.
    /// </summary>
    /// <param name="services">The services.</param>
    public static IServiceCollection AddCommerceEmailSender(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddScoped<ICommerceEmailSender, CommerceEmailSender>();
        services.TryAddScoped<ICommerceEmailTemplates, LiquidCommerceEmailTemplates>();

        return services;
    }
}
