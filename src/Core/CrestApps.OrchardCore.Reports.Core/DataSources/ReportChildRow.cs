namespace CrestApps.OrchardCore.Reports.Designer.DataSources;

/// <summary>
/// One row of a data set whose rows are the entries a document holds, such as a line of an order: the entry, and the
/// document it belongs to, so the row can read both.
/// </summary>
/// <typeparam name="TParent">The type of the document.</typeparam>
/// <typeparam name="TChild">The type of the entry.</typeparam>
public sealed class ReportChildRow<TParent, TChild>
    where TParent : class
    where TChild : class
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ReportChildRow{TParent, TChild}"/> class.
    /// </summary>
    /// <param name="parent">The document the entry belongs to.</param>
    /// <param name="child">The entry.</param>
    /// <param name="position">The one-based position of the entry in the document.</param>
    public ReportChildRow(
        TParent parent,
        TChild child,
        int position)
    {
        Parent = parent;
        Child = child;
        Position = position;
    }

    /// <summary>
    /// Gets the document the entry belongs to.
    /// </summary>
    public TParent Parent { get; }

    /// <summary>
    /// Gets the entry.
    /// </summary>
    public TChild Child { get; }

    /// <summary>
    /// Gets the one-based position of the entry in the document.
    /// </summary>
    public int Position { get; }

    /// <summary>
    /// Gets the rows of the entries a document holds, in their order, skipping missing entries.
    /// </summary>
    /// <param name="parent">The document.</param>
    /// <param name="children">The entries it holds, or <see langword="null"/>.</param>
    /// <returns>The rows.</returns>
    public static IEnumerable<ReportChildRow<TParent, TChild>> For(TParent parent, IEnumerable<TChild> children)
    {
        if (parent is null || children is null)
        {
            yield break;
        }

        var position = 0;

        foreach (var child in children)
        {
            position++;

            if (child is not null)
            {
                yield return new ReportChildRow<TParent, TChild>(parent, child, position);
            }
        }
    }
}
