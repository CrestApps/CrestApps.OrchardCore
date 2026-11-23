using System.Text.RegularExpressions;
using CrestApps.Core;
using CrestApps.OrchardCore.MailingLists.Mailchimp.Models;
using CrestApps.OrchardCore.MailingLists.Models;

namespace CrestApps.OrchardCore.MailingLists.Mailchimp.Services;

/// <summary>
/// Reads the Mailchimp settings a mailing list keeps in its properties.
/// </summary>
public static partial class MailchimpListSettingsExtensions
{
    /// <summary>
    /// Returns whether the list is kept in Mailchimp.
    /// </summary>
    /// <param name="list">The list.</param>
    public static bool IsMailchimpList(this MailingList list)
    {
        ArgumentNullException.ThrowIfNull(list);

        return string.Equals(list.ProviderName, MailchimpConstants.ProviderName, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Returns the list's Mailchimp settings, or new settings with their defaults when none are saved.
    /// </summary>
    /// <param name="list">The list.</param>
    public static MailchimpListSettings GetMailchimpSettings(this MailingList list)
    {
        ArgumentNullException.ThrowIfNull(list);

        return list.TryGet<MailchimpListSettings>(out var settings) && settings is not null
            ? settings
            : new MailchimpListSettings();
    }

    /// <summary>
    /// Returns whether a value can be a Mailchimp merge tag: up to 10 capital letters, digits and underscores.
    /// </summary>
    /// <param name="tag">The value.</param>
    public static bool IsValidMergeTag(string tag)
        => !string.IsNullOrEmpty(tag) && MergeTagPattern().IsMatch(tag);

    /// <summary>
    /// Returns whether a value can be a Mailchimp audience id: up to 50 letters and digits.
    /// </summary>
    /// <param name="audienceId">The value.</param>
    public static bool IsValidAudienceId(string audienceId)
        => !string.IsNullOrEmpty(audienceId) && AudienceIdPattern().IsMatch(audienceId);

    [GeneratedRegex("^[A-Z0-9_]{1,10}$", RegexOptions.CultureInvariant)]
    private static partial Regex MergeTagPattern();

    [GeneratedRegex("^[A-Za-z0-9]{1,50}$", RegexOptions.CultureInvariant)]
    private static partial Regex AudienceIdPattern();
}
