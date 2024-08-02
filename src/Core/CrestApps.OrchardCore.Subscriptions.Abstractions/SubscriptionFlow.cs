using System.Collections.Specialized;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Subscriptions.Core.Models;

public sealed class SubscriptionFlow
{
    public string Direction { get; set; }

    public SubscriptionSession Session { get; }

    public ContentItem ContentItem { get; }

    public SubscriptionFlow(SubscriptionSession session, ContentItem contentItem)
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

    public SubscriptionFlowStep[] GetSortedSteps()
    {
        if (_sortedSteps == null && Session.Steps != null && Session.Steps.Count > 0)
        {
            _sortedSteps = Session.Steps
                .OrderBy(step => step.Order)
                .ThenBy(Session.Steps.IndexOf)
                .ToArray();
        }

        return _sortedSteps ?? [];
    }

    private SubscriptionFlowStep _currentStep;

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

    public void SetCurrentStep(string key)
    {
        var step = Session.Steps.FirstOrDefault(x => string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase));

        if (step == null)
        {
            throw new InvalidOperationException($"The step '{key}' does not exists.");
        }

        Session.CurrentStep = key;
        _currentStep = null;
    }

    public bool CurrentStepEquals(string key)
        => key != null && GetCurrentStep().Key == key;

    public SubscriptionFlowStep GetFirstStep()
        => GetSortedSteps().FirstOrDefault();

    public SubscriptionFlowStep GetLastStep()
        => GetSortedSteps().LastOrDefault();

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

            if (string.Equals(step.Key, currentStep.Key, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (i + 1 < steps.Length)
            {
                return steps[i + 1];
            }

            break;
        }

        return null;
    }

    public SubscriptionFlowStep GetPreviousStep()
    {
        var currentStep = GetCurrentStep();

        if (currentStep == null || Session.SavedSteps == null || Session.SavedSteps.Count == 0)
        {
            return null;
        }

        var steps = GetSortedSteps();

        for (var i = 0; i < steps.Length; i++)
        {
            var step = steps[i];
            var nextIndex = i + 1;

            if (nextIndex < steps.Length && string.Equals(steps[nextIndex].Key, currentStep.Key, StringComparison.OrdinalIgnoreCase))
            {
                return step;
            }
        }

        return null;
    }
}
