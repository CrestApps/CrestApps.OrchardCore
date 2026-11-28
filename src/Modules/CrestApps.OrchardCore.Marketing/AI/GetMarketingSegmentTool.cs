using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Marketing.Models;
using CrestApps.OrchardCore.Marketing.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Marketing.AI;

/// <summary>
/// Returns one customer segment with its rules and the two counts its edit page shows
/// (<see cref="CampaignService.CountAsync(Segment, CancellationToken)"/>): customers in the segment, and those of
/// them a campaign can reach (active, with an email, accepting marketing). Plain lists and reads of segments go through the
/// generic record tools (kind <c>marketingSegment</c>); this read adds the audience size, which needs the purchase records.
/// </summary>
internal sealed class GetMarketingSegmentTool : MarketingAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "getMarketingSegment";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "segmentId": {
          "type": "string",
          "description": "The segment's id, from listRecords with kind marketingSegment."
        }
      },
      "required": ["segmentId"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Returns one segment with its rules and two counts: customers in the segment and customers who can be emailed (accept marketing). Use it to size an audience before sending. Addresses that opted out are also skipped when a campaign is sent, so fewer may receive it.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        if (ReadString(arguments, "segmentId") is not { } segmentId)
        {
            return "Nothing was read: give the segment's id. Use listRecords with kind marketingSegment to find it.";
        }

        if (await services.GetRequiredService<ISegmentStore>().FindByIdAsync(segmentId, cancellationToken) is not { } segment)
        {
            return $"Nothing was read: no segment has the id '{segmentId}'. Use listRecords with kind marketingSegment to find it.";
        }

        var (inSegment, reachable) = await services.GetRequiredService<CampaignService>().CountAsync(segment, cancellationToken);
        var currency = (await services.GetRequiredService<ISiteService>().GetSettingsAsync<CheckoutSettings>()).Currency;
        var rules = segment.Rules ?? new SegmentRules();

        return Json(new
        {
            id = segment.ItemId,
            name = segment.Name,
            description = segment.Description,
            rules = new
            {
                minOrders = rules.MinOrders,
                maxOrders = rules.MaxOrders,
                minSpent = rules.MinSpent,
                maxSpent = rules.MaxSpent,
                spentCurrency = currency,
                lastOrderAtLeastDaysAgo = rules.LastOrderAtLeastDaysAgo,
                lastOrderWithinDays = rules.LastOrderWithinDays,
                firstOrderWithinDays = rules.FirstOrderWithinDays,
                country = rules.Country,
            },
            customersInSegment = inSegment,
            customersWhoCanBeEmailed = reachable,
            note = "customersWhoCanBeEmailed counts active customers with an email who accept marketing, and stops at 10,000. When the campaign is sent, opted-out addresses are skipped too, so fewer may receive it.",
            adminPath = GetAdminPath(services, "SegmentsEdit", new { area = MarketingConstants.Feature, itemId = segment.ItemId }),
        });
    }
}
