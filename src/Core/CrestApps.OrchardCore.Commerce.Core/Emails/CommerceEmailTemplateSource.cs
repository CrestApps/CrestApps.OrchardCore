using CrestApps.OrchardCore.Commerce.Emails;
using CrestApps.OrchardCore.Emails;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Commerce.Core.Emails;

/// <summary>
/// The store's templates of its emails, kept in <see cref="OrderEmailTemplateSettings"/> and edited under
/// Settings › Commerce › Emails, with the store's name as <c>{{ StoreName }}</c>. Only the commerce sender reads it, so
/// the store's layout never wraps another module's email.
/// </summary>
public sealed class CommerceEmailTemplateSource : ITemplatedEmailTemplateSource
{
    private readonly ISiteService _siteService;

    /// <summary>
    /// Initializes a new instance of the <see cref="CommerceEmailTemplateSource"/> class.
    /// </summary>
    /// <param name="siteService">The site service.</param>
    public CommerceEmailTemplateSource(ISiteService siteService)
    {
        _siteService = siteService;
    }

    /// <inheritdoc/>
    public async Task<TemplatedEmailOverride> FindAsync(string kind, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(kind))
        {
            return null;
        }

        var site = await _siteService.GetSiteSettingsAsync();
        var settings = site.GetOrCreate<OrderEmailTemplateSettings>();
        var template = settings.Templates is not null && settings.Templates.TryGetValue(kind, out var found) && found.IsEnabled ? found : null;

        return new TemplatedEmailOverride
        {
            Template = template is null ? null : new TemplatedEmailTemplate { IsEnabled = true, Subject = template.Subject, Body = template.Body },
            Layout = settings.Layout,
            Values = new Dictionary<string, string>(StringComparer.Ordinal) { ["StoreName"] = site.SiteName ?? string.Empty },
        };
    }
}
