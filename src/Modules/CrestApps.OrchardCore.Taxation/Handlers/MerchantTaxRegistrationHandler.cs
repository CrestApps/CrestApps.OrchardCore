using System.ComponentModel.DataAnnotations;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Core.Deployments;
using CrestApps.OrchardCore.Taxation.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Taxation.Handlers;

/// <summary>
/// Stamps a <see cref="MerchantTaxRegistration"/> when it changes and checks it: a name, a jurisdiction, dates in order
/// and, when it files returns, a known frequency and due day.
/// </summary>
internal sealed class MerchantTaxRegistrationHandler : CatalogEntryHandlerBase<MerchantTaxRegistration>
{
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    public MerchantTaxRegistrationHandler(
        IClock clock,
        IStringLocalizer<MerchantTaxRegistrationHandler> stringLocalizer)
    {
        _clock = clock;
        S = stringLocalizer;
    }

    public override Task InitializingAsync(InitializingContext<MerchantTaxRegistration> context, CancellationToken cancellationToken = default)
    {
        CatalogDeploymentSerializer.Populate(context.Model, context.Data);

        return Task.CompletedTask;
    }

    public override Task UpdatingAsync(UpdatingContext<MerchantTaxRegistration> context, CancellationToken cancellationToken = default)
    {
        CatalogDeploymentSerializer.Populate(context.Model, context.Data);
        context.Model.ModifiedUtc = _clock.UtcNow;

        return Task.CompletedTask;
    }

    public override Task ValidatingAsync(ValidatingContext<MerchantTaxRegistration> context, CancellationToken cancellationToken = default)
    {
        var registration = context.Model;

        if (string.IsNullOrWhiteSpace(registration.Name))
        {
            context.Result.Fail(new ValidationResult(S["A tax registration needs a name."], [nameof(MerchantTaxRegistration.Name)]));
        }

        if (string.IsNullOrWhiteSpace(registration.JurisdictionId))
        {
            context.Result.Fail(new ValidationResult(S["Choose the jurisdiction the business is registered in."], [nameof(MerchantTaxRegistration.JurisdictionId)]));
        }

        if (registration.EffectiveFromUtc is { } from && registration.EffectiveToUtc is { } to && to < from)
        {
            context.Result.Fail(new ValidationResult(S["The registration ends after it takes effect."], [nameof(MerchantTaxRegistration.EffectiveToUtc)]));
        }

        if (registration.Filing is { } filing)
        {
            if (!TaxFilingFrequencies.IsKnown(filing.Frequency))
            {
                context.Result.Fail(new ValidationResult(S["Choose how often the registration files: monthly, quarterly or annually."], [nameof(MerchantTaxRegistration.Filing)]));
            }

            if (filing.DueDay is < 1 or > 31 || filing.AnnualDueDay is < 1 or > 31)
            {
                context.Result.Fail(new ValidationResult(S["The due day is a day of the month, 1 to 31."], [nameof(MerchantTaxRegistration.Filing)]));
            }

            if (filing.DueMonthsAfterPeriod is < 0 or > 12 || filing.AnnualDueMonthsAfterYear is < 0 or > 12)
            {
                context.Result.Fail(new ValidationResult(S["A return is due 0 to 12 months after its period."], [nameof(MerchantTaxRegistration.Filing)]));
            }
        }

        return Task.CompletedTask;
    }
}
