using System.ComponentModel.DataAnnotations;
using System.Text.Json.Nodes;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Core.Recipes;
using CrestApps.OrchardCore.Marketing.Models;
using CrestApps.OrchardCore.Marketing.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.Recipes.Models;

namespace CrestApps.OrchardCore.Marketing.Recipes;

/// <summary>
/// Adds or updates marketing campaigns from a <c>MarketingCampaigns</c> recipe step. A campaign is matched by its
/// <c>ItemId</c>, so running a recipe twice adds nothing, and its segment must exist.
/// </summary>
/// <remarks>
/// <code>
/// { "name": "MarketingCampaigns", "Campaigns": [ { "ItemId": "...", "Name": "Win back", "SegmentId": "...", "Subject": "We miss you", "Body": "..." } ] }
/// </code>
/// A recipe never sends email: a new campaign is always a draft, whatever status the recipe carries, and a campaign that
/// is scheduled or has started sending on this site is left as it is.
/// </remarks>
internal sealed class MarketingCampaignsStep : CatalogStoreRecipeStep<Campaign>
{
    public const string StepKey = "MarketingCampaigns";

    public const string EntriesName = "Campaigns";

    private readonly ISegmentStore _segmentStore;

    internal readonly IStringLocalizer S;

    public MarketingCampaignsStep(
        ICampaignStore store,
        ISegmentStore segmentStore,
        IStringLocalizer<MarketingCampaignsStep> stringLocalizer)
        : base(StepKey, EntriesName, store)
    {
        _segmentStore = segmentStore;
        S = stringLocalizer;
    }

    protected override async ValueTask<Campaign> NewAsync(JsonObject token, RecipeExecutionContext context)
    {
        var campaign = await base.NewAsync(token, context);

        if (campaign is not null)
        {
            ResetDelivery(campaign);
        }

        return campaign;
    }

    protected override async ValueTask<Campaign> PrepareUpdateAsync(Campaign entry, JsonObject token)
    {
        // Only a draft takes changes: a scheduled or sending campaign belongs to this site's delivery.
        if (entry.Status != CampaignStatus.Draft)
        {
            return entry;
        }

        var campaign = await base.PrepareUpdateAsync(entry, token);

        ResetDelivery(campaign);

        return campaign;
    }

    protected override void Preparing(Campaign entry, JsonObject token)
    {
        entry.Name = entry.Name?.Trim();
        entry.Subject = entry.Subject?.Trim();
    }

    protected override async ValueTask<ValidationResultDetails> ValidateAsync(Campaign entry)
    {
        var result = new ValidationResultDetails();

        if (string.IsNullOrEmpty(entry.Name))
        {
            result.Fail(new ValidationResult(S["Name the campaign. Only staff see it."]));
        }

        if (string.IsNullOrEmpty(entry.SegmentId) || await _segmentStore.FindByIdAsync(entry.SegmentId) is null)
        {
            result.Fail(new ValidationResult(S["The campaign '{0}' goes to a segment that doesn't exist. Import the segments first.", entry.Name]));
        }

        if (string.IsNullOrEmpty(entry.Subject))
        {
            result.Fail(new ValidationResult(S["Write the email's subject."]));
        }

        if (string.IsNullOrWhiteSpace(entry.Body))
        {
            result.Fail(new ValidationResult(S["Write the email."]));
        }

        return result;
    }

    private static void ResetDelivery(Campaign campaign)
    {
        campaign.Status = CampaignStatus.Draft;
        campaign.ScheduledUtc = null;
        campaign.StartedUtc = null;
        campaign.CompletedUtc = null;
        campaign.Cursor = null;
        campaign.Sent = 0;
        campaign.Skipped = 0;
        campaign.Failed = 0;
    }
}
