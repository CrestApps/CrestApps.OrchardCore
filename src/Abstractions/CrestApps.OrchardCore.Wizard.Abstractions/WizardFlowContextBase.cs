namespace CrestApps.OrchardCore.Wizard;

/// <summary>
/// Base class for wizard flow lifecycle contexts that expose the active <see cref="WizardFlow"/>.
/// </summary>
public abstract class WizardFlowContextBase
{
    /// <summary>
    /// Gets the flow the event is being raised for.
    /// </summary>
    public WizardFlow Flow { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="WizardFlowContextBase"/> class.
    /// </summary>
    /// <param name="flow">The active wizard flow.</param>
    protected WizardFlowContextBase(WizardFlow flow)
    {
        ArgumentNullException.ThrowIfNull(flow);

        Flow = flow;
    }
}
