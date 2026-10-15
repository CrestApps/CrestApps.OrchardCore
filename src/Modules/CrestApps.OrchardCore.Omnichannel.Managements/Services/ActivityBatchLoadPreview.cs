namespace CrestApps.OrchardCore.Omnichannel.Managements.Services;

/// <summary>
/// What an activity load would do if it ran now: how many records match its filters, how many it would load, and why
/// the rest would be skipped. Nothing is created to find this out.
/// </summary>
public sealed class ActivityBatchLoadPreview
{
    /// <summary>
    /// Gets why the load could not be counted, or <see langword="null"/> when it was.
    /// </summary>
    public string Error { get; init; }

    /// <summary>
    /// Gets how many records match the load's filters.
    /// </summary>
    public long Matched { get; init; }

    /// <summary>
    /// Gets how many activities the load would create.
    /// </summary>
    public long WouldLoad { get; init; }

    /// <summary>
    /// Gets how many matching records already have an open activity for the subject.
    /// </summary>
    public long SkippedAsDuplicate { get; init; }

    /// <summary>
    /// Gets how many matching records have asked not to be reached on the channel, themselves or through a shared
    /// number.
    /// </summary>
    public long SkippedAsOptedOut { get; init; }

    /// <summary>
    /// Gets how many matching records have no address on the channel for automated work.
    /// </summary>
    public long SkippedForNoDestination { get; init; }

    /// <summary>
    /// Gets how many matching records have only numbers known not to be in service.
    /// </summary>
    public long SkippedAsNotInService { get; init; }

    /// <summary>
    /// Gets how many matching leads were converted into contacts or share a number with a contact.
    /// </summary>
    public long SkippedAsLead { get; init; }

    /// <summary>
    /// Gets how many matching records are past the load's limit.
    /// </summary>
    public long SkippedByLimit { get; init; }
}
