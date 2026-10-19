namespace CrestApps.OrchardCore.Omnichannel.Core.Indexes;

/// <summary>
/// An index row that names a contact or lead found by one of its phone numbers or addresses, with what is needed to
/// rank it against the other records found the same way.
/// </summary>
public interface IOmnichannelContactMatch
{
    /// <summary>
    /// Gets the content item id of the record.
    /// </summary>
    string ContentItemId { get; }

    /// <summary>
    /// Gets the content type of the record, or <see langword="null"/> on a row written before it was indexed.
    /// </summary>
    string ContentType { get; }

    /// <summary>
    /// Gets whether the record is a lead that was converted.
    /// </summary>
    bool IsConverted { get; }
}
