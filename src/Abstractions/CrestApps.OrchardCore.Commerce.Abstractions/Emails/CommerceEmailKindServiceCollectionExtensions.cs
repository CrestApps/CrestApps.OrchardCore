using CrestApps.OrchardCore.Commerce.Emails;
using OrchardCore.Localization;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers the kinds of email the store can write its own templates of.
/// </summary>
public static class CommerceEmailKindServiceCollectionExtensions
{
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
