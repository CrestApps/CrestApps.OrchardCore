using CrestApps.OrchardCore.Marketing.Models;
using CrestApps.OrchardCore.Marketing.Services;
using CrestApps.OrchardCore.Outreach;
using CrestApps.OrchardCore.Outreach.Models;

namespace CrestApps.OrchardCore.Marketing.Outreach;

/// <summary>
/// Copies a campaign run's counters and position onto the campaign, and marks it sent when the run completes.
/// </summary>
public sealed class MarketingCampaignRunObserver : IOutreachRunObserver
{
    private readonly ICampaignStore _campaigns;

    /// <summary>
    /// Initializes a new instance of the <see cref="MarketingCampaignRunObserver"/> class.
    /// </summary>
    /// <param name="campaigns">The campaign store.</param>
    public MarketingCampaignRunObserver(ICampaignStore campaigns)
    {
        _campaigns = campaigns;
    }

    /// <inheritdoc/>
    public async Task RunUpdatedAsync(OutreachRun run, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(run);

        if (!string.Equals(run.OwnerType, MarketingOutreachConstants.CampaignOwnerType, StringComparison.Ordinal) ||
            string.IsNullOrEmpty(run.OwnerId) ||
            await _campaigns.FindByIdAsync(run.OwnerId, cancellationToken) is not { } campaign)
        {
            return;
        }

        campaign.Sent = run.Sent;
        campaign.Skipped = run.Skipped;
        campaign.Failed = run.Failed;
        campaign.Cursor = run.Cursor;
        campaign.ModifiedUtc = run.ModifiedUtc;

        // A campaign someone canceled stays canceled.
        if (campaign.Status == CampaignStatus.Sending)
        {
            if (run.Status == OutreachRunStatus.Completed)
            {
                campaign.Status = CampaignStatus.Sent;
                campaign.CompletedUtc = run.CompletedUtc;
            }
            else if (run.Status is OutreachRunStatus.Failed or OutreachRunStatus.Canceled)
            {
                campaign.Status = CampaignStatus.Canceled;
                campaign.CompletedUtc = run.CompletedUtc;
            }
        }

        await _campaigns.UpdateAsync(campaign, cancellationToken);
    }
}
