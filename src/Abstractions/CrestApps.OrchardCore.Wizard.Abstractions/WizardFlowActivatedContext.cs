namespace CrestApps.OrchardCore.Wizard;

/// <summary>
/// The context raised after a new wizard session has been activated.
/// </summary>
public sealed class WizardFlowActivatedContext : WizardFlowContextBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WizardFlowActivatedContext"/> class.
    /// </summary>
    /// <param name="flow">The active wizard flow.</param>
    public WizardFlowActivatedContext(WizardFlow flow)
        : base(flow)
    {
    }
}
