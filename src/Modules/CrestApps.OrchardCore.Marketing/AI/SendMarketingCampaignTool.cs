using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Consent;
using CrestApps.OrchardCore.Marketing.Models;
using CrestApps.OrchardCore.Marketing.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Marketing.AI;

/// <summary>
/// Sends a draft or scheduled campaign through the send its page uses (<see cref="CampaignService.QueueSendAsync"/>): it is
/// scheduled when its send time is still to come, otherwise queued for the Marketing background task, whose outreach run
/// asks the consent service before every email, so opted-out addresses are skipped. Nothing goes out during the call; the
/// answer says "queued" or "scheduled", never "sent".
/// </summary>
internal sealed class SendMarketingCampaignTool : MarketingAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "sendMarketingCampaign";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "campaignId": {
          "type": "string",
          "description": "The campaign's id, from listRecords with kind marketingCampaign. It must be a draft or scheduled campaign."
        }
      },
      "required": ["campaignId"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Starts sending a draft campaign to every reachable customer in its segment, or schedules it when its send time is in the future. Opted-out addresses are skipped. Requires the site's email and base URL to be set up; emails go out in batches and cannot be recalled.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        if (ReadString(arguments, "campaignId") is not { } campaignId)
        {
            return "Nothing was sent: give the campaign's id. Use listRecords with kind marketingCampaign to find it.";
        }

        if (await services.GetRequiredService<ICampaignStore>().FindByIdAsync(campaignId, cancellationToken) is not { } campaign)
        {
            return $"Nothing was sent: no campaign has the id '{campaignId}'. Use listRecords with kind marketingCampaign to find it.";
        }

        if (campaign.Status is not (CampaignStatus.Draft or CampaignStatus.Scheduled))
        {
            return $"Nothing was sent: campaign '{campaign.Name}' is {ToCamel(campaign.Status.ToString())}. Only a draft or scheduled campaign can be sent.";
        }

        if (string.IsNullOrWhiteSpace(campaign.Subject) || string.IsNullOrWhiteSpace(campaign.Body) ||
            string.IsNullOrEmpty(campaign.SegmentId) ||
            await services.GetRequiredService<ISegmentStore>().FindByIdAsync(campaign.SegmentId, cancellationToken) is null)
        {
            return $"Nothing was sent: campaign '{campaign.Name}' needs a subject, a body and an existing segment. Finish it on the campaign's page.";
        }

        // The outreach run checks every address with the consent service; without one, nobody's opt-out would be honored.
        if (services.GetService<IConsentService>() is null)
        {
            return "Nothing was sent: the site can't check email consent, so opted-out addresses would not be skipped. Turn on consent before sending campaigns.";
        }

        var start = await services.GetRequiredService<CampaignService>().QueueSendAsync(campaign, cancellationToken);

        if (start == CampaignSendStart.NotConfigured)
        {
            return "Nothing was sent: campaigns need the site's email set up and its base URL, for the unsubscribe link. The campaign is unchanged.";
        }

        if (await CommitAsync(services, cancellationToken) is { } error)
        {
            return error;
        }

        var scheduledFor = start == CampaignSendStart.Scheduled ? await LocalTimeAsync(services, campaign.ScheduledUtc) : null;

        return Json(new
        {
            campaignId = campaign.ItemId,
            name = campaign.Name,
            status = ToCamel(campaign.Status.ToString()),
            scheduledFor,
            segmentId = campaign.SegmentId,
            message = start == CampaignSendStart.Scheduled
                ? $"Campaign '{campaign.Name}' is scheduled and starts sending at {scheduledFor}. Nothing has been sent yet; cancelMarketingCampaign stops it."
                : $"Campaign '{campaign.Name}' is queued: the background task starts sending it within about a minute, in batches. Nothing has been sent yet. Customers who don't accept marketing and opted-out addresses are skipped. Emails that go out cannot be recalled; cancelMarketingCampaign stops the rest.",
        });
    }
}
