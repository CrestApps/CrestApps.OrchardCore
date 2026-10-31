using System.ComponentModel.DataAnnotations;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Workforce.Models;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Workforce.Core.Handlers;

/// <summary>
/// Checks activity codes.
/// </summary>
internal sealed class ActivityCodeCatalogHandler : CatalogEntryHandlerBase<ActivityCode>
{
    internal readonly IStringLocalizer S;

    public ActivityCodeCatalogHandler(IStringLocalizer<ActivityCodeCatalogHandler> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override Task ValidatingAsync(ValidatingContext<ActivityCode> context, CancellationToken cancellationToken = default)
    {
        context.Model.Name = context.Model.Name?.Trim();
        context.Model.Code = string.IsNullOrWhiteSpace(context.Model.Code) ? null : context.Model.Code.Trim().ToUpperInvariant();

        if (string.IsNullOrEmpty(context.Model.Name))
        {
            context.Result.Fail(new ValidationResult(S["The name is required."], [nameof(ActivityCode.Name)]));
        }

        return Task.CompletedTask;
    }
}
