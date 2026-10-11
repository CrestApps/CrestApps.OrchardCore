namespace CrestApps.OrchardCore.Wizard;

/// <summary>
/// The context raised before a wizard session is initialized for display.
/// </summary>
public sealed class WizardFlowInitializingContext : WizardFlowContextBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WizardFlowInitializingContext"/> class.
    /// </summary>
    /// <param name="flow">The active wizard flow.</param>
    public WizardFlowInitializingContext(WizardFlow flow)
        : base(flow)
    {
    }
}
