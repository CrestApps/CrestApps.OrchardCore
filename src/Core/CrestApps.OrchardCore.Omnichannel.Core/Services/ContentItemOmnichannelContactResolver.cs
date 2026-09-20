using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.Core.Omnichannel.Models;
using CrestApps.Core.Omnichannel.Services;
using OrchardCore.ContentManagement;
using YesSql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Omnichannel.Core.Services;

/// <summary>
/// Finds Orchard Core contact content items and reports them as contacts.
/// </summary>
public sealed class ContentItemOmnichannelContactResolver : IOmnichannelContactResolver
{
    private readonly IContentManager _contentManager;
    private readonly ISession _session;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContentItemOmnichannelContactResolver"/> class.
    /// </summary>
    /// <param name="contentManager">The content manager.</param>
    /// <param name="session">The session, used for the phone and email lookups.</param>
    public ContentItemOmnichannelContactResolver(IContentManager contentManager, ISession session)
    {
        _contentManager = contentManager;
        _session = session;
    }

    /// <inheritdoc/>
    public async Task<OmnichannelContact> FindByIdAsync(string contactId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(contactId))
        {
            return null;
        }

        // The latest version rather than the published one: an opt-out recorded during a conversation
        // has to be visible to the next message even before anybody publishes the contact.
        var contentItem = await _contentManager.GetAsync(contactId, VersionOptions.Latest);

        return ContentItemOmnichannelContactProjection.Project(contentItem);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<OmnichannelContact>> FindByPhoneNumberAsync(string phoneNumber, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
        {
            return [];
        }

        var normalized = phoneNumber.Trim();

        var matches = await _session
            .QueryIndex<OmnichannelContactIndex>(index =>
                index.Latest &&
                (index.NormalizedPrimaryCellPhoneNumber == normalized ||
                 index.NormalizedPrimaryHomePhoneNumber == normalized))
            .ListAsync(cancellationToken);

        return await ProjectAsync(matches.Select(index => index.ContentItemId));
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<OmnichannelContact>> FindByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return [];
        }

        var normalized = email.Trim();

        var matches = await _session
            .QueryIndex<OmnichannelContactIndex>(index => index.Latest && index.PrimaryEmailAddress == normalized)
            .ListAsync(cancellationToken);

        return await ProjectAsync(matches.Select(index => index.ContentItemId));
    }

    /// <summary>
    /// Loads and projects the contacts behind a set of content item identifiers.
    /// </summary>
    /// <param name="contentItemIds">The identifiers.</param>
    /// <returns>The contacts that still exist.</returns>
    private async Task<IReadOnlyCollection<OmnichannelContact>> ProjectAsync(IEnumerable<string> contentItemIds)
    {
        var contacts = new List<OmnichannelContact>();

        foreach (var contentItemId in contentItemIds.Distinct(StringComparer.Ordinal))
        {
            var contact = ContentItemOmnichannelContactProjection.Project(
                await _contentManager.GetAsync(contentItemId, VersionOptions.Latest));

            if (contact is not null)
            {
                contacts.Add(contact);
            }
        }

        return contacts;
    }
}
