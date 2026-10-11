namespace CrestApps.OrchardCore.Wizard;

/// <summary>
/// The context raised before a wizard is completed, after every step has been validated.
/// </summary>
public sealed class WizardFlowCompletingContext : WizardFlowContextBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WizardFlowCompletingContext"/> class.
    /// </summary>
    /// <param name="flow">The active wizard flow.</param>
    public WizardFlowCompletingContext(WizardFlow flow)
        : base(flow)
    {
    }
}
