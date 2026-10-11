namespace CrestApps.OrchardCore.Wizard;

/// <summary>
/// The context raised after a wizard has completed and every step has been fulfilled.
/// </summary>
public sealed class WizardFlowCompletedContext : WizardFlowContextBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WizardFlowCompletedContext"/> class.
    /// </summary>
    /// <param name="flow">The active wizard flow.</param>
    public WizardFlowCompletedContext(WizardFlow flow)
        : base(flow)
    {
    }
}
