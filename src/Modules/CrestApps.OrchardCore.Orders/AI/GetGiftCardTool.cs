using System.Security.Claims;
using System.Text.Json;
using Microsoft.Extensions.AI;

namespace CrestApps.OrchardCore.Orders.AI;

/// <summary>
/// Returns one gift card as its page on Commerce › Gift cards shows it, with its activity newest first. The code is
/// never returned, only its last characters: it is a bearer credential worth money, and chat transcripts are stored.
/// </summary>
internal sealed class GetGiftCardTool : GiftCardAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "getGiftCard";

    private const int MaxActivity = 50;

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "giftCardId": {
          "type": "string",
          "description": "The card's id, from listGiftCards."
        }
      },
      "required": ["giftCardId"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Returns one gift card with its balance, recipient, note, the order that bought it and its activity (issued, spent, returned, adjusted, with reasons and who). The full code is never returned, only its last characters.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        var (card, error) = await FindCardAsync(services, ReadString(arguments, "giftCardId"), cancellationToken);

        if (card is null)
        {
            return error;
        }

        var result = await DescribeAsync(services, card);
        var activity = new List<object>();
        var entries = (card.Activity ?? []).OrderByDescending(entry => entry.CreatedUtc).ToList();

        foreach (var entry in entries.Take(MaxActivity))
        {
            activity.Add(new
            {
                at = await LocalTimeAsync(services, entry.CreatedUtc),
                amount = entry.Amount,
                balanceAfter = entry.Balance,
                reason = entry.Reason,
                by = entry.Actor,
                atCheckout = !string.IsNullOrEmpty(entry.SessionId),
            });
        }

        result["note"] = card.Note;
        result["activity"] = activity;
        result["activityTruncated"] = entries.Count > MaxActivity;
        result["adminPath"] = GetAdminPath(services, "GiftCardsEdit", new { action = "Edit", itemId = card.ItemId });

        return Json(result);
    }
}
