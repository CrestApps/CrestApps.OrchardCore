using CrestApps.OrchardCore.Emails;
using OrchardCore.Localization;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers the kinds of email the site can write its own templates of.
/// </summary>
public static class TemplatedEmailKindServiceCollectionExtensions
{
    /// <summary>
    /// Adds a kind of email the site can write its own template of.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <param name="group">The group the kind is listed in, such as <c>Tickets</c>.</param>
    /// <param name="name">The kind's name, which <see cref="TemplatedEmail.Kind"/> names.</param>
    /// <param name="title">The kind's title on the settings page.</param>
    /// <param name="values">What the email's own values are, for the settings page; or <see langword="null"/>.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddTemplatedEmailKind(this IServiceCollection services, string group, string name, LocalizationSource title, LocalizationSource values = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var kind = new TemplatedEmailKind(group, name, title, values);

        services.Configure<TemplatedEmailOptions>(options => options.AddKind(kind));

        return services;
    }
}
