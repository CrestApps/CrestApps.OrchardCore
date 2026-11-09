namespace CrestApps.OrchardCore.PersonalData;

/// <summary>
/// What a privacy erasure changed: how many records of each kind were anonymized.
/// </summary>
public sealed class PersonalDataAnonymizeResult
{
    /// <summary>
    /// Gets the number of records anonymized, by kind.
    /// </summary>
    public IDictionary<string, int> Records { get; init; } = new Dictionary<string, int>(StringComparer.Ordinal);

    /// <summary>
    /// Gets what could not be anonymized and must be done by hand, for the person handling the request.
    /// </summary>
    public IList<string> Warnings { get; init; } = [];

    /// <summary>
    /// Gets the number of records anonymized, of every kind.
    /// </summary>
    public int Total
        => Records.Values.Sum();

    /// <summary>
    /// Counts records of a kind as anonymized.
    /// </summary>
    /// <param name="kind">The kind of record.</param>
    /// <param name="count">How many records were anonymized.</param>
    public void Count(string kind, int count = 1)
    {
        ArgumentException.ThrowIfNullOrEmpty(kind);

        if (count <= 0)
        {
            return;
        }

        Records[kind] = Records.TryGetValue(kind, out var current) ? current + count : count;
    }
}
