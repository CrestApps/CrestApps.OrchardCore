namespace CrestApps.OrchardCore.Wizard;

/// <summary>
/// Represents one step in a wizard flow. Steps are contributed by wizard handlers and ordered by
/// <see cref="Order"/>.
/// </summary>
public sealed class WizardStep
{
    /// <summary>
    /// Gets or sets the unique key that identifies the step within the flow.
    /// </summary>
    public string Key { get; set; }

    /// <summary>
    /// Gets or sets the title shown for the step.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets the description shown for the step.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the position used to order the step within the flow.
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the step must store data in
    /// <see cref="IWizardFlowSession.SavedSteps"/> before it is considered complete.
    /// </summary>
    public bool CollectData { get; set; }

    /// <summary>
    /// Gets or sets the lifecycle state of the step within the current session.
    /// </summary>
    public WizardStepState State { get; set; }

    /// <summary>
    /// Gets the custom, step-specific data associated with the step.
    /// </summary>
    public Dictionary<string, object> Data { get; init; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether the step is hidden from rendering.
    /// </summary>
    public bool Conceal { get; set; }
}
