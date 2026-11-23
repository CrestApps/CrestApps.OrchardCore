using System.Globalization;
using CrestApps.OrchardCore.MailingLists.Mailchimp.Models;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Primitives;

namespace CrestApps.OrchardCore.MailingLists.Mailchimp.Services;

/// <summary>
/// Reads a Mailchimp audience webhook from its <c>application/x-www-form-urlencoded</c> body: <c>type</c>,
/// <c>fired_at</c> and the <c>data[...]</c> values.
/// </summary>
public static class MailchimpWebhookParser
{
    /// <summary>
    /// The event of a person who subscribed, or confirmed Mailchimp's double opt-in e-mail.
    /// </summary>
    public const string Subscribe = "subscribe";

    /// <summary>
    /// The event of a person who unsubscribed, or was deleted by an admin.
    /// </summary>
    public const string Unsubscribe = "unsubscribe";

    /// <summary>
    /// The event of a person whose profile (merge fields) changed.
    /// </summary>
    public const string Profile = "profile";

    /// <summary>
    /// The event of a person whose address changed.
    /// </summary>
    public const string AddressChange = "upemail";

    /// <summary>
    /// The event of an address Mailchimp cleaned because mail to it bounces.
    /// </summary>
    public const string Cleaned = "cleaned";

    /// <summary>
    /// The event of a campaign that was sent or canceled; it changes no member.
    /// </summary>
    public const string Campaign = "campaign";

    private const string MergesPrefix = "data[merges][";

    /// <summary>
    /// Returns whether the site acts on an event type.
    /// </summary>
    /// <param name="type">The event type.</param>
    public static bool IsHandled(string type)
        => type is Subscribe or Unsubscribe or Profile or AddressChange or Cleaned;

    /// <summary>
    /// Reads a webhook body.
    /// </summary>
    /// <param name="body">The form-encoded body.</param>
    /// <returns>The event, or <see langword="null"/> when the body has no <c>type</c>.</returns>
    public static MailchimpWebhookEvent Parse(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        var form = QueryHelpers.ParseQuery(body.Trim());
        var type = Get(form, "type")?.ToLowerInvariant();

        if (string.IsNullOrEmpty(type))
        {
            return null;
        }

        var merges = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (key, value) in form)
        {
            if (!key.StartsWith(MergesPrefix, StringComparison.OrdinalIgnoreCase) || !key.EndsWith(']'))
            {
                continue;
            }

            var tag = key.Substring(MergesPrefix.Length, key.Length - MergesPrefix.Length - 1);

            // Groupings and addresses are nested (data[merges][GROUPINGS][0][name]); only plain fields are kept.
            if (tag.Length == 0 || tag.Contains('[', StringComparison.Ordinal) || tag.Contains(']', StringComparison.Ordinal))
            {
                continue;
            }

            merges[tag] = value.ToString();
        }

        return new MailchimpWebhookEvent
        {
            Type = type,
            FiredUtc = ParseFiredAt(Get(form, "fired_at")),
            AudienceId = Get(form, "data[list_id]"),
            Email = Get(form, "data[email]"),
            OldEmail = Get(form, "data[old_email]"),
            NewEmail = Get(form, "data[new_email]"),
            Action = Get(form, "data[action]")?.ToLowerInvariant(),
            Reason = Get(form, "data[reason]")?.ToLowerInvariant(),
            Merges = merges,
        };
    }

    /// <summary>
    /// Reads <c>fired_at</c> (<c>yyyy-MM-dd HH:mm:ss</c>) as a UTC time.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The time, or <see langword="null"/> when it can't be read.</returns>
    public static DateTime? ParseFiredAt(string value)
    {
        if (DateTime.TryParseExact(
            value?.Trim(),
            "yyyy-MM-dd HH:mm:ss",
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var firedUtc))
        {
            return DateTime.SpecifyKind(firedUtc, DateTimeKind.Utc);
        }

        return null;
    }

    private static string Get(Dictionary<string, StringValues> form, string key)
    {
        if (!form.TryGetValue(key, out var values))
        {
            return null;
        }

        var value = values.ToString().Trim();

        return value.Length == 0 ? null : value;
    }
}
