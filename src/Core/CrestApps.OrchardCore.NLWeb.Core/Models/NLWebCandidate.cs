namespace CrestApps.OrchardCore.NLWeb.Models;

/// <summary>
/// An item a retriever found for a query, before it is loaded, checked and described.
/// </summary>
public sealed class NLWebCandidate
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NLWebCandidate"/> class.
    /// </summary>
    /// <param name="id">The identifier of the item, such as a content item identifier.</param>
    /// <param name="score">How well the item matches, when the retriever knows. Higher is better.</param>
    /// <param name="source">The name of the retriever that found the item.</param>
    public NLWebCandidate(
        string id,
        double? score,
        string source)
    {
        Id = id;
        Score = score;
        Source = source;
    }

    /// <summary>
    /// Gets the identifier of the item.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Gets how well the item matches, when known. Higher is better.
    /// </summary>
    public double? Score { get; }

    /// <summary>
    /// Gets the name of the retriever that found the item.
    /// </summary>
    public string Source { get; }
}
