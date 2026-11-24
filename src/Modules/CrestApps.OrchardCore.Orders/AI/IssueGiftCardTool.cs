using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Api;
using CrestApps.OrchardCore.Calendars;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Orders.Services;
using CrestApps.OrchardCore.Payments;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using YesSqlSession = YesSql.ISession;

namespace CrestApps.OrchardCore.Orders.AI;

/// <summary>
/// Issues a gift card as the Create form of Commerce › Gift cards does: the form's checks
/// (<see cref="GiftCardIssueRules"/>), then <see cref="GiftCardService.IssueAsync"/> with a code made up by the site,
/// then, when asked, <see cref="GiftCardEmailSender.SendAsync"/>. It creates money the holder can spend, so the value
/// and currency are always given, the value must fit the currency's decimal places, and every call carries an
/// idempotency key (<see cref="IdempotentOperation"/>) so a retry never issues a second card. No own code is taken and
/// the code is never returned.
/// </summary>
internal sealed class IssueGiftCardTool : GiftCardAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "issueGiftCard";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "amount": {
          "type": "number",
          "description": "The card's value, more than zero, in currency."
        },
        "currency": {
          "type": "string",
          "description": "The three-letter currency of the card, such as USD. It is spent only at checkouts in that currency."
        },
        "expires": {
          "type": "string",
          "description": "When the card expires, as a date (yyyy-MM-dd) or date and time in the site's time zone. Left out, it never expires."
        },
        "recipientName": {
          "type": "string",
          "description": "Who the card is for."
        },
        "recipientEmail": {
          "type": "string",
          "description": "The recipient's email, needed to email the card."
        },
        "note": {
          "type": "string",
          "description": "An internal note kept with the card."
        },
        "sendEmail": {
          "type": "boolean",
          "description": "Set to true to email the card, with its code, to the recipient."
        },
        "message": {
          "type": "string",
          "description": "A message put in the email."
        },
        "idempotencyKey": {
          "type": "string",
          "description": "A new unique key for this card, up to 128 characters, such as giftcard-maria-apology. A second call with the same key is refused, so a retry never issues two cards."
        }
      },
      "required": ["amount", "currency", "idempotencyKey"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Creates a gift card worth an amount in a currency, with an optional expiry, recipient and note, and optionally emails it to the recipient with a message. It creates money the holder can spend at checkout. The site makes up the code, which is never shown here; every call needs a new idempotency key.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        var key = CheckKey(ReadString(arguments, "idempotencyKey"));

        if (key is null)
        {
            return $"Nothing was issued: give a new idempotencyKey of up to {MaxKeyLength} characters for this card.";
        }

        if (!TryReadDecimal(arguments, "amount", out var amount))
        {
            return "Nothing was issued: amount must be a number.";
        }

        var currency = ReadString(arguments, "currency")?.ToUpperInvariant();
        DateTime? expiresUtc = null;

        if (ReadString(arguments, "expires") is { } expires)
        {
            if (!DateTime.TryParse(expires, CultureInfo.InvariantCulture, DateTimeStyles.None, out var local))
            {
                return "Nothing was issued: expires must be a date (yyyy-MM-dd) or a date and time.";
            }

            expiresUtc = await services.GetRequiredService<ILocalClock>().ToUtcLenientAsync(DateTime.SpecifyKind(local, DateTimeKind.Unspecified));
        }

        var recipientEmail = ReadString(arguments, "recipientEmail");
        var sendEmail = ReadBool(arguments, "sendEmail") == true;
        var localizer = services.GetRequiredService<IStringLocalizer<IssueGiftCardTool>>();
        var problems = GiftCardIssueRules.Validate(amount, currency, code: null, expiresUtc, sendEmail, recipientEmail, UtcNow(services), localizer)
            .Select(problem => problem.Message.Value)
            .ToList();

        if (problems.Count == 0 && Money.Round(amount.Value, currency) != amount.Value)
        {
            problems.Add($"{Invariant(amount.Value)} has more decimal places than {currency} allows.");
        }

        if (problems.Count > 0)
        {
            return $"Nothing was issued: {string.Join(" ", problems)}";
        }

        var cache = services.GetService<IDistributedCache>();
        var locks = services.GetService<IDistributedLock>();

        if (cache is null || locks is null)
        {
            return "Nothing was issued: the site can't keep idempotency keys right now, so a card issued from a chat can't be guarded against being issued twice. Issue it on the Gift cards page.";
        }

        if (await WriteEarlierWorkAsync(services, cancellationToken) is { } earlierError)
        {
            return earlierError;
        }

        var recipientName = ReadString(arguments, "recipientName");
        var note = ReadString(arguments, "note");
        var message = ReadString(arguments, "message");
        var fingerprint = string.Join('|', Invariant(amount.Value), currency, expiresUtc?.Ticks, recipientName, recipientEmail, note, sendEmail);

        var outcome = await IdempotentOperation.RunAsync(cache, locks, KeyScope(services, TheName, caller), key, fingerprint, KeyLifetime, async token =>
        {
            GiftCard card;

            try
            {
                card = await services.GetRequiredService<GiftCardService>().IssueAsync(
                    amount.Value,
                    currency,
                    localizer["Issued"],
                    created =>
                    {
                        created.ExpiresUtc = expiresUtc;
                        created.RecipientName = recipientName;
                        created.RecipientEmail = recipientEmail;
                        created.Note = note;
                    },
                    code: null,
                    GetActor(caller),
                    token);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // Nothing was committed: the pending card is discarded so it can't be saved when the message ends.
                await services.GetRequiredService<YesSqlSession>().CancelAsync();
                CloseSession(services);

                return new IdempotentOperationAnswer("Nothing was issued: issuing the card stopped with an error. Try again in your next message.", Keep: false);
            }

            try
            {
                if (await CommitCardAsync(services, token) is { } commitError)
                {
                    // Nothing was saved, so the key may be used again.
                    return new IdempotentOperationAnswer(commitError, Keep: false);
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                await services.GetRequiredService<YesSqlSession>().CancelAsync();
                CloseSession(services);

                return new IdempotentOperationAnswer("Saving the gift card stopped with an error, and whether it was issued is unknown. Do not retry with this or another key; list the gift cards in your next message.", Keep: true);
            }

            bool? emailed = null;

            if (sendEmail)
            {
                try
                {
                    emailed = await services.GetRequiredService<GiftCardEmailSender>().SendAsync(card, message, token);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    // The card is issued; a failed email must not let a retry issue another one.
                    emailed = false;
                }
            }

            var result = await DescribeAsync(services, card);
            result["emailed"] = emailed;
            result["message"] = emailed == false
                ? "The gift card was created, but it couldn't be emailed. Check the site's email settings, then use emailGiftCard."
                : emailed == true
                    ? $"The gift card was created and emailed to {card.RecipientEmail}."
                    : "The gift card was created. Its code is on its page on Commerce › Gift cards; it is not shown here.";
            result["adminPath"] = GetAdminPath(services, "GiftCardsEdit", new { action = "Edit", itemId = card.ItemId });

            return new IdempotentOperationAnswer(Json(result), Keep: true);
        }, cancellationToken);

        return outcome.Outcome switch
        {
            IdempotentOperationOutcome.Ran => outcome.Value,
            IdempotentOperationOutcome.Replayed => $"Nothing was issued again: the idempotency key '{key}' was already used for this card. The first call answered: {outcome.Value}",
            IdempotentOperationOutcome.Conflict => $"Nothing was issued: the idempotency key '{key}' was already used for a different card. Use a new key for a new card.",
            _ => $"Nothing was issued: a card with the idempotency key '{key}' is still being issued. List the gift cards in your next message.",
        };
    }

    private static void CloseSession(IServiceProvider services)
    {
        if (services.GetService<OrdersToolScope>() is { } scope)
        {
            scope.SessionClosed = true;
        }
    }
}
