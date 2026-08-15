using System.ComponentModel.DataAnnotations;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Taxation.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Taxation.Handlers;

internal sealed class TaxCategoryHandler : CatalogEntryHandlerBase<TaxCategory>
{
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    public TaxCategoryHandler(
        IClock clock,
        IStringLocalizer<TaxCategoryHandler> stringLocalizer)
    {
        _clock = clock;
        S = stringLocalizer;
    }

    public override Task UpdatingAsync(UpdatingContext<TaxCategory> context, CancellationToken cancellationToken = default)
    {
        context.Model.ModifiedUtc = _clock.UtcNow;

        return Task.CompletedTask;
    }

    public override Task ValidatingAsync(ValidatingContext<TaxCategory> context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(context.Model.Name))
        {
            context.Result.Fail(new ValidationResult(S["Name is required."], [nameof(TaxCategory.Name)]));
        }

        if (string.IsNullOrWhiteSpace(context.Model.Code))
        {
            context.Result.Fail(new ValidationResult(S["Code is required."], [nameof(TaxCategory.Code)]));
        }

        return Task.CompletedTask;
    }
}
