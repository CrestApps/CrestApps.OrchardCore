namespace CrestApps.OrchardCore.Wizard;

/// <summary>
/// The context raised after a wizard session has been initialized for display.
/// </summary>
public sealed class WizardFlowInitializedContext : WizardFlowContextBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WizardFlowInitializedContext"/> class.
    /// </summary>
    /// <param name="flow">The active wizard flow.</param>
    public WizardFlowInitializedContext(WizardFlow flow)
        : base(flow)
    {
    }
}
