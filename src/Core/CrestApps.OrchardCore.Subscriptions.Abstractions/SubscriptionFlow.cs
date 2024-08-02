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

    public SubscriptionFlowStep GetCurrentStep()
    {
        if (Session.CurrentStep == null)
        {
            return GetFirstStep();
        }

        // Use sorted steps to ensure we always get the first
        // step incase we have multiple steps with the same key.
        var step = GetSortedSteps().FirstOrDefault(x => x.Key == Session.CurrentStep);

        return step ?? GetFirstStep();
    }

    public SubscriptionFlowStep GetFirstStep()
        => GetSortedSteps().FirstOrDefault();

    public SubscriptionFlowStep GetLastStep()
        => GetSortedSteps().LastOrDefault();

    public SubscriptionFlowStep GetNextStep()
    {
        if (Session.CurrentStep == null)
        {
            return null;
        }

        var steps = GetSortedSteps();

        for (var i = 0; i < steps.Length; i++)
        {
            var step = steps[i];

            if (step.Key != Session.CurrentStep)
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
        if (Session.CurrentStep == null || Session.SavedSteps == null || Session.SavedSteps.Count == 0)
        {
            return null;
        }

        var steps = GetSortedSteps();

        for (var i = 0; i < steps.Length; i++)
        {
            var step = steps[i];
            var nextIndex = i + 1;

            if (nextIndex < steps.Length && steps[nextIndex].Key == Session.CurrentStep)
            {
                return step;
            }
        }

        return null;
    }
}
