using CrestApps.OrchardCore.Orders.Core.Models;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Orders.Services;

/// <summary>
/// The checks of the Create gift card form on Commerce › Gift cards, shared by the page and the gift card AI tools: a
/// value above zero, a three-letter currency, an own code of 8 to 32 letters and digits, an expiry in the future and a
/// recipient email when the card is emailed.
/// </summary>
internal static class GiftCardIssueRules
{
    /// <summary>
    /// Checks the values of a new gift card.
    /// </summary>
    /// <param name="value">The card's value.</param>
    /// <param name="currency">The card's currency.</param>
    /// <param name="code">The card's own code, or <see langword="null"/> to have one made up.</param>
    /// <param name="expiresUtc">When the card expires, in UTC, or <see langword="null"/>.</param>
    /// <param name="sendEmail">Whether the card is emailed to its recipient.</param>
    /// <param name="recipientEmail">The recipient's email.</param>
    /// <param name="utcNow">The current UTC time.</param>
    /// <param name="S">The localizer of the caller, so each message keeps its translation context.</param>
    /// <returns>Each problem with the name of the form field it belongs to.</returns>
    public static IEnumerable<(string Field, LocalizedString Message)> Validate(
        decimal? value,
        string currency,
        string code,
        DateTime? expiresUtc,
        bool sendEmail,
        string recipientEmail,
        DateTime utcNow,
        IStringLocalizer S)
    {
        ArgumentNullException.ThrowIfNull(S);

        if (value is not > 0m)
        {
            yield return ("Value", S["Enter an amount greater than zero."]);
        }

        if (string.IsNullOrWhiteSpace(currency) || currency.Trim().Length != 3)
        {
            yield return ("Currency", S["Enter the three-letter currency code, such as USD."]);
        }

        if (!string.IsNullOrWhiteSpace(code) && GiftCardCodes.Normalize(code)?.Length is < 8 or > 32)
        {
            yield return ("Code", S["A code is 8 to 32 letters and digits. Leave it blank to have one made up."]);
        }

        if (expiresUtc.HasValue && expiresUtc.Value <= utcNow)
        {
            yield return ("Expires", S["The expiry date must be in the future."]);
        }

        if (sendEmail && string.IsNullOrWhiteSpace(recipientEmail))
        {
            yield return ("RecipientEmail", S["Enter the email to send the card to, or clear Email the card."]);
        }
    }
}
