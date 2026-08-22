using System.Collections.Specialized;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Subscriptions.Core.Models;

/// <summary>
/// Coordinates navigation through the visible steps of a subscription checkout session.
/// </summary>
public sealed class SubscriptionFlow
{
    /// <summary>
    /// Gets the session that stores the subscription flow state.
    /// </summary>
    public ISubscriptionFlowSession Session { get; }

    /// <summary>
    /// Gets the subscription content item being processed by the flow.
    /// </summary>
    public ContentItem ContentItem { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SubscriptionFlow"/> class.
    /// </summary>
    /// <param name="session">The session that stores the subscription flow state.</param>
    /// <param name="contentItem">The subscription content item being processed by the flow.</param>
    public SubscriptionFlow(
        ISubscriptionFlowSession session,
        ContentItem contentItem)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(contentItem);


        Session = session;
        ContentItem = contentItem;
    }

    private void StepsChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        _sortedSteps = null;
    }

    private SubscriptionFlowStep[] _sortedSteps;

    /// <summary>
    /// Gets the visible flow steps sorted by display order and their original session order.
    /// </summary>
    /// <returns>The sorted visible steps, or an empty array when no visible steps are available.</returns>
    public SubscriptionFlowStep[] GetSortedSteps()
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

    private SubscriptionFlowStep _currentStep;

    /// <summary>
    /// Gets the current visible step, falling back to the first visible step when the session has no valid current step.
    /// </summary>
    /// <returns>The current step, or <see langword="null"/> when the flow has no visible steps.</returns>
    public SubscriptionFlowStep GetCurrentStep()
    {
        if (_currentStep == null)
        {
            if (string.IsNullOrEmpty(Session.CurrentStep))
            {
                _currentStep = GetFirstStep();
            }
            else
            {
                // Use sorted steps to ensure we always get the first
                // step incase we have multiple steps with the same key.
                var step = GetSortedSteps().FirstOrDefault(x => x.Key == Session.CurrentStep);

                _currentStep = step ?? GetFirstStep();
            }
        }

        return _currentStep;
    }

    /// <summary>
    /// Updates the current step stored on the session.
    /// </summary>
    /// <param name="key">The key of the step to make current.</param>
    public void SetCurrentStep(string key)
    {
        var step = Session.Steps.FirstOrDefault(x => string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"The step '{key}' does not exists.");

        Session.CurrentStep = key;
        _currentStep = null;
    }

    /// <summary>
    /// Determines whether the current step has the specified key.
    /// </summary>
    /// <param name="key">The step key to compare with the current step key.</param>
    /// <returns><see langword="true"/> when the current step key matches the specified key; otherwise, <see langword="false"/>.</returns>
    public bool CurrentStepEquals(string key)
        => key != null && GetCurrentStep().Key == key;

    /// <summary>
    /// Gets the first visible step in the flow.
    /// </summary>
    /// <returns>The first visible step, or <see langword="null"/> when the flow has no visible steps.</returns>
    public SubscriptionFlowStep GetFirstStep()
        => GetSortedSteps().FirstOrDefault();

    /// <summary>
    /// Gets the last visible step in the flow.
    /// </summary>
    /// <returns>The last visible step, or <see langword="null"/> when the flow has no visible steps.</returns>
    public SubscriptionFlowStep GetLastStep()
        => GetSortedSteps().LastOrDefault();

    /// <summary>
    /// Gets the visible step that follows the current step.
    /// </summary>
    /// <returns>The next visible step, or <see langword="null"/> when the current step is last or unavailable.</returns>
    public SubscriptionFlowStep GetNextStep()
    {
        var currentStep = GetCurrentStep();

        if (currentStep == null)
        {
            return null;
        }

        var steps = GetSortedSteps();

        for (var i = 0; i < steps.Length; i++)
        {
            var step = steps[i];

            if (string.Equals(step.Key, currentStep.Key, StringComparison.OrdinalIgnoreCase) && i + 1 < steps.Length)
            {
                return steps[i + 1];
            }
        }

        return null;
    }

    /// <summary>
    /// Gets the visible step that precedes the current step when saved step data exists.
    /// </summary>
    /// <returns>The previous visible step, or <see langword="null"/> when the current step is first or unavailable.</returns>
    public SubscriptionFlowStep GetPreviousStep()
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
            var step = steps[i];

            if (string.Equals(step.Key, currentStep.Key, StringComparison.OrdinalIgnoreCase))
            {
                return steps[i - 1];
            }
        }

        return null;
    }
}
