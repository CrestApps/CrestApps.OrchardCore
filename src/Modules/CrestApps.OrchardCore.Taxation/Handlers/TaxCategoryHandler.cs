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
/// Stamps and binds a <see cref="TaxCategory"/> through <see cref="AuditedCatalogEntryHandlerBase{T}"/> and validates it.
/// </summary>
internal sealed class TaxCategoryHandler : AuditedCatalogEntryHandlerBase<TaxCategory>
{
    private readonly INamedCatalog<TaxCategory> _catalog;

    internal readonly IStringLocalizer S;

    public TaxCategoryHandler(
        IHttpContextAccessor httpContextAccessor,
        INamedCatalog<TaxCategory> catalog,
        IClock clock,
        IStringLocalizer<TaxCategoryHandler> stringLocalizer)
        : base(httpContextAccessor, clock)
    {
        _catalog = catalog;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override async Task ValidatingAsync(ValidatingContext<TaxCategory> context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(context.Model.Name))
        {
            context.Result.Fail(new ValidationResult(S["Name is required."], [nameof(TaxCategory.Name)]));
        }
        else if (await _catalog.IsNameUsedByAnotherEntryAsync(context.Model, cancellationToken))
        {
            context.Result.Fail(new ValidationResult(S["A tax category with the same name already exists."], [nameof(TaxCategory.Name)]));
        }

        if (string.IsNullOrWhiteSpace(context.Model.Code))
        {
            context.Result.Fail(new ValidationResult(S["Code is required."], [nameof(TaxCategory.Code)]));
        }
    }
}
