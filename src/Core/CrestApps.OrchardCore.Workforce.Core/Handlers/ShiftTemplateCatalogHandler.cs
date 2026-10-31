using System.ComponentModel.DataAnnotations;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Workforce.Models;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Workforce.Core.Handlers;

/// <summary>
/// Checks shift templates: a name, a length of up to a day, and segments that fit inside the shift.
/// </summary>
internal sealed class ShiftTemplateCatalogHandler : CatalogEntryHandlerBase<ShiftTemplate>
{
    internal readonly IStringLocalizer S;

    public ShiftTemplateCatalogHandler(IStringLocalizer<ShiftTemplateCatalogHandler> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override Task ValidatingAsync(ValidatingContext<ShiftTemplate> context, CancellationToken cancellationToken = default)
    {
        var template = context.Model;

        template.Name = template.Name?.Trim();

        if (string.IsNullOrEmpty(template.Name))
        {
            context.Result.Fail(new ValidationResult(S["The name is required."], [nameof(ShiftTemplate.Name)]));
        }

        if (template.LengthMinutes is < 15 or > 24 * 60)
        {
            context.Result.Fail(new ValidationResult(S["A shift lasts from 15 minutes to 24 hours."], [nameof(ShiftTemplate.LengthMinutes)]));
        }

        if (template.Segments.Any(segment => segment.OffsetMinutes < 0 || segment.LengthMinutes <= 0 || segment.OffsetMinutes + segment.LengthMinutes > template.LengthMinutes))
        {
            context.Result.Fail(new ValidationResult(S["Every break or activity must fit inside the shift."], [nameof(ShiftTemplate.Segments)]));
        }

        return Task.CompletedTask;
    }
}
