namespace CrestApps.OrchardCore.Numbering;

/// <summary>
/// Why a <see cref="NumberSeriesFormat"/> cannot be used.
/// </summary>
public enum NumberSeriesFormatError
{
    /// <summary>
    /// The format can be used.
    /// </summary>
    None,

    /// <summary>
    /// The pattern is empty.
    /// </summary>
    Empty,

    /// <summary>
    /// The pattern is longer than <see cref="NumberSeriesFormat.MaximumPatternLength"/>.
    /// </summary>
    TooLong,

    /// <summary>
    /// The pattern has no <c>{seq}</c> token, so every number would be the same.
    /// </summary>
    MissingSequence,

    /// <summary>
    /// The pattern has more than one <c>{seq}</c> token.
    /// </summary>
    SeveralSequences,

    /// <summary>
    /// The pattern has a token that is not known, or a brace that is not closed.
    /// </summary>
    UnknownToken,

    /// <summary>
    /// The numbers start again each year or month, but the pattern has no year token, so numbers would repeat.
    /// </summary>
    ResetNeedsYear,

    /// <summary>
    /// The numbers start again each month, but the pattern has no month token, so numbers would repeat.
    /// </summary>
    ResetNeedsMonth,

    /// <summary>
    /// The first number is less than 1.
    /// </summary>
    FirstNumberTooLow,
}
