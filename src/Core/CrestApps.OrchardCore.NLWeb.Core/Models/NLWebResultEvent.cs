namespace CrestApps.OrchardCore.NLWeb.Models;

/// <summary>
/// One result of the answer.
/// </summary>
public sealed class NLWebResultEvent : NLWebEvent
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NLWebResultEvent"/> class.
    /// </summary>
    /// <param name="index">The position of the result, from zero.</param>
    /// <param name="result">The result.</param>
    public NLWebResultEvent(
        int index,
        NLWebResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        Index = index;
        Result = result;
    }

    /// <summary>
    /// Gets the position of the result, from zero.
    /// </summary>
    public int Index { get; }

    /// <summary>
    /// Gets the result.
    /// </summary>
    public NLWebResult Result { get; }
}
