using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Dispatch.ViewModels;

/// <summary>
/// The Technicians card of the work order editor.
/// </summary>
public class WorkOrderTechniciansViewModel
{
    public IList<string> PreferredWorkerIds { get; set; } = [];

    public IList<string> ExcludedWorkerIds { get; set; } = [];

    [BindNever]
    public string Capabilities { get; set; }
}
