using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Core.Deployments;
using CrestApps.OrchardCore.Stripe.Core;
using CrestApps.OrchardCore.Stripe.Deployments;
using CrestApps.OrchardCore.Stripe.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Localization;
using OrchardCore.Entities;
using OrchardCore.Environment.Options;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Stripe.Recipes;

/// <summary>
/// Imports the Stripe settings. Members the step carries replace the stored ones; a secret the step does not carry
/// keeps its stored value, and a secret it does carry, in clear text, is protected before it is stored.
/// </summary>
/// <remarks>
/// <para>
/// Every key is checked against the prefix Stripe gives it for its mode, as the settings page checks it, so a key of the
/// wrong mode, or a value copied out of another site's database (which is encrypted and starts with no Stripe prefix),
/// fails the step instead of being stored unusable.
/// </para>
/// <para>
/// The verified account and webhook identifiers are ignored, since they describe another site's connection to Stripe.
/// </para>
/// </remarks>
internal sealed class StripeSettingsStep : NamedRecipeStepHandler
{
    private static readonly (string Member, string Prefix)[] _prefixes =
    [
        (nameof(StripeSettings.LivePublishableKey), "pk_live_"),
        (nameof(StripeSettings.LivePrivateSecret), "sk_live_"),
        (nameof(StripeSettings.LiveWebhookSecret), "whsec_"),
        (nameof(StripeSettings.TestPublishableKey), "pk_test_"),
        (nameof(StripeSettings.TestPrivateSecret), "sk_test_"),
        (nameof(StripeSettings.TestWebhookSecret), "whsec_"),
    ];

    private readonly ISiteService _siteService;
    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly IOptionsUpdateNotifier _optionsUpdateNotifier;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="StripeSettingsStep"/> class.
    /// </summary>
    /// <param name="siteService">The site service that stores the settings.</param>
    /// <param name="dataProtectionProvider">The data protection provider used to protect supplied secrets.</param>
    /// <param name="optionsUpdateNotifier">The notifier that refreshes the runtime Stripe options once the import is saved.</param>
    /// <param name="stringLocalizer">The string localizer for error messages.</param>
    public StripeSettingsStep(
        ISiteService siteService,
        IDataProtectionProvider dataProtectionProvider,
        IOptionsUpdateNotifier optionsUpdateNotifier,
        IStringLocalizer<StripeSettingsStep> stringLocalizer)
        : base(StripeDeploymentSteps.Settings)
    {
        _siteService = siteService;
        _dataProtectionProvider = dataProtectionProvider;
        _optionsUpdateNotifier = optionsUpdateNotifier;
        S = stringLocalizer;
    }

    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
        if (context.Step["Settings"] is not JsonObject data)
        {
            return;
        }

        var portable = data.DeepClone().AsObject();

        foreach (var member in StripeDeploymentSteps.EnvironmentOwnedMembers)
        {
            portable.Remove(member);
        }

        var valid = true;

        foreach (var (member, prefix) in _prefixes)
        {
            var value = portable[member] is JsonValue jsonValue && jsonValue.TryGetValue<string>(out var text)
                ? text?.Trim()
                : null;

            if (!string.IsNullOrEmpty(value) && !value.StartsWith(prefix, StringComparison.Ordinal))
            {
                context.Errors.Add(S["The Stripe settings were not imported because '{0}' must start with '{1}'. Supply secrets in clear text; they are protected when they are imported.", member, prefix]);
                valid = false;
            }
        }

        if (!valid)
        {
            return;
        }

        var site = await _siteService.LoadSiteSettingsAsync();
        var settings = site.GetOrCreate<StripeSettings>();

        ProtectedSettingsDeploymentSerializer.Populate(settings, portable, StripeDeploymentSteps.ProtectedMembers, _dataProtectionProvider);

        settings.LivePublishableKey = settings.LivePublishableKey?.Trim();
        settings.TestPublishableKey = settings.TestPublishableKey?.Trim();

        site.Put(settings);

        await _siteService.UpdateSiteSettingsAsync(site);

        // The runtime StripeOptions are rebuilt once the settings are committed, as they are when the settings page saves
        // a change.
        _optionsUpdateNotifier.RequestUpdate<StripeOptions>();
    }
}
