using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Channels;
using CrestApps.OrchardCore.Omnichannel.Messaging.ViewModels;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Records;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Services;

/// <summary>
/// Finds contacts to message on a channel, for the composer. Matching by name is the same for every channel; the
/// channel adds matching by address and says which address of each contact to message.
/// </summary>
public sealed class MessagingContactSearch
{
    private const int MaxResults = 20;

    private readonly IMessagingChannelResolver _channelResolver;
    private readonly IOmnichannelContactTypeProvider _contactTypeProvider;
    private readonly IContentManager _contentManager;
    private readonly ISession _session;

    public MessagingContactSearch(
        IMessagingChannelResolver channelResolver,
        IOmnichannelContactTypeProvider contactTypeProvider,
        IContentManager contentManager,
        ISession session)
    {
        _channelResolver = channelResolver;
        _contactTypeProvider = contactTypeProvider;
        _contentManager = contentManager;
        _session = session;
    }

    /// <summary>
    /// Searches contacts by name, or by address on the channel, and returns those reachable on the channel.
    /// </summary>
    /// <param name="term">The term the user typed.</param>
    /// <param name="channelName">The channel the message will be sent on.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The matching contacts, each with the address to message them at.</returns>
    public async Task<IReadOnlyList<ContactSearchResult>> SearchAsync(string term, string channelName, CancellationToken cancellationToken)
    {
        var channel = _channelResolver.Get(channelName);

        if (channel is null || string.IsNullOrWhiteSpace(term) || term.Trim().Length < 2)
        {
            return [];
        }

        term = term.Trim();

        // A contact is a content item whose type carries the Omnichannel Contact part. Scope the search to those
        // content types rather than relying on a contact-index join, which only exists once a contact is indexed.
        var contactTypes = (await _contactTypeProvider.GetContactContentTypesAsync(cancellationToken)).ToArray();

        if (contactTypes.Length == 0)
        {
            return [];
        }

        var hits = new Dictionary<string, ContentItem>(StringComparer.OrdinalIgnoreCase);

        // Name matches: the contact's display text (the tenant's title, e.g. first + last name).
        foreach (var item in await _session.Query<ContentItem, ContentItemIndex>(index =>
                    index.Latest && index.ContentType.IsIn(contactTypes) && index.DisplayText.Contains(term))
                .Take(MaxResults)
                .ListAsync(cancellationToken))
        {
            hits[item.ContentItemId] = item;
        }

        // Address matches, as the channel understands its addresses.
        var addressMatches = (await channel.SearchContactIdsByAddressAsync(term, contactTypes, MaxResults, cancellationToken))
            .Where(id => !hits.ContainsKey(id))
            .ToArray();

        if (addressMatches.Length > 0)
        {
            foreach (var item in await _contentManager.GetAsync(addressMatches, VersionOptions.Latest))
            {
                hits[item.ContentItemId] = item;
            }
        }

        var results = new List<ContactSearchResult>();

        foreach (var item in hits.Values.Take(MaxResults))
        {
            // A contact with no address on this channel cannot be messaged on it, so it is not offered.
            var addresses = channel.GetContactAddresses(item);
            var address = addresses.Count > 0 ? addresses[0] : null;

            if (string.IsNullOrEmpty(address))
            {
                continue;
            }

            var displayAddress = channel.FormatAddress(address);

            results.Add(new ContactSearchResult
            {
                Id = item.ContentItemId,
                Name = string.IsNullOrEmpty(item.DisplayText) ? displayAddress : item.DisplayText,
                Address = address,
                DisplayAddress = displayAddress,
            });
        }

        return results;
    }
}
