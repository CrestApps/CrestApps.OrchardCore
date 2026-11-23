using CrestApps.OrchardCore.Emails;
using CrestApps.OrchardCore.MailingLists.Models;
using Microsoft.Extensions.Options;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.MailingLists.Core.Services;

/// <summary>
/// The site's own templates of the mailing lists e-mails, from <see cref="MailingListEmailSettings"/>. It answers only
/// for the kinds of the mailing lists group, so its layout never wraps another module's e-mail.
/// </summary>
public sealed class MailingListEmailTemplateSource : ITemplatedEmailTemplateSource
{
    private readonly ISiteService _siteService;
    private readonly TemplatedEmailOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="MailingListEmailTemplateSource"/> class.
    /// </summary>
    /// <param name="siteService">The site service.</param>
    /// <param name="options">The templated e-mail options, for the group of a kind.</param>
    public MailingListEmailTemplateSource(
        ISiteService siteService,
        IOptions<TemplatedEmailOptions> options)
    {
        _siteService = siteService;
        _options = options.Value;
    }

    /// <inheritdoc/>
    public async Task<TemplatedEmailOverride> FindAsync(string kind, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(kind) || _options.Find(kind)?.Group != MailingListsConstants.EmailGroup)
        {
            return null;
        }

        var settings = (await _siteService.GetSiteSettingsAsync()).GetOrCreate<MailingListEmailSettings>();
        var template = settings.Templates is not null && settings.Templates.TryGetValue(kind, out var found) && found.IsEnabled ? found : null;

        if (template is null && string.IsNullOrWhiteSpace(settings.Layout))
        {
            return null;
        }

        return new TemplatedEmailOverride
        {
            Template = template,
            Layout = settings.Layout,
        };
    }
}
