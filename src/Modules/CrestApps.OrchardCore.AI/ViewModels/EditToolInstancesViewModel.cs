namespace CrestApps.OrchardCore.AI.ViewModels;

/// <summary>
/// Represents the view model used to select the AI tool instances available to a resource.
/// </summary>
public class EditToolInstancesViewModel
{
    /// <summary>
    /// Gets or sets the selectable tool instances.
    /// </summary>
    public ToolInstanceEntry[] Instances { get; set; }
}
