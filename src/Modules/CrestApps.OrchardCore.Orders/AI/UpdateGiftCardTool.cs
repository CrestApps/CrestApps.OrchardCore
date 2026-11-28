using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Calendars;
using CrestApps.OrchardCore.Orders.Core.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Orders.AI;

/// <summary>
/// Changes a gift card's enabled flag, expiry, recipient and note, as the card's page on Commerce › Gift cards does
/// (<see cref="IGiftCardStore.UpdateAsync"/>); only the fields given change. The balance is changed only by
/// <c>adjustGiftCardBalance</c>. Committed at once, so a checkout spending the card at the same moment is reported.
/// </summary>
internal sealed class UpdateGiftCardTool : GiftCardAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "updateGiftCard";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "giftCardId": {
          "type": "string",
          "description": "The card's id, from listGiftCards."
        },
        "isEnabled": {
          "type": "boolean",
          "description": "false to disable the card so it can't be spent, true to enable it again."
        },
        "expires": {
          "type": "string",
          "description": "The new expiry, as a date (yyyy-MM-dd) or date and time in the site's time zone, or an empty text for no expiry."
        },
        "recipientName": {
          "type": "string",
          "description": "The recipient's name; an empty text clears it."
        },
        "recipientEmail": {
          "type": "string",
          "description": "The recipient's email; an empty text clears it."
        },
        "note": {
          "type": "string",
          "description": "The internal note; an empty text clears it."
        }
      },
      "required": ["giftCardId"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Changes a gift card's enabled flag, expiry, recipient name and email, and note. It does not change the balance.";

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

        // Every argument is read and checked before the card, which the session tracks, is changed.
        var hasExpires = arguments.ContainsKey("expires");
        DateTime? expiresUtc = null;

        if (hasExpires && ReadString(arguments, "expires") is { } expires)
        {
            if (!DateTime.TryParse(expires, CultureInfo.InvariantCulture, DateTimeStyles.None, out var local))
            {
                return "Nothing was changed: expires must be a date (yyyy-MM-dd), a date and time, or an empty text for no expiry.";
            }

            expiresUtc = await services.GetRequiredService<ILocalClock>().ToUtcLenientAsync(DateTime.SpecifyKind(local, DateTimeKind.Unspecified));
        }

        var changed = new List<string>();

        if (ReadBool(arguments, "isEnabled") is { } enabled)
        {
            card.IsEnabled = enabled;
            changed.Add("isEnabled");
        }

        if (hasExpires)
        {
            card.ExpiresUtc = expiresUtc;
            changed.Add("expires");
        }

        foreach (var (name, apply) in new (string, Action<string>)[]
        {
            ("recipientName", value => card.RecipientName = value),
            ("recipientEmail", value => card.RecipientEmail = value),
            ("note", value => card.Note = value),
        })
        {
            if (arguments.ContainsKey(name))
            {
                apply(ReadString(arguments, name));
                changed.Add(name);
            }
        }

        if (changed.Count == 0)
        {
            return "Nothing was changed: give at least one of isEnabled, expires, recipientName, recipientEmail or note.";
        }

        if (await WriteEarlierWorkAsync(services, cancellationToken) is { } earlierError)
        {
            return earlierError;
        }

        await services.GetRequiredService<IGiftCardStore>().UpdateAsync(card, cancellationToken);

        if (await CommitCardAsync(services, cancellationToken) is { } commitError)
        {
            return commitError;
        }

        var result = await DescribeAsync(services, card);
        result["note"] = card.Note;
        result["changed"] = changed;

        return Json(result);
    }
}
