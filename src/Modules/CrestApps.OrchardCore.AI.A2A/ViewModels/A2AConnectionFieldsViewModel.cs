using CrestApps.OrchardCore.AI.Core.ViewModels;

namespace CrestApps.OrchardCore.AI.A2A.ViewModels;

/// <summary>
/// Represents the view model for A2A connection fields: the agent's title, endpoint and authentication.
/// </summary>
public class A2AConnectionFieldsViewModel : ConnectionAuthFieldsViewModel
{
    /// <summary>
    /// Gets or sets the display text.
    /// </summary>
    public string DisplayText { get; set; }

    /// <summary>
    /// Gets or sets the endpoint.
    /// </summary>
    public string Endpoint { get; set; }
}
