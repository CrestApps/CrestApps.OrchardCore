using CrestApps.OrchardCore.Omnichannel.Core.Indexes;

namespace CrestApps.OrchardCore.Omnichannel.Core.Services;

/// <summary>
/// Orders the records that share a phone number or address, so a caller or sender is matched to the right one:
/// contacts first, then leads that are still open. A lead that was converted is never matched; the contact it
/// became is. Without leads in the tenant every match is a contact and the order is unchanged.
/// </summary>
public static class OmnichannelContactMatches
{
    /// <summary>
    /// Orders the matches by tier and removes converted leads and duplicates.
    /// </summary>
    /// <param name="matches">The contact index rows that matched.</param>
    /// <param name="leadContentTypes">The lead content types.</param>
    /// <returns>The matched content item ids, contacts first.</returns>
    public static IReadOnlyList<string> Order(IEnumerable<OmnichannelContactIndex> matches, IReadOnlyCollection<string> leadContentTypes)
    {
        var (contacts, leads) = Split(matches, leadContentTypes);

        return contacts.Concat(leads).Distinct(StringComparer.Ordinal).ToArray();
    }

    /// <summary>
    /// Returns the matches of the best tier only: every matching contact, or, when no contact matches, every
    /// matching open lead. A contact and a lead at one number therefore resolve to the contact alone, while two
    /// contacts at one number still come back as two.
    /// </summary>
    /// <param name="matches">The contact index rows that matched.</param>
    /// <param name="leadContentTypes">The lead content types.</param>
    /// <returns>The matched content item ids of the best tier.</returns>
    public static IReadOnlyList<string> BestTier(IEnumerable<OmnichannelContactIndex> matches, IReadOnlyCollection<string> leadContentTypes)
    {
        var (contacts, leads) = Split(matches, leadContentTypes);

        return (contacts.Count > 0 ? contacts : leads).Distinct(StringComparer.Ordinal).ToArray();
    }

    private static (List<string> Contacts, List<string> Leads) Split(IEnumerable<OmnichannelContactIndex> matches, IReadOnlyCollection<string> leadContentTypes)
    {
        var contacts = new List<string>();
        var leads = new List<string>();

        if (matches is null)
        {
            return (contacts, leads);
        }

        var leadTypes = leadContentTypes as ISet<string> ?? new HashSet<string>(leadContentTypes ?? [], StringComparer.Ordinal);

        foreach (var match in matches)
        {
            if (match is null || string.IsNullOrEmpty(match.ContentItemId) || match.IsConverted)
            {
                continue;
            }

            // A row written before the content type was indexed has none; it predates leads, so it is a contact.
            if (!string.IsNullOrEmpty(match.ContentType) && leadTypes.Contains(match.ContentType))
            {
                leads.Add(match.ContentItemId);
            }
            else
            {
                contacts.Add(match.ContentItemId);
            }
        }

        return (contacts, leads);
    }
}
