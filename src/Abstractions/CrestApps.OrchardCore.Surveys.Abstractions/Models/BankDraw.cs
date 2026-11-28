namespace CrestApps.OrchardCore.Surveys.Models;

/// <summary>
/// A query of a question bank that adds candidates to a pool.
/// </summary>
public sealed class BankDraw
{
    /// <summary>
    /// Gets or sets the id of the question bank.
    /// </summary>
    public string BankId { get; set; }

    /// <summary>
    /// Gets or sets the tags a question must all have; empty for any.
    /// </summary>
    public List<string> Tags { get; set; } = [];

    /// <summary>
    /// Gets or sets the difficulty a question must have, or null for any.
    /// </summary>
    public int? Difficulty { get; set; }

    /// <summary>
    /// Gets or sets the most candidates the query adds, or null for the default.
    /// </summary>
    public int? MaxCandidates { get; set; }
}
