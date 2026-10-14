namespace CrestApps.OrchardCore.Wizard;

/// <summary>
/// The context raised while a new wizard session is being activated. Handlers add their steps here.
/// </summary>
public sealed class WizardFlowActivatingContext
{
    /// <summary>
    /// Gets the session being activated.
    /// </summary>
    public WizardSession Session { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="WizardFlowActivatingContext"/> class.
    /// </summary>
    /// <param name="session">The session being activated.</param>
    public WizardFlowActivatingContext(WizardSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        Session = session;
    }
}
