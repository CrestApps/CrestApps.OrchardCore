namespace CrestApps.OrchardCore.Wizard;

/// <summary>
/// The context raised after a wizard session has been loaded for the current step.
/// </summary>
public sealed class WizardFlowLoadedContext : WizardFlowContextBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WizardFlowLoadedContext"/> class.
    /// </summary>
    /// <param name="flow">The active wizard flow.</param>
    public WizardFlowLoadedContext(WizardFlow flow)
        : base(flow)
    {
    }
}
