using CrestApps.Core.AI.Chat.Privacy;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.AI.Services;

/// <summary>
/// Keeps the site's name in a conversation whose personal details are redacted: it is the business the assistant
/// speaks for, not the customer's personal data.
/// </summary>
/// <remarks>
/// Live, a call's transcript came back with the business's name redacted wherever the assistant introduced itself.
/// </remarks>
public sealed class AISiteNamePrivacyHandler : AIConversationPrivacyHandlerBase
{
    private readonly ISiteService _siteService;

    /// <summary>
    /// Initializes a new instance of the <see cref="AISiteNamePrivacyHandler"/> class.
    /// </summary>
    /// <param name="siteService">The site service the site's name is read from.</param>
    public AISiteNamePrivacyHandler(ISiteService siteService)
    {
        _siteService = siteService;
    }

    /// <inheritdoc/>
    public override async Task CollectingAsync(AIConversationPrivacyContext context, CancellationToken cancellationToken)
    {
        var site = await _siteService.GetSiteSettingsAsync();

        AINamesToKeep.Add(context, site?.SiteName);
    }
}
