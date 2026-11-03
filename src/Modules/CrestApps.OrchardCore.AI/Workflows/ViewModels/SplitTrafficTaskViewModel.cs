namespace CrestApps.OrchardCore.AI.Workflows.ViewModels;

/// <summary>
/// Edits the Split Traffic task.
/// </summary>
public class SplitTrafficTaskViewModel
{
    /// <summary>
    /// Gets or sets the outcomes and their weights, one <c>name: weight</c> per line.
    /// </summary>
    public string Variants { get; set; }

    /// <summary>
    /// Gets or sets the Liquid expression for the key that keeps a customer on the same outcome.
    /// </summary>
    public string StickyKey { get; set; }
}
