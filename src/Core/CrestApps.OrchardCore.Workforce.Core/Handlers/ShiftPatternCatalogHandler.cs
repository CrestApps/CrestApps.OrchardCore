using System.ComponentModel.DataAnnotations;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Workforce.Models;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Workforce.Core.Handlers;

/// <summary>
/// Checks rotations: a name, a team, one to twelve weeks, and days inside those weeks.
/// </summary>
internal sealed class ShiftPatternCatalogHandler : CatalogEntryHandlerBase<ShiftPattern>
{
    internal readonly IStringLocalizer S;

    public ShiftPatternCatalogHandler(IStringLocalizer<ShiftPatternCatalogHandler> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override Task ValidatingAsync(ValidatingContext<ShiftPattern> context, CancellationToken cancellationToken = default)
    {
        var pattern = context.Model;

        pattern.Name = pattern.Name?.Trim();

        if (string.IsNullOrEmpty(pattern.Name))
        {
            context.Result.Fail(new ValidationResult(S["The name is required."], [nameof(ShiftPattern.Name)]));
        }

        if (string.IsNullOrEmpty(pattern.TeamId))
        {
            context.Result.Fail(new ValidationResult(S["Choose the team that uses the rotation."], [nameof(ShiftPattern.TeamId)]));
        }

        if (pattern.WeekCount is < 1 or > 12)
        {
            context.Result.Fail(new ValidationResult(S["A rotation lasts from 1 to 12 weeks."], [nameof(ShiftPattern.WeekCount)]));
        }

        if (pattern.Days.Any(day => day.Week < 0 || day.Week >= pattern.WeekCount || string.IsNullOrEmpty(day.TemplateId)))
        {
            context.Result.Fail(new ValidationResult(S["Every working day needs a week inside the rotation and a shift template."], [nameof(ShiftPattern.Days)]));
        }

        return Task.CompletedTask;
    }
}
