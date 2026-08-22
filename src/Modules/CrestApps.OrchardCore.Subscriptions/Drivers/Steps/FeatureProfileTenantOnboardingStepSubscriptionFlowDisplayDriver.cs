using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using Microsoft.Extensions.Options;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Json;

namespace CrestApps.OrchardCore.Subscriptions.Drivers.Steps;

/// <summary>
/// Updates the tenant onboarding step with the feature profile configured on the active subscription flow step.
/// </summary>
public sealed partial class FeatureProfileTenantOnboardingStepSubscriptionFlowDisplayDriver : SubscriptionFlowDisplayDriver
{
    private readonly DocumentJsonSerializerOptions _documentJsonSerializerOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="FeatureProfileTenantOnboardingStepSubscriptionFlowDisplayDriver"/> class.
    /// </summary>
    /// <param name="documentJsonSerializerOptions">The JSON serializer options used to read saved tenant onboarding step data.</param>
    public FeatureProfileTenantOnboardingStepSubscriptionFlowDisplayDriver(IOptions<DocumentJsonSerializerOptions> documentJsonSerializerOptions)
    {
        _documentJsonSerializerOptions = documentJsonSerializerOptions.Value;
    }

    /// <summary>
    /// Gets the tenant onboarding step key handled by this display driver.
    /// </summary>
    protected override string StepKey
        => SubscriptionConstants.StepKey.TenantOnboarding;

    /// <summary>
    /// Saves the feature profile from the current tenant onboarding step into the subscription flow session.
    /// </summary>
    /// <param name="flow">The subscription flow being updated.</param>
    /// <param name="context">The editor update context.</param>
    /// <returns>The display result produced by the base tenant onboarding step update.</returns>
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
