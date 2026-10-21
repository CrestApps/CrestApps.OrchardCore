using System.ComponentModel.DataAnnotations;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Core.Handlers;
using CrestApps.OrchardCore.Core.Services;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Taxation.Handlers;

/// <summary>
/// Stamps and binds a <see cref="TaxRule"/> through <see cref="AuditedCatalogEntryHandlerBase{T}"/> and validates it.
/// </summary>
internal sealed class TaxRuleHandler : AuditedCatalogEntryHandlerBase<TaxRule>
{
    private readonly INamedCatalog<TaxRule> _catalog;
    private readonly ITaxCalculationMethodProvider _methodProvider;

    internal readonly IStringLocalizer S;

    public TaxRuleHandler(
        IHttpContextAccessor httpContextAccessor,
        INamedCatalog<TaxRule> catalog,
        ITaxCalculationMethodProvider methodProvider,
        IClock clock,
        IStringLocalizer<TaxRuleHandler> stringLocalizer)
        : base(httpContextAccessor, clock)
    {
        _catalog = catalog;
        _methodProvider = methodProvider;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override async Task ValidatingAsync(ValidatingContext<TaxRule> context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(context.Model.Name))
        {
            context.Result.Fail(new ValidationResult(S["Name is required."], [nameof(TaxRule.Name)]));
        }
        else if (await _catalog.IsNameUsedByAnotherEntryAsync(context.Model, cancellationToken))
        {
            context.Result.Fail(new ValidationResult(S["A tax rule with the same name already exists."], [nameof(TaxRule.Name)]));
        }

        if (string.IsNullOrWhiteSpace(context.Model.TaxType))
        {
            context.Result.Fail(new ValidationResult(S["Tax type is required."], [nameof(TaxRule.TaxType)]));
        }

        if (string.IsNullOrWhiteSpace(context.Model.Source))
        {
            context.Result.Fail(new ValidationResult(S["Calculation method is required."], [nameof(TaxRule.Source)]));
        }
        else if (string.IsNullOrEmpty(context.Model.TaxTableId) &&
            _methodProvider.GetMethod(context.Model.Source)?.Inputs.HasFlag(TaxCalculationMethodInputs.TaxTable) == true)
        {
            context.Result.Fail(new ValidationResult(S["A tax table is required for the '{0}' calculation method.", context.Model.Source], [nameof(TaxRule.TaxTableId)]));
        }

        if (context.Model.MinimumAmount.HasValue &&
            context.Model.MaximumAmount.HasValue &&
            context.Model.MaximumAmount.Value < context.Model.MinimumAmount.Value)
        {
            context.Result.Fail(new ValidationResult(S["The maximum amount cannot be smaller than the minimum amount."], [nameof(TaxRule.MaximumAmount)]));
        }

        if (context.Model.EffectiveFromUtc.HasValue &&
            context.Model.EffectiveToUtc.HasValue &&
            context.Model.EffectiveToUtc.Value < context.Model.EffectiveFromUtc.Value)
        {
            context.Result.Fail(new ValidationResult(S["The effective end date cannot be earlier than the effective start date."], [nameof(TaxRule.EffectiveToUtc)]));
        }
    }
}
