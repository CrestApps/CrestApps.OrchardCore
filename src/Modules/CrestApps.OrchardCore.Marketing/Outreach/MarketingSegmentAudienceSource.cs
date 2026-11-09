using System.Globalization;
using CrestApps.OrchardCore.Customers.Core.Models;
using CrestApps.OrchardCore.Customers.Core.Services;
using CrestApps.OrchardCore.Marketing.Services;
using CrestApps.OrchardCore.Outreach;
using CrestApps.OrchardCore.Outreach.Models;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Marketing.Outreach;

/// <summary>
/// Pages the customers of a campaign's segment, in the order their purchases were first counted, with the document id of
/// the last one as the cursor. Customers who don't accept marketing, aren't active or have no e-mail are skipped.
/// </summary>
public sealed class MarketingSegmentAudienceSource : IOutreachAudienceSource
{
    private readonly ICampaignStore _campaigns;
    private readonly ISegmentStore _segments;
    private readonly ICustomerPurchasesStore _purchases;
    private readonly ICustomerStore _customers;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="MarketingSegmentAudienceSource"/> class.
    /// </summary>
    public MarketingSegmentAudienceSource(
        ICampaignStore campaigns,
        ISegmentStore segments,
        ICustomerPurchasesStore purchases,
        ICustomerStore customers,
        IClock clock)
    {
        _campaigns = campaigns;
        _segments = segments;
        _purchases = purchases;
        _customers = customers;
        _clock = clock;
    }

    /// <inheritdoc/>
    public string Name
        => MarketingOutreachConstants.SegmentAudienceSource;

    /// <inheritdoc/>
    public async Task<OutreachAudiencePage> GetPageAsync(OutreachRun run, string cursor, int pageSize, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(run);

        var campaign = string.IsNullOrEmpty(run.OwnerId) ? null : await _campaigns.FindByIdAsync(run.OwnerId, cancellationToken);
        var segment = campaign is null || string.IsNullOrEmpty(campaign.SegmentId) ? null : await _segments.FindByIdAsync(campaign.SegmentId, cancellationToken);

        if (segment is null)
        {
            return OutreachAudiencePage.Empty;
        }

        var batch = await _purchases.ListAsync(segment.Rules, _clock.UtcNow, cursor, pageSize, cancellationToken);
        var recipients = new List<OutreachRecipient>(batch.Count);

        foreach (var purchases in batch)
        {
            var customer = await _customers.FindByIdAsync(purchases.ItemId, cancellationToken);

            // Only customers who said yes to marketing get it.
            var reachable = customer is { AcceptsMarketing: true, Status: CustomerStatus.Active } && !string.IsNullOrWhiteSpace(customer.Email);

            recipients.Add(new OutreachRecipient
            {
                Key = purchases.ItemId,
                ContactId = purchases.ItemId,
                Address = reachable ? customer.Email : null,
                DisplayName = customer?.GetName(),
                SkipReason = reachable ? null : OutreachConstants.SkipReasons.NoConsent,
            });
        }

        return new OutreachAudiencePage
        {
            Recipients = recipients,
            NextCursor = batch.Count < pageSize ? null : batch[^1].Id.ToString(CultureInfo.InvariantCulture),
        };
    }
}
