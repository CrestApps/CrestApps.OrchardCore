using CrestApps.OrchardCore.Omnichannel.Core.Models;

namespace CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;

/// <summary>
/// Represents the view model for editing a <see cref="Cadence"/>.
/// </summary>
public class CadenceViewModel
{
    /// <summary>
    /// Gets or sets the display text (name).
    /// </summary>
    public string DisplayText { get; set; }

    /// <summary>
    /// Gets or sets the description.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the schedule is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the ordered nudge steps. Model-bound from the repeatable rows in the editor.
    /// </summary>
    public IList<CadenceStep> Steps { get; set; } = [];
}
