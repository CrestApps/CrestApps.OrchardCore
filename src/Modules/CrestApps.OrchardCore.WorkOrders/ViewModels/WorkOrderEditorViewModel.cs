namespace CrestApps.OrchardCore.WorkOrders.ViewModels;

/// <summary>
/// The work order editor page: what the page shows around the editor shape the display drivers of the work order build.
/// </summary>
public class WorkOrderEditorViewModel
{
    /// <summary>
    /// Gets or sets the id of the work order, or <see langword="null"/> for a new one.
    /// </summary>
    public string WorkOrderId { get; set; }

    /// <summary>
    /// Gets or sets the type of the work order.
    /// </summary>
    public string TypeId { get; set; }

    /// <summary>
    /// Gets or sets the name of the type.
    /// </summary>
    public string TypeName { get; set; }

    /// <summary>
    /// Gets or sets the number of the work order, or <see langword="null"/> before it is created.
    /// </summary>
    public string Number { get; set; }

    /// <summary>
    /// Gets or sets the editor shape.
    /// </summary>
    public object Editor { get; set; }
}
