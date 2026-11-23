namespace CrestApps.OrchardCore.Marketing.Models;

/// <summary>
/// Who a <see cref="Campaign"/> goes to.
/// </summary>
public static class CampaignAudienceKinds
{
    /// <summary>
    /// The customers of a segment who accept marketing; the audience of a campaign that names none.
    /// </summary>
    public const string Segment = "Segment";

    /// <summary>
    /// The subscribed members of a mailing list, while the Mailing Lists feature is on.
    /// </summary>
    public const string MailingList = "MailingList";

    /// <summary>
    /// Returns whether an audience kind is <see cref="MailingList"/>; any other value, an empty one included, is a
    /// segment.
    /// </summary>
    /// <param name="kind">The campaign's audience kind.</param>
    /// <returns><see langword="true"/> when the campaign goes to a mailing list.</returns>
    public static bool IsMailingList(string kind)
        => string.Equals(kind, MailingList, StringComparison.OrdinalIgnoreCase);
}
