namespace CrestApps.OrchardCore.AI.ViewModels;

/// <summary>
/// Represents the "New AI profile" picker shown over the AI profiles list.
/// </summary>
public class ProfileScenarioPickerViewModel
{
    /// <summary>
    /// The view data key the AI profiles list passes the picker under.
    /// </summary>
    public const string ViewDataKey = nameof(ProfileScenarioPickerViewModel);

    /// <summary>
    /// Gets or sets the starting points, featured scenarios first and already sorted so that grouping them by
    /// category keeps each category's scenarios in order.
    /// </summary>
    public IList<ProfileScenarioCardViewModel> Scenarios { get; set; } = [];
}
