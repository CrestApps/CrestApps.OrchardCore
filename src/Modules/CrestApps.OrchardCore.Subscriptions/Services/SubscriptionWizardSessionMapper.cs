using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Subscriptions;
using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using CrestApps.OrchardCore.Wizard;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Subscriptions.Services;

internal static class SubscriptionWizardSessionMapper
{
    public static WizardSession ToWizardSession(SubscriptionSession subscriptionSession)
    {
        ArgumentNullException.ThrowIfNull(subscriptionSession);

        var wizardSession = new WizardSession
        {
            SessionId = subscriptionSession.SessionId,
            WizardType = SubscriptionConstants.WizardType,
            DefinitionId = subscriptionSession.ContentItemId,
            DefinitionVersionId = subscriptionSession.ContentItemVersionId,
            Status = ToWizardStatus(subscriptionSession.Status),
            CreatedUtc = subscriptionSession.CreatedUtc,
            ModifiedUtc = subscriptionSession.ModifiedUtc,
            CompletedUtc = subscriptionSession.CompletedUtc,
            OwnerId = subscriptionSession.OwnerId,
            CurrentStep = subscriptionSession.CurrentStep,
            IPAddress = subscriptionSession.IPAddress,
            AgentInfo = subscriptionSession.AgentInfo,
        };

        CopyJsonObject(subscriptionSession.SavedSteps, wizardSession.SavedSteps);
        CopyJsonObject(subscriptionSession.Properties, wizardSession.Properties);
        CopySteps(subscriptionSession.Steps, wizardSession.Steps);

        return wizardSession;
    }

    public static SubscriptionSession ToSubscriptionSession(
        WizardSession wizardSession,
        ContentItem contentItem)
    {
        ArgumentNullException.ThrowIfNull(wizardSession);
        ArgumentNullException.ThrowIfNull(contentItem);

        var subscriptionSession = new SubscriptionSession
        {
            SessionId = wizardSession.SessionId,
            ContentType = contentItem.ContentType,
            ContentItemId = contentItem.ContentItemId,
            ContentItemVersionId = contentItem.ContentItemVersionId,
            Status = ToSubscriptionStatus(wizardSession.Status),
            CreatedUtc = wizardSession.CreatedUtc,
            ModifiedUtc = wizardSession.ModifiedUtc,
            CompletedUtc = wizardSession.CompletedUtc,
            OwnerId = wizardSession.OwnerId,
            CurrentStep = wizardSession.CurrentStep,
            IPAddress = wizardSession.IPAddress,
            AgentInfo = wizardSession.AgentInfo,
        };

        CopyJsonObject(wizardSession.SavedSteps, subscriptionSession.SavedSteps);
        CopyJsonObject(wizardSession.Properties, subscriptionSession.Properties);
        CopySteps(wizardSession.Steps, subscriptionSession.Steps);

        return subscriptionSession;
    }

    public static void CopyToSubscriptionSession(
        WizardSession source,
        SubscriptionSession target)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);

        target.Status = ToSubscriptionStatus(source.Status);
        target.CreatedUtc = source.CreatedUtc;
        target.ModifiedUtc = source.ModifiedUtc;
        target.CompletedUtc = source.CompletedUtc;
        target.OwnerId = source.OwnerId;
        target.CurrentStep = source.CurrentStep;
        target.IPAddress = source.IPAddress;
        target.AgentInfo = source.AgentInfo;

        CopyJsonObject(source.SavedSteps, target.SavedSteps);
        CopyJsonObject(source.Properties, target.Properties);
        CopySteps(source.Steps, target.Steps);
    }

    public static void CopyToWizardSession(
        SubscriptionSession source,
        WizardSession target)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);

        target.WizardType = SubscriptionConstants.WizardType;
        target.DefinitionId = source.ContentItemId;
        target.DefinitionVersionId = source.ContentItemVersionId;
        target.Status = ToWizardStatus(source.Status);
        target.CreatedUtc = source.CreatedUtc;
        target.ModifiedUtc = source.ModifiedUtc;
        target.CompletedUtc = source.CompletedUtc;
        target.OwnerId = source.OwnerId;
        target.CurrentStep = source.CurrentStep;
        target.IPAddress = source.IPAddress;
        target.AgentInfo = source.AgentInfo;

        CopyJsonObject(source.SavedSteps, target.SavedSteps);
        CopyJsonObject(source.Properties, target.Properties);
        CopySteps(source.Steps, target.Steps);
    }

    private static void CopySteps(
        IEnumerable<SubscriptionFlowStep> source,
        IList<WizardStep> target)
    {
        target.Clear();

        if (source == null)
        {
            return;
        }

        foreach (var step in source)
        {
            var wizardStep = new WizardStep
            {
                Key = step.Key,
                Title = step.Title,
                Description = step.Description,
                Order = step.Order,
                CollectData = step.CollectData,
                Conceal = step.Conceal,
            };

            CopyDictionary(step.Data, wizardStep.Data);

            if (step.BillingItems != null)
            {
                wizardStep.Data[SubscriptionConstants.StepDataKey.BillingItems] = step.BillingItems;
            }

            target.Add(wizardStep);
        }
    }

    private static void CopySteps(
        IEnumerable<WizardStep> source,
        IList<SubscriptionFlowStep> target)
    {
        target.Clear();

        if (source == null)
        {
            return;
        }

        foreach (var step in source)
        {
            var subscriptionStep = new SubscriptionFlowStep
            {
                Key = step.Key,
                Title = step.Title,
                Description = step.Description,
                Order = step.Order,
                CollectData = step.CollectData,
                Conceal = step.Conceal,
            };

            CopyDictionary(step.Data, subscriptionStep.Data);

            if (step.Data.TryGetValue(SubscriptionConstants.StepDataKey.BillingItems, out var billingItems) &&
                billingItems is BillingItem[] items)
            {
                subscriptionStep.BillingItems = items;
            }

            target.Add(subscriptionStep);
        }
    }

    private static void CopyDictionary(
        IReadOnlyDictionary<string, object> source,
        Dictionary<string, object> target)
    {
        target.Clear();

        if (source == null)
        {
            return;
        }

        foreach (var item in source)
        {
            target[item.Key] = CloneValue(item.Value);
        }
    }

    private static void CopyJsonObject(JsonObject source, JsonObject target)
    {
        target.Clear();

        if (source == null)
        {
            return;
        }

        foreach (var item in source)
        {
            target[item.Key] = item.Value?.DeepClone();
        }
    }

    private static object CloneValue(object value)
        => value switch
        {
            JsonNode node => node.DeepClone(),
            BillingItem[] billingItems => billingItems.ToArray(),
            _ => value,
        };

    private static WizardSessionStatus ToWizardStatus(SubscriptionSessionStatus status)
        => status switch
        {
            SubscriptionSessionStatus.Pending => WizardSessionStatus.Pending,
            SubscriptionSessionStatus.Completed => WizardSessionStatus.Completed,
            SubscriptionSessionStatus.Canceled => WizardSessionStatus.Canceled,
            _ => WizardSessionStatus.Pending,
        };

    private static SubscriptionSessionStatus ToSubscriptionStatus(WizardSessionStatus status)
        => status switch
        {
            WizardSessionStatus.Completed => SubscriptionSessionStatus.Completed,
            WizardSessionStatus.Canceled => SubscriptionSessionStatus.Canceled,
            _ => SubscriptionSessionStatus.Pending,
        };
}
