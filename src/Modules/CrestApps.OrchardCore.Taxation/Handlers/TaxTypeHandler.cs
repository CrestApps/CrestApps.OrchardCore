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
/// Stamps and binds a <see cref="TaxType"/> through <see cref="AuditedCatalogEntryHandlerBase{T}"/> and validates it.
/// </summary>
internal sealed class TaxTypeHandler : AuditedCatalogEntryHandlerBase<TaxType>
{
    private readonly INamedCatalog<TaxType> _catalog;

    internal readonly IStringLocalizer S;

    public TaxTypeHandler(
        IHttpContextAccessor httpContextAccessor,
        INamedCatalog<TaxType> catalog,
        IClock clock,
        IStringLocalizer<TaxTypeHandler> stringLocalizer)
        : base(httpContextAccessor, clock)
    {
        _catalog = catalog;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override async Task ValidatingAsync(ValidatingContext<TaxType> context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(context.Model.Name))
        {
            context.Result.Fail(new ValidationResult(S["Name is required."], [nameof(TaxType.Name)]));
        }
        else if (await _catalog.IsNameUsedByAnotherEntryAsync(context.Model, cancellationToken))
        {
            context.Result.Fail(new ValidationResult(S["A tax type with the same name already exists."], [nameof(TaxType.Name)]));
        }
    }
}
