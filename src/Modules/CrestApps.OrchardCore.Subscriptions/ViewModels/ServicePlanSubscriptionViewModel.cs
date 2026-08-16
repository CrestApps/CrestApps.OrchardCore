using Microsoft.AspNetCore.Mvc.ModelBinding;
using OrchardCore.DisplayManagement;

namespace CrestApps.OrchardCore.Subscriptions.ViewModels;

/// <summary>
/// Represents the service plan content and session state for a subscription step.
/// </summary>
public class ServicePlanSubscriptionViewModel
{
    /// <summary>
    /// Gets or sets the content item identifier of the selected service plan.
    /// </summary>
    public string ContentItemId { get; set; }

    /// <summary>
    /// Gets or sets the subscription session identifier.
    /// </summary>
    public string SessionId { get; set; }

    /// <summary>
    /// Gets or sets the current subscription flow step.
    /// </summary>
    public string Step { get; set; }

    /// <summary>
    /// Gets or sets the rendered service plan content shape.
    /// </summary>
    [BindNever]
    public IShape Content { get; set; }
}
