using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Orders.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Orders.AI;

/// <summary>
/// Emails a gift card, with its code, to its saved recipient again, as the Email action of the card's page on
/// Commerce › Gift cards (<see cref="GiftCardEmailSender.SendAsync"/>). The code goes only into the email, never into
/// the answer.
/// </summary>
internal sealed class EmailGiftCardTool : GiftCardAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "emailGiftCard";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "giftCardId": {
          "type": "string",
          "description": "The card's id, from listGiftCards. It must have a recipient email; set one with updateGiftCard."
        }
      },
      "required": ["giftCardId"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Emails a gift card with its code to its saved recipient again. The card must have a recipient email.";

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

        if (string.IsNullOrWhiteSpace(card.RecipientEmail))
        {
            return "Nothing was sent: the card has no recipient email. Set one with updateGiftCard first.";
        }

        if (!await services.GetRequiredService<GiftCardEmailSender>().SendAsync(card, message: null, cancellationToken))
        {
            return "Nothing was sent: the gift card couldn't be emailed. Check the site's email settings.";
        }

        return Json(new
        {
            id = card.ItemId,
            codeEndsWith = card.LastCharacters,
            sentTo = card.RecipientEmail,
            message = $"The gift card was emailed to {card.RecipientEmail}.",
        });
    }
}
