using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Learning.Assessments.Models;

/// <summary>
/// The items of an ordering question, in their right order.
/// </summary>
public sealed class OrderingQuestionPart : ContentPart
{
    /// <summary>
    /// Gets or sets the items, in their right order.
    /// </summary>
    public IList<ChoiceOption> Items { get; set; } = [];

    /// <summary>
    /// Gets or sets whether each item in its right place earns its share of the points.
    /// </summary>
    public bool PartialCredit { get; set; }
}
