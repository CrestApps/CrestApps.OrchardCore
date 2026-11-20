namespace CrestApps.OrchardCore.Numbering;

/// <summary>
/// A number issued by an <see cref="INumberSeries"/>.
/// </summary>
/// <param name="Sequence">The number in its series.</param>
/// <param name="Text">The number as the format of the series writes it, such as <c>INV-2026-0042</c>.</param>
public readonly record struct SeriesNumber(long Sequence, string Text);
