using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Subscriptions.Core.Services;
using CrestApps.OrchardCore.Subscriptions.Models;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Environment.Shell;
using OrchardCore.Json;

namespace CrestApps.OrchardCore.Subscriptions.Core.Handlers;

/// <summary>
/// Checks a tenant purchase again just before it is paid for, so a new-site step saved before a rule existed, or
/// edited around the step editor, can't buy a site with a reserved prefix, the operator's host, another recipe or no
/// feature profile.
/// </summary>
public sealed class TenantProvisioningPaymentGate : ICheckoutPaymentGate
{
    private readonly ITenantPurchaseRulesProvider _rulesProvider;
    private readonly IShellHost _shellHost;
    private readonly DocumentJsonSerializerOptions _jsonOptions;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="TenantProvisioningPaymentGate"/> class.
    /// </summary>
    /// <param name="rulesProvider">The rules provider.</param>
    /// <param name="shellHost">The shell host, which knows every tenant.</param>
    /// <param name="jsonOptions">The document serializer options.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public TenantProvisioningPaymentGate(
        ITenantPurchaseRulesProvider rulesProvider,
        IShellHost shellHost,
        IOptions<DocumentJsonSerializerOptions> jsonOptions,
        IStringLocalizer<TenantProvisioningPaymentGate> stringLocalizer)
    {
        _rulesProvider = rulesProvider;
        _shellHost = shellHost;
        _jsonOptions = jsonOptions.Value;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public async Task BeginningAsync(CheckoutPaymentBeginningContext context, CancellationToken cancellationToken = default)
    {
        var session = context.Flow.Session;
        var planStep = session.Steps.FirstOrDefault(step => step.Key == SubscriptionConstants.StepKey.TenantProvisioning);

        if (planStep is null)
        {
            return;
        }

        var saved = ReadSaved(session);

        if (saved is null)
        {
            context.Refuse(S["Tell us about your new site before you pay."]);

            return;
        }

        var planRecipe = GetData(planStep, "RecipeName");
        var request = new TenantPurchaseRequest
        {
            TenantName = saved.TenantName,
            Prefix = saved.Prefix,
            Domains = saved.Domains ?? [],
            RecipeName = saved.RecipeName,

            // The profile always comes from the plan, never from what was saved with the step.
            FeatureProfile = GetData(planStep, "FeatureProfile"),
        };

        var rules = await _rulesProvider.GetRulesAsync(planRecipe);
        var errors = TenantPurchaseValidator.Validate(request, rules, _shellHost.GetAllSettings());

        if (errors.Count > 0)
        {
            context.Refuse(errors.Any(error => error.Code is TenantPurchaseErrorCode.FeatureProfileRequired or TenantPurchaseErrorCode.FeatureProfileNotFound)
                ? S["This plan can't be bought yet: the store hasn't finished setting it up."]
                : S["The details of your new site are no longer accepted. Go back to the new-site step and check them."]);
        }
    }

    private TenantProvisioningStep ReadSaved(ICheckoutFlowSession session)
    {
        if (!session.SavedSteps.TryGetPropertyValue(SubscriptionConstants.StepKey.TenantProvisioning, out var node) || node is null)
        {
            return null;
        }

        try
        {
            return node.Deserialize<TenantProvisioningStep>(_jsonOptions.SerializerOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string GetData(CheckoutFlowStep step, string key)
        => step.Data.TryGetValue(key, out var value)
            ? value switch
            {
                string text => text,
                JsonNode node => node.GetValue<string>(),
                _ => value?.ToString(),
            }
            : null;
}
