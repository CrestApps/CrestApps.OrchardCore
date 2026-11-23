using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.OrchardCore.Emails;
using CrestApps.OrchardCore.WorkQueues;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Core.Emails;

/// <summary>
/// Finds the emails of the templated email outbox: the ones due for an attempt, the ones of a record or an address,
/// and the finished ones old enough to remove.
/// </summary>
public sealed class TemplatedEmailOutboxIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets where the email stands on the queue.
    /// </summary>
    public LeasedWorkItemStatus Status { get; set; }

    /// <summary>
    /// Gets or sets when the email can next be claimed.
    /// </summary>
    public DateTime NextAttemptUtc { get; set; }

    /// <summary>
    /// Gets or sets the record the email belongs to, as <c>Kind:Id</c>, or <see langword="null"/>.
    /// </summary>
    public string Source { get; set; }

    /// <summary>
    /// Gets or sets the address the email goes to, normalized.
    /// </summary>
    public string Recipient { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the email was sent or given up, or <see langword="null"/> while it waits.
    /// </summary>
    public DateTime? FinishedUtc { get; set; }
}

/// <summary>
/// Maps <see cref="TemplatedEmailOutboxItem"/> to <see cref="TemplatedEmailOutboxIndex"/> in the outbox's collection.
/// </summary>
public sealed class TemplatedEmailOutboxIndexProvider : IndexProvider<TemplatedEmailOutboxItem>
{
    /// <summary>
    /// The longest record reference the index keeps.
    /// </summary>
    public const int SourceLength = 150;

    /// <summary>
    /// Initializes a new instance of the <see cref="TemplatedEmailOutboxIndexProvider"/> class.
    /// </summary>
    public TemplatedEmailOutboxIndexProvider()
    {
        CollectionName = TemplatedEmailOutboxStorage.CollectionName;
    }

    /// <inheritdoc/>
    public override void Describe(DescribeContext<TemplatedEmailOutboxItem> context)
    {
        context.For<TemplatedEmailOutboxIndex>()
            .ExclusiveToDocumentType()
            .SingleRecordPerDocument()
            .Map(item => new TemplatedEmailOutboxIndex
            {
                ItemId = item.ItemId,
                Status = item.Status,
                NextAttemptUtc = item.NextAttemptUtc,
                Source = SourceOf(item),
                Recipient = RecipientOf(item.To),
                FinishedUtc = item.SentUtc ?? item.AbandonedUtc,
            });
    }

    /// <summary>
    /// Returns the address as the index keeps it.
    /// </summary>
    /// <param name="email">The address.</param>
    /// <returns>The normalized address, cut to the longest an address may be, or <see langword="null"/>.</returns>
    public static string RecipientOf(string email)
    {
        var normalized = EmailAddresses.Normalize(email);

        return normalized?.Length > EmailAddresses.MaxLength ? normalized[..EmailAddresses.MaxLength] : normalized;
    }

    private static string SourceOf(TemplatedEmailOutboxItem item)
    {
        var source = item.Source.ToString();

        if (string.IsNullOrEmpty(source))
        {
            return null;
        }

        return source.Length > SourceLength ? source[..SourceLength] : source;
    }
}
