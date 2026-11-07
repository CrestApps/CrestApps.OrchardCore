namespace CrestApps.OrchardCore.ContactCenter.AI;

/// <summary>
/// How sure a tool is that a finding is the reason, most sure first. A finding the tool read straight from a record
/// is <see cref="Certain"/>; one that follows from the records is <see cref="Likely"/>; one that only fits is
/// <see cref="Possible"/>; one that explains nothing by itself but belongs in the picture is
/// <see cref="Informational"/>.
/// </summary>
internal static class Likelihood
{
    public const string Certain = "certain";

    public const string Likely = "likely";

    public const string Possible = "possible";

    public const string Informational = "informational";

    /// <summary>
    /// Gets the sort rank of a likelihood: the lower, the more likely.
    /// </summary>
    /// <param name="likelihood">The likelihood.</param>
    /// <returns>The rank.</returns>
    public static int Rank(string likelihood)
        => likelihood switch
        {
            Certain => 0,
            Likely => 1,
            Possible => 2,
            _ => 3,
        };
}

/// <summary>
/// One thing a tool found, stated in plain words with the facts it rests on.
/// </summary>
/// <param name="Code">A stable name for the kind of finding, which a model or a test can match on.</param>
/// <param name="Likelihood">How sure the tool is that this is the reason; one of <see cref="AI.Likelihood"/>.</param>
/// <param name="Summary">What was found, in a sentence a manager can read.</param>
/// <param name="Facts">The records the finding rests on, or <see langword="null"/>.</param>
internal sealed record ToolFinding(string Code, string Likelihood, string Summary, object Facts = null)
{
    /// <summary>
    /// Orders findings most likely first, keeping the order they were found in among equals.
    /// </summary>
    /// <param name="findings">The findings.</param>
    /// <returns>The findings, most likely first.</returns>
    public static IReadOnlyList<ToolFinding> Order(IEnumerable<ToolFinding> findings)
        => [.. findings.Select((finding, index) => (finding, index))
            .OrderBy(entry => AI.Likelihood.Rank(entry.finding.Likelihood))
            .ThenBy(entry => entry.index)
            .Select(entry => entry.finding)];
}
