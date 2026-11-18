using System.Text.Json.Serialization;

namespace CrestApps.OrchardCore.Records;

/// <summary>
/// A reference to a record of any module: an open kind (such as <c>Order</c> or <c>Asset</c>, named by the module that
/// keeps the records) and the record's id in that kind. Use it where a record points at "some other record" (the
/// subject of a note, an attachment's owner, a record linked to a ticket) instead of one id property per module.
/// A person or an organization a record belongs to is a party reference, not a record reference. It is stored as the
/// text <c>Kind:Id</c>.
/// </summary>
/// <param name="Kind">The kind of record, as the <see cref="IRecordSource"/> of its module names it.</param>
/// <param name="Id">The record's id in its kind.</param>
[JsonConverter(typeof(RecordRefJsonConverter))]
public readonly record struct RecordRef(string Kind, string Id)
{
    /// <summary>
    /// Gets whether the reference names no record.
    /// </summary>
    [JsonIgnore]
    public bool IsEmpty => string.IsNullOrEmpty(Kind) || string.IsNullOrEmpty(Id);

    /// <summary>
    /// Formats the reference as <c>Kind:Id</c>, the form <see cref="TryParse"/> reads.
    /// </summary>
    /// <returns>The text.</returns>
    public override string ToString()
        => IsEmpty ? string.Empty : $"{Kind}:{Id}";

    /// <summary>
    /// Reads a reference written as <c>Kind:Id</c>.
    /// </summary>
    /// <param name="value">The text.</param>
    /// <param name="record">The reference.</param>
    /// <returns><see langword="true"/> when the text holds a kind and an id.</returns>
    public static bool TryParse(string value, out RecordRef record)
    {
        record = default;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var separator = value.IndexOf(':', StringComparison.Ordinal);

        if (separator <= 0 || separator == value.Length - 1)
        {
            return false;
        }

        record = new RecordRef(value[..separator], value[(separator + 1)..]);

        return true;
    }
}
