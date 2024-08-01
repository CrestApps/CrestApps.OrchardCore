using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Text.Json.Serialization;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Subscriptions.Core.Models;

public sealed class SubscriptionFlow
{
    public string Direction { get; set; }

    public SubscriptionSession Session { get; }

    [JsonIgnore]
    public ContentItem ContentItem { get; }

    [JsonIgnore]
    public ObservableCollection<SubscriptionFlowStep> Steps { get; }

    public SubscriptionFlow(SubscriptionSession session, ContentItem contentItem)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(contentItem);

        Steps = [];
        Steps.CollectionChanged += StepsChanged;
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
        _sortedSteps ??= Steps.OrderBy(step => step.Order)
            .ThenBy(step => Steps.IndexOf(step))
            .ToArray();

        return _sortedSteps;
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

public sealed class SubscriptionFlowStep
{
    public string Title { get; set; }

    public string Description { get; set; }

    /// <summary>
    /// Each step must have a unique identifier.
    /// </summary>
    public string Key { get; set; }

    public int Order { get; set; }

    public Dictionary<string, object> Data { get; } = [];

}
