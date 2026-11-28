using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Fundraising.Models;
using CrestApps.OrchardCore.Fundraising.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Fundraising.AI;

/// <summary>
/// Returns a fundraising campaign's progress as its page computes it (<see cref="DonationService.GetProgressAsync"/>:
/// raised net of refunds, goal, percent, donors, days left) and, when asked, its donations from
/// <see cref="IDonationStore.ListByCampaignAsync"/>, largest first, capped and paged. No admin page lists a campaign's
/// donations; donor names and emails are personal data, returned only to a caller with Manage fundraising.
/// </summary>
internal sealed class GetFundraisingCampaignProgressTool : FundraisingAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "getFundraisingCampaignProgress";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "campaignId": {
          "type": "string",
          "description": "The campaign's content item id (a product with the Fundraising campaign part)."
        },
        "includeDonations": {
          "type": "boolean",
          "description": "Also return the campaign's donations, largest first."
        },
        "minimumAmount": {
          "type": "number",
          "description": "With includeDonations: only donations of at least this amount, in the donation's currency."
        },
        "page": {
          "type": "integer",
          "description": "With includeDonations: the page of donations to return, from 1."
        },
        "pageSize": {
          "type": "integer",
          "description": "With includeDonations: how many donations per page, up to 50 (default 20)."
        }
      },
      "required": ["campaignId"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Returns a campaign's amount raised, goal, percent, number of donors and days left, and optionally its donations (donor, amount, receipt number, tribute, refunded). Use it to report on a campaign; the content tools return only the campaign's settings.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override Permission Permission => FundraisingPermissions.ManageFundraising;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        if (ReadString(arguments, "campaignId") is not { } campaignId)
        {
            return "Give the campaign's content item id. Find it with the content tools (a product with the Fundraising campaign part).";
        }

        if (!TryReadDecimal(arguments, "minimumAmount", out var minimumAmount) || minimumAmount < 0m)
        {
            return "minimumAmount must be a number of zero or more.";
        }

        var campaign = await services.GetRequiredService<IContentManager>().GetAsync(campaignId, VersionOptions.Latest);

        if (campaign is null || !campaign.TryGet<FundraisingCampaignPart>(out var part))
        {
            return $"No fundraising campaign has the id '{campaignId}'. A campaign is a product with the Fundraising campaign part.";
        }

        var progress = await services.GetRequiredService<DonationService>().GetProgressAsync(campaign, cancellationToken);
        object donations = null;

        if (ReadBool(arguments, "includeDonations") == true)
        {
            var (page, pageSize) = ReadPaging(arguments);
            var all = (await services.GetRequiredService<IDonationStore>().ListByCampaignAsync(campaign.ContentItemId, cancellationToken))
                .Where(donation => minimumAmount is null || donation.Amount >= minimumAmount.Value)
                .OrderByDescending(donation => donation.Amount)
                .ThenByDescending(donation => donation.CreatedUtc)
                .ToList();

            var items = new List<object>();

            foreach (var donation in all.Skip((page - 1) * pageSize).Take(pageSize))
            {
                items.Add(new
                {
                    id = donation.ItemId,
                    donor = donation.DonorName,
                    donorEmail = donation.DonorEmail,
                    amount = donation.Amount,
                    currency = donation.Currency,
                    refunded = donation.Refunded,
                    fullyRefunded = donation.Amount > 0m && donation.Refunded >= donation.Amount,
                    receiptNumber = donation.ReceiptNumber,
                    tribute = donation.Tribute,
                    taxDeductible = donation.TaxDeductible,
                    orderId = donation.OrderId,
                    orderNumber = donation.OrderNumber,
                    givenAt = await LocalTimeAsync(services, donation.CreatedUtc),
                });
            }

            donations = new
            {
                items,
                total = all.Count,
                page,
                pageSize,
                truncated = (page - 1) * pageSize + items.Count < all.Count,
            };
        }

        return Json(new
        {
            campaignId = campaign.ContentItemId,
            title = campaign.DisplayText,
            raised = progress.Raised,
            goal = progress.Goal,
            percent = progress.Percent,
            currency = progress.Currency,
            donors = progress.Donors,
            daysLeft = progress.DaysLeft,
            endsAt = await LocalTimeAsync(services, part.EndsUtc),
            donations,
        });
    }
}
