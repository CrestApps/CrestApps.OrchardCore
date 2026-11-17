using System.ComponentModel.DataAnnotations;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Workforce.Models;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Workforce.Core.Handlers;

/// <summary>
/// Checks the time rule Workforce keeps on a shift differential pay code: the hours it applies in.
/// </summary>
internal sealed class ShiftDifferentialPayCodeHandler : CatalogEntryHandlerBase<PayCode>
{
    internal readonly IStringLocalizer S;

    public ShiftDifferentialPayCodeHandler(IStringLocalizer<ShiftDifferentialPayCodeHandler> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override Task ValidatingAsync(ValidatingContext<PayCode> context, CancellationToken cancellationToken = default)
    {
        var item = context.Model;

        if (item.Kind != PayCodeKinds.ShiftDifferential)
        {
            return Task.CompletedTask;
        }

        var window = item.GetWorkforceSettings();

        if (!window.WindowStart.HasValue || !window.WindowEnd.HasValue || window.WindowStart == window.WindowEnd)
        {
            context.Result.Fail(new ValidationResult(S["A shift differential needs the hours it applies in."], [nameof(WorkforcePayCodeSettings.WindowStart)]));
        }

        if (item.PremiumPerHour is not > 0 && item.Multiplier <= 0)
        {
            context.Result.Fail(new ValidationResult(S["A shift differential pays a premium an hour or a multiple of the rate."], [nameof(PayCode.PremiumPerHour)]));
        }

        return Task.CompletedTask;
    }
}
