using System.ComponentModel.DataAnnotations;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Core.Handlers;
using CrestApps.OrchardCore.Core.Services;
using CrestApps.OrchardCore.Taxation.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Taxation.Handlers;

/// <summary>
/// Stamps and binds a <see cref="TaxJurisdiction"/> through <see cref="AuditedCatalogEntryHandlerBase{T}"/> and validates it.
/// </summary>
internal sealed class TaxJurisdictionHandler : AuditedCatalogEntryHandlerBase<TaxJurisdiction>
{
    private readonly INamedCatalog<TaxJurisdiction> _catalog;

    internal readonly IStringLocalizer S;

    public TaxJurisdictionHandler(
        IHttpContextAccessor httpContextAccessor,
        INamedCatalog<TaxJurisdiction> catalog,
        IClock clock,
        IStringLocalizer<TaxJurisdictionHandler> stringLocalizer)
        : base(httpContextAccessor, clock)
    {
        _catalog = catalog;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override async Task ValidatingAsync(ValidatingContext<TaxJurisdiction> context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(context.Model.Name))
        {
            context.Result.Fail(new ValidationResult(S["Name is required."], [nameof(TaxJurisdiction.Name)]));
        }
        else if (await _catalog.IsNameUsedByAnotherEntryAsync(context.Model, cancellationToken))
        {
            context.Result.Fail(new ValidationResult(S["A tax jurisdiction with the same name already exists."], [nameof(TaxJurisdiction.Name)]));
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
    }
}
