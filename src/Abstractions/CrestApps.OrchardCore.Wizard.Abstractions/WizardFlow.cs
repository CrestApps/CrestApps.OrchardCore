namespace CrestApps.OrchardCore.Wizard;

/// <summary>
/// Provides step navigation over an <see cref="IWizardFlowSession"/>. The flow sorts the session steps and
/// resolves the current, first, last, next, and previous step while keeping the session's current step
/// aligned. It contains no persistence or feature-specific logic, which makes it reusable by any wizard.
/// </summary>
public sealed class WizardFlow
{
    /// <summary>
    /// Gets the session this flow navigates.
    /// </summary>
    public IWizardFlowSession Session { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="WizardFlow"/> class.
    /// </summary>
    /// <param name="session">The wizard session to navigate.</param>
    public WizardFlow(IWizardFlowSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        Session = session;
    }

    private WizardStep[] _sortedSteps;

    /// <summary>
    /// Returns the visible steps sorted by <see cref="WizardStep.Order"/> then declaration order.
    /// </summary>
    /// <returns>The sorted visible steps, or an empty array when no visible steps are available.</returns>
    public WizardStep[] GetSortedSteps()
    {
        if (_sortedSteps == null && Session.Steps != null && Session.Steps.Count > 0)
        {
            _sortedSteps = Session.Steps
                .Where(step => !step.Conceal)
                .OrderBy(step => step.Order)
                .ThenBy(Session.Steps.IndexOf)
                .ToArray();
        }

        return _sortedSteps ?? [];
    }

    private WizardStep _currentStep;

    /// <summary>
    /// Returns the step the user is currently on, defaulting to the first step.
    /// </summary>
    /// <returns>The current step, or <see langword="null"/> when the flow has no visible steps.</returns>
    public WizardStep GetCurrentStep()
    {
        if (_currentStep == null)
        {
            if (string.IsNullOrEmpty(Session.CurrentStep))
            {
                _currentStep = GetFirstStep();
            }
            else
            {
                // Use sorted steps so we always resolve the first matching step when multiple steps share a key.
                var step = GetSortedSteps().FirstOrDefault(x => x.Key == Session.CurrentStep);

                _currentStep = step ?? GetFirstStep();
            }
        }

        return _currentStep;
    }

    /// <summary>
    /// Sets the current step to the step with the given key.
    /// </summary>
    /// <param name="key">The key of the step to make current.</param>
    public void SetCurrentStep(string key)
    {
        var step = Session.Steps.FirstOrDefault(x => string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"The step '{key}' does not exist.");

        Session.CurrentStep = key;
        _currentStep = null;
    }

    /// <summary>
    /// Returns <see langword="true"/> when the current step has the given key.
    /// </summary>
    /// <param name="key">The step key to compare against.</param>
    /// <returns><see langword="true"/> when the current step key matches; otherwise, <see langword="false"/>.</returns>
    public bool CurrentStepEquals(string key)
        => key != null && GetCurrentStep()?.Key == key;

    /// <summary>
    /// Returns the first visible step in the flow.
    /// </summary>
    /// <returns>The first visible step, or <see langword="null"/> when the flow has no visible steps.</returns>
    public WizardStep GetFirstStep()
        => GetSortedSteps().FirstOrDefault();

    /// <summary>
    /// Returns the last visible step in the flow.
    /// </summary>
    /// <returns>The last visible step, or <see langword="null"/> when the flow has no visible steps.</returns>
    public WizardStep GetLastStep()
        => GetSortedSteps().LastOrDefault();

    /// <summary>
    /// Returns the step after the current step, or <see langword="null"/> when on the last step.
    /// </summary>
    /// <returns>The next visible step, or <see langword="null"/> when the current step is last or unavailable.</returns>
    public WizardStep GetNextStep()
    {
        var currentStep = GetCurrentStep();

        if (currentStep == null)
        {
            return null;
        }

        var steps = GetSortedSteps();

        for (var i = 0; i < steps.Length; i++)
        {
            if (string.Equals(steps[i].Key, currentStep.Key, StringComparison.OrdinalIgnoreCase) && i + 1 < steps.Length)
            {
                return steps[i + 1];
            }
        }

        return null;
    }

    /// <summary>
    /// Returns the step before the current step when saved step data exists, or <see langword="null"/> when
    /// on the first step.
    /// </summary>
    /// <returns>The previous visible step, or <see langword="null"/> when the current step is first or unavailable.</returns>
    public WizardStep GetPreviousStep()
    {
        var currentStep = GetCurrentStep();

        if (currentStep == null || Session.SavedSteps == null || Session.SavedSteps.Count == 0)
        {
            return null;
        }

        var steps = GetSortedSteps();

        if (steps.Length < 2 || string.Equals(steps[0].Key, currentStep.Key, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        for (var i = 0; i < steps.Length; i++)
        {
            if (string.Equals(steps[i].Key, currentStep.Key, StringComparison.OrdinalIgnoreCase))
            {
                return steps[i - 1];
            }
        }

        return null;
    }
}
