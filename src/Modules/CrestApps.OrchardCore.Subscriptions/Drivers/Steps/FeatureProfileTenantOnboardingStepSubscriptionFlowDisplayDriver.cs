using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using Microsoft.Extensions.Options;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Json;

namespace CrestApps.OrchardCore.Subscriptions.Drivers.Steps;

public sealed partial class FeatureProfileTenantOnboardingStepSubscriptionFlowDisplayDriver : SubscriptionFlowDisplayDriver
{
    private readonly DocumentJsonSerializerOptions _documentJsonSerializerOptions;

    public FeatureProfileTenantOnboardingStepSubscriptionFlowDisplayDriver(IOptions<DocumentJsonSerializerOptions> documentJsonSerializerOptions)
    {
        _documentJsonSerializerOptions = documentJsonSerializerOptions.Value;
    }

    protected override string StepKey
        => SubscriptionConstants.StepKey.TenantOnboarding;

    protected override Task<IDisplayResult> UpdateStepAsync(SubscriptionFlow flow, UpdateEditorContext context)
    {
        if (!TryGetStepInfo(flow.Session, out var stepInfo))
        {
            stepInfo = new TenantOnboardingStep();
        }

        stepInfo.FeatureProfile = flow.GetCurrentStep().Data["FeatureProfile"]?.ToString();

        flow.Session.SavedSteps[SubscriptionConstants.StepKey.TenantOnboarding] = JObject.FromObject(stepInfo);

        return base.UpdateStepAsync(flow, context);
    }

    private bool TryGetStepInfo(ISubscriptionFlowSession session, out TenantOnboardingStep stepInfo)
    {
        if (!session.SavedSteps.TryGetPropertyValue(SubscriptionConstants.StepKey.TenantOnboarding, out var node))
        {
            stepInfo = null;

            return false;
        }

        stepInfo = node.Deserialize<TenantOnboardingStep>(_documentJsonSerializerOptions.SerializerOptions);

        return true;
    }
}
