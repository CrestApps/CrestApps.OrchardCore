namespace CrestApps.OrchardCore.Wizard;

/// <summary>
/// The context raised when a wizard fails.
/// </summary>
public sealed class WizardFlowFailedContext : WizardFlowContextBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WizardFlowFailedContext"/> class.
    /// </summary>
    /// <param name="flow">The active wizard flow.</param>
    public WizardFlowFailedContext(WizardFlow flow)
        : base(flow)
    {
    }
}
