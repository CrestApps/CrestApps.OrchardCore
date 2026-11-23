using CrestApps.OrchardCore.MailingLists;
using CrestApps.OrchardCore.Marketing.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Marketing;

/// <summary>
/// Lets a campaign go to the subscribed members of a mailing list as well as to a segment, while the Mailing Lists
/// feature is on. The campaign editor offers the enabled lists; the run reads the list through the mailing lists
/// Outreach audience, and each e-mail's unsubscribe link unsubscribes the list. Without the feature, Marketing sends to
/// segments only, as before.
/// </summary>
[RequireFeatures(MailingListsConstants.Features.MailingLists)]
public sealed class MailingListsStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        // Replaces Marketing's default, which has no lists, whichever startup runs first.
        services.Replace(ServiceDescriptor.Scoped<ICampaignMailingLists, MailingListCampaignLists>());
    }
}
