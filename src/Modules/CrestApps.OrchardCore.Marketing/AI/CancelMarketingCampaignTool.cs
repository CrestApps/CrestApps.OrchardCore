using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Marketing.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Marketing.AI;

/// <summary>
/// Cancels a scheduled or sending campaign through the cancel its page uses (<see cref="CampaignService.CancelAsync"/>),
/// which also stops its outreach run. Stopping a mailing is the safe direction, so it does not ask for approval.
/// </summary>
internal sealed class CancelMarketingCampaignTool : MarketingAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "cancelMarketingCampaign";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "campaignId": {
          "type": "string",
          "description": "The campaign's id, from listRecords with kind marketingCampaign. It must be scheduled or sending."
        }
      },
      "required": ["campaignId"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Cancels a scheduled or sending campaign and stops its outreach run. Emails already sent stay sent, and a canceled campaign cannot be restarted.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        if (ReadString(arguments, "campaignId") is not { } campaignId)
        {
            return "Nothing was canceled: give the campaign's id. Use listRecords with kind marketingCampaign to find it.";
        }

        if (await services.GetRequiredService<ICampaignStore>().FindByIdAsync(campaignId, cancellationToken) is not { } campaign)
        {
            return $"Nothing was canceled: no campaign has the id '{campaignId}'. Use listRecords with kind marketingCampaign to find it.";
        }

        if (!await services.GetRequiredService<CampaignService>().CancelAsync(campaign, cancellationToken))
        {
            return $"Nothing was canceled: campaign '{campaign.Name}' is {ToCamel(campaign.Status.ToString())}. Only a scheduled or sending campaign can be canceled.";
        }

        if (await CommitAsync(services, cancellationToken) is { } error)
        {
            return error;
        }

        return Json(new
        {
            campaignId = campaign.ItemId,
            name = campaign.Name,
            status = ToCamel(campaign.Status.ToString()),
            sent = campaign.Sent,
            skipped = campaign.Skipped,
            failed = campaign.Failed,
            message = $"Campaign '{campaign.Name}' was canceled; nothing more goes out. {campaign.Sent} email(s) had already gone out and stay sent.",
        });
    }
}
