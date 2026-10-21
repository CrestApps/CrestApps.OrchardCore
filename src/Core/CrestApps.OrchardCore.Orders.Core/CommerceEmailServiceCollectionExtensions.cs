using CrestApps.OrchardCore.Orders.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Orders.Core;

/// <summary>
/// Registers the commerce email sender and the kinds of email the store can write its own templates of.
/// </summary>
public static class CommerceEmailServiceCollectionExtensions
{
    /// <summary>
    /// Adds the <see cref="ICommerceEmailSender"/>. Without the Orders feature's templates, every email goes as built.
    /// </summary>
    /// <param name="services">The services.</param>
    public static IServiceCollection AddCommerceEmailSender(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddScoped<ICommerceEmailSender, CommerceEmailSender>();
        services.TryAddScoped<IOrderEmailTemplates, AsBuiltOrderEmailTemplates>();

        return services;
    }

    /// <summary>
    /// Adds a kind of email the store can write its own template of, listed under Settings › Commerce › Emails.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <param name="name">The kind's name, which <see cref="CommerceEmail.Kind"/> names.</param>
    /// <param name="title">The kind's title on the settings page.</param>
    /// <param name="values">What the email's own values are, for the settings page; or <see langword="null"/>.</param>
    public static IServiceCollection AddCommerceEmailKind(this IServiceCollection services, string name, LocalizationSource title, LocalizationSource values = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var kind = new CommerceEmailKind(name, title, values);

        services.Configure<CommerceEmailOptions>(options => options.AddKind(kind));

        return services;
    }
}
