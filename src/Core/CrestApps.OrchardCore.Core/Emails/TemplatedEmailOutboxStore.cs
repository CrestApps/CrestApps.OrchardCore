using CrestApps.OrchardCore.Emails;
using CrestApps.OrchardCore.Records;
using YesSql;

namespace CrestApps.OrchardCore.Core.Emails;

/// <summary>
/// The default <see cref="ITemplatedEmailOutboxStore"/>: removes the outbox's emails through the caller's session.
/// </summary>
public sealed class TemplatedEmailOutboxStore : ITemplatedEmailOutboxStore
{
    private readonly ISession _session;

    /// <summary>
    /// Initializes a new instance of the <see cref="TemplatedEmailOutboxStore"/> class.
    /// </summary>
    /// <param name="session">The caller's session.</param>
    public TemplatedEmailOutboxStore(ISession session)
    {
        _session = session;
    }

    /// <inheritdoc/>
    public async Task<int> DeleteBySourceAsync(RecordRef source, CancellationToken cancellationToken = default)
    {
        if (source.IsEmpty)
        {
            return 0;
        }

        var key = source.ToString();

        if (key.Length > TemplatedEmailOutboxIndexProvider.SourceLength)
        {
            key = key[..TemplatedEmailOutboxIndexProvider.SourceLength];
        }

        var emails = await _session.Query<TemplatedEmailOutboxItem, TemplatedEmailOutboxIndex>(index => index.Source == key, collection: TemplatedEmailOutboxStorage.CollectionName)
            .ListAsync(cancellationToken);

        return Delete(emails.Where(email => email.Source == source));
    }

    /// <inheritdoc/>
    public async Task<int> DeleteByRecipientAsync(string email, CancellationToken cancellationToken = default)
    {
        var recipient = TemplatedEmailOutboxIndexProvider.RecipientOf(email);

        if (recipient is null)
        {
            return 0;
        }

        var emails = await _session.Query<TemplatedEmailOutboxItem, TemplatedEmailOutboxIndex>(index => index.Recipient == recipient, collection: TemplatedEmailOutboxStorage.CollectionName)
            .ListAsync(cancellationToken);

        return Delete(emails);
    }

    private int Delete(IEnumerable<TemplatedEmailOutboxItem> emails)
    {
        var deleted = 0;

        foreach (var email in emails)
        {
            _session.Delete(email, TemplatedEmailOutboxStorage.CollectionName);
            deleted++;
        }

        return deleted;
    }
}
