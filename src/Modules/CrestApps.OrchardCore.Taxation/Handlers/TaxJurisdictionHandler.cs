using System.ComponentModel.DataAnnotations;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Taxation.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Taxation.Handlers;

internal sealed class TaxJurisdictionHandler : CatalogEntryHandlerBase<TaxJurisdiction>
{
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    public TaxJurisdictionHandler(
        IClock clock,
        IStringLocalizer<TaxJurisdictionHandler> stringLocalizer)
    {
        _clock = clock;
        S = stringLocalizer;
    }

    public override Task UpdatingAsync(UpdatingContext<TaxJurisdiction> context, CancellationToken cancellationToken = default)
    {
        context.Model.ModifiedUtc = _clock.UtcNow;

        return Task.CompletedTask;
    }

    public override Task ValidatingAsync(ValidatingContext<TaxJurisdiction> context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(context.Model.Name))
        {
            context.Result.Fail(new ValidationResult(S["Name is required."], [nameof(TaxJurisdiction.Name)]));
        }

        if (string.IsNullOrWhiteSpace(context.Model.Code))
        {
            context.Result.Fail(new ValidationResult(S["Code is required."], [nameof(TaxJurisdiction.Code)]));
        }

        if (context.Model.EffectiveFromUtc.HasValue &&
            context.Model.EffectiveToUtc.HasValue &&
            context.Model.EffectiveToUtc.Value < context.Model.EffectiveFromUtc.Value)
        {
            context.Result.Fail(new ValidationResult(S["The effective end date cannot be earlier than the effective start date."], [nameof(TaxJurisdiction.EffectiveToUtc)]));
        }

        return Task.CompletedTask;
    }
}
