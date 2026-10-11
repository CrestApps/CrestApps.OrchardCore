namespace CrestApps.OrchardCore.Wizard;

/// <summary>
/// The context raised before a wizard session is loaded for the current step.
/// </summary>
public sealed class WizardFlowLoadingContext : WizardFlowContextBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WizardFlowLoadingContext"/> class.
    /// </summary>
    /// <param name="flow">The active wizard flow.</param>
    public WizardFlowLoadingContext(WizardFlow flow)
        : base(flow)
    {
    }
}
