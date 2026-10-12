namespace CrestApps.OrchardCore.Checkout;

/// <summary>
/// Provides step navigation over a <see cref="ICheckoutFlowSession"/>. The flow sorts the session steps,
/// resolves the current, first, last, next, and previous step, and keeps the session's current step aligned.
/// </summary>
public sealed class CheckoutFlow
{
    /// <summary>
    /// The session this flow navigates.
    /// </summary>
    public ICheckoutFlowSession Session { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="CheckoutFlow"/> class.
    /// </summary>
    /// <param name="session">The checkout session to navigate.</param>
    public CheckoutFlow(ICheckoutFlowSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        Session = session;
    }

    private CheckoutFlowStep[] _sortedSteps;

    /// <summary>
    /// Returns the visible steps sorted by <see cref="CheckoutFlowStep.Order"/> then declaration order.
    /// </summary>
    public CheckoutFlowStep[] GetSortedSteps()
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

    private CheckoutFlowStep _currentStep;

    /// <summary>
    /// Returns the step the customer is currently on, defaulting to the first step.
    /// </summary>
    public CheckoutFlowStep GetCurrentStep()
    {
        if (_currentStep == null)
        {
            if (string.IsNullOrEmpty(Session.CurrentStep))
            {
                _currentStep = GetFirstStep();
            }
            else
            {
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
    public bool CurrentStepEquals(string key)
        => key != null && GetCurrentStep()?.Key == key;

    /// <summary>
    /// Returns the first step in the flow.
    /// </summary>
    public CheckoutFlowStep GetFirstStep()
        => GetSortedSteps().FirstOrDefault();

    /// <summary>
    /// Returns the last step in the flow.
    /// </summary>
    public CheckoutFlowStep GetLastStep()
        => GetSortedSteps().LastOrDefault();

    /// <summary>
    /// Returns the step after the current step, or <see langword="null"/> when on the last step.
    /// </summary>
    public CheckoutFlowStep GetNextStep()
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
    /// Returns the step before the current step, or <see langword="null"/> when on the first step.
    /// </summary>
    public CheckoutFlowStep GetPreviousStep()
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
