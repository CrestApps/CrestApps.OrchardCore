namespace CrestApps.OrchardCore.Surveys.Core.Services;

/// <summary>
/// Splits a list of ids into the chunks one <c>IN</c> list may hold. SQL Server refuses a statement with more than 2,100
/// parameters, so every query of Surveys that names ids it didn't choose itself (every readable survey, every question a
/// bank draw matches) reads them a chunk at a time.
/// </summary>
public static class SurveyIdChunks
{
    /// <summary>
    /// The most ids one <c>IN</c> list holds, leaving room for the statement's other parameters.
    /// </summary>
    public const int Size = 1000;

    /// <summary>
    /// Splits ids into chunks of at most <see cref="Size"/>, without empty ids or repeats, keeping their order.
    /// </summary>
    /// <param name="ids">The ids, or <see langword="null"/>.</param>
    /// <returns>The chunks; none when there is no id.</returns>
    public static IEnumerable<string[]> Of(IEnumerable<string> ids)
        => (ids ?? [])
            .Where(id => !string.IsNullOrEmpty(id))
            .Distinct(StringComparer.Ordinal)
            .Chunk(Size);

    /// <summary>
    /// Gets whether ids fit in one <c>IN</c> list.
    /// </summary>
    /// <param name="count">How many ids.</param>
    /// <returns><see langword="true"/> when they fit.</returns>
    public static bool FitsOneList(int count)
        => count <= Size;
}
