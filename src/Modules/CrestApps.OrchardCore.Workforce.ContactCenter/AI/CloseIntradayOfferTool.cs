using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Workforce.ContactCenter.Models;
using CrestApps.OrchardCore.Workforce.ContactCenter.Services.Intraday;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;
using YesSql;
using static CrestApps.OrchardCore.Workforce.ContactCenter.AI.ContactCenterWorkforceTools;

namespace CrestApps.OrchardCore.Workforce.ContactCenter.AI;

/// <summary>
/// Closes an overtime or time-off offer to new takers, as the Intraday page's Close button does.
/// </summary>
internal sealed class CloseIntradayOfferTool : ContactCenterWorkforceToolBase
{
    public const string ToolName = "closeIntradayOffer";

    private static readonly JsonElement _schema = Schema("""
    {
      "type": "object",
      "properties": {
        "offerId": { "type": "string", "description": "The offer's ID, from getIntradayStatus." }
      },
      "required": ["offerId"],
      "additionalProperties": false
    }
    """);

    public override string Name => ToolName;

    public override string Description => "Closes an overtime or time-off offer so no more workers can take it. Places already taken stay.";

    public override JsonElement JsonSchema => _schema;

    protected override Permission Permission => WorkforcePermissions.ManageForecasts;

    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var id = Read(arguments, "offerId");

        if (id is null)
        {
            return "Give the offer's ID (offerId), from getIntradayStatus.";
        }

        var intraday = services.GetRequiredService<IntradayService>();
        var offer = await intraday.FindAsync(id, cancellationToken);

        if (offer is null)
        {
            return $"No offer has the ID '{id}'.";
        }

        if (offer.Status != IntradayOfferStatus.Open)
        {
            return ToJson(new
            {
                closed = false,
                id,
                status = offer.Status.ToString(),
                message = $"The offer is no longer open (status {offer.Status}); nothing was changed.",
            });
        }

        await intraday.CloseAsync(offer, cancellationToken);
        await services.GetRequiredService<ISession>().SaveChangesAsync(cancellationToken);

        return ToJson(new
        {
            closed = true,
            id,
            kind = offer.Kind.ToString(),
            placesTaken = offer.Claims.Count,
            places = offer.Places,
            status = offer.Status.ToString(),
            message = "The offer is closed. Places already taken stay.",
        });
    }
}
