using CrestApps.OrchardCore.ServiceLevels.Core.Models;
using CrestApps.OrchardCore.ServiceLevels.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.ServiceLevels.ViewModels;

/// <summary>
/// The targets card of the policy editor: a grid with a row per priority, a last row for every other priority, and a
/// column per metric of the policy's scope.
/// </summary>
public class ServiceLevelTargetsViewModel
{
    /// <summary>
    /// Gets or sets the metrics, the grid's columns.
    /// </summary>
    [BindNever]
    public IList<ServiceLevelOption> Metrics { get; set; } = [];

    /// <summary>
    /// Gets or sets the priorities, the grid's rows; the last row has an empty key and stands for every other priority.
    /// </summary>
    [BindNever]
    public IList<ServiceLevelOption> Priorities { get; set; } = [];

    /// <summary>
    /// Gets or sets the cells, row by row: for each priority, one cell per metric.
    /// </summary>
    public IList<ServiceLevelTargetCell> Cells { get; set; } = [];
}
