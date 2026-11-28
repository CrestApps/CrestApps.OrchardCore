using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Consent;
using CrestApps.OrchardCore.Marketing.Services;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Marketing.AI;

/// <summary>
/// Emails one test of a campaign, rendered for a sample customer of its segment, through the test the campaign's page sends
/// (<see cref="CampaignService.SendTestAsync"/>). The address is checked against the consent service first, so a test never
/// goes to an address that opted out.
/// </summary>
internal sealed class SendMarketingCampaignTestTool : MarketingAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "sendMarketingCampaignTest";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "campaignId": {
          "type": "string",
          "description": "The campaign's id, from listRecords with kind marketingCampaign."
        },
        "to": {
          "type": "string",
          "description": "The one email address the test goes to, as the user typed it."
        }
      },
      "required": ["campaignId", "to"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Sends one test email of a campaign, rendered with a sample customer from its segment, to the address given. Use it to check the content before sending the campaign. An address that opted out of email is refused.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        if (ReadString(arguments, "campaignId") is not { } campaignId)
        {
            return "Nothing was sent: give the campaign's id. Use listRecords with kind marketingCampaign to find it.";
        }

        var to = ReadString(arguments, "to");

        if (to is null || to.Contains(',', StringComparison.Ordinal) || to.Contains(';', StringComparison.Ordinal) || to.IndexOf('@', StringComparison.Ordinal) <= 0)
        {
            return "Nothing was sent: give one email address for the test.";
        }

        if (await services.GetRequiredService<ICampaignStore>().FindByIdAsync(campaignId, cancellationToken) is not { } campaign)
        {
            return $"Nothing was sent: no campaign has the id '{campaignId}'. Use listRecords with kind marketingCampaign to find it.";
        }

        if (services.GetService<IConsentService>() is not { } consentService)
        {
            return "Nothing was sent: the site can't check email consent right now, so a test can't be sent from a chat. Send it from the campaign's page.";
        }

        if ((await consentService.GetAsync(ConsentChannels.Email, to, cancellationToken: cancellationToken))?.HasOptedOut == true)
        {
            return $"Nothing was sent: {to} has opted out of email. Use another address for the test.";
        }

        var campaignService = services.GetRequiredService<CampaignService>();

        if (!await campaignService.CanSendAsync())
        {
            return "Nothing was sent: the site's email isn't set up. Set it up under Settings, then try again.";
        }

        var baseUrl = (await services.GetRequiredService<ISiteService>().GetSiteSettingsAsync()).BaseUrl;
        var linkGenerator = services.GetService<LinkGenerator>();

        string UnsubscribeUrl(string token)
            => (baseUrl?.TrimEnd('/') ?? string.Empty) + linkGenerator?.GetPathByRouteValues(MarketingConstants.UnsubscribeRouteName, new { token });

        if (!await campaignService.SendTestAsync(campaign, to, UnsubscribeUrl, cancellationToken))
        {
            return $"Nothing was sent: the test of '{campaign.Name}' couldn't be handed to the email provider. Check the site's email settings.";
        }

        return Json(new
        {
            campaignId = campaign.ItemId,
            name = campaign.Name,
            to,
            message = $"A test of '{campaign.Name}' was handed to the email provider for {to}, with the subject marked [Test].",
        });
    }
}
