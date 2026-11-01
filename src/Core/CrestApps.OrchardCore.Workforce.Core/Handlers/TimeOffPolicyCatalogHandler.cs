using System.ComponentModel.DataAnnotations;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Workforce.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Workforce.Core.Handlers;

/// <summary>
/// Stamps and checks time-off policies: a name, a type, rates and caps that aren't negative, a real reset date and assignments to one team or one worker each.
/// </summary>
internal sealed class TimeOffPolicyCatalogHandler : CatalogEntryHandlerBase<TimeOffPolicy>
{
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    public TimeOffPolicyCatalogHandler(IClock clock, IStringLocalizer<TimeOffPolicyCatalogHandler> stringLocalizer)
    {
        _clock = clock;
        S = stringLocalizer;
    }

    public override Task InitializedAsync(InitializedContext<TimeOffPolicy> context, CancellationToken cancellationToken = default)
    {
        context.Model.CreatedUtc = _clock.UtcNow;

        return Task.CompletedTask;
    }

    public override Task UpdatingAsync(UpdatingContext<TimeOffPolicy> context, CancellationToken cancellationToken = default)
    {
        context.Model.ModifiedUtc = _clock.UtcNow;

        return Task.CompletedTask;
    }

    public override Task ValidatingAsync(ValidatingContext<TimeOffPolicy> context, CancellationToken cancellationToken = default)
    {
        var item = context.Model;

        item.Name = item.Name?.Trim();

        if (string.IsNullOrEmpty(item.Name))
        {
            context.Result.Fail(new ValidationResult(S["The name is required."], [nameof(TimeOffPolicy.Name)]));
        }

        if (string.IsNullOrEmpty(item.TypeId))
        {
            context.Result.Fail(new ValidationResult(S["Choose the time-off type."], [nameof(TimeOffPolicy.TypeId)]));
        }

        if (item.Rate < 0 || item.TenureTiers.Any(tier => tier.Rate < 0 || tier.FromMonths < 0))
        {
            context.Result.Fail(new ValidationResult(S["Rates can't be negative."], [nameof(TimeOffPolicy.Rate)]));
        }

        if (item.WaitingPeriodDays < 0 || item.MaxBalance < 0 || item.CarryoverMax < 0 || item.NegativeBalanceLimit < 0)
        {
            context.Result.Fail(new ValidationResult(S["The waiting period, the caps and the negative balance limit can't be negative."], [nameof(TimeOffPolicy.MaxBalance)]));
        }

        if (item.ResetMonth is < 1 or > 12 || item.ResetDay < 1 || item.ResetDay > DateTime.DaysInMonth(2025, Math.Clamp(item.ResetMonth, 1, 12)))
        {
            context.Result.Fail(new ValidationResult(S["The reset date isn't a real date."], [nameof(TimeOffPolicy.ResetDay)]));
        }

        if (item.Assignments.Any(assignment => string.IsNullOrEmpty(assignment.TeamId) == string.IsNullOrEmpty(assignment.WorkerId)))
        {
            context.Result.Fail(new ValidationResult(S["Each assignment is for one team or one worker."], [nameof(TimeOffPolicy.Assignments)]));
        }

        if (item.Assignments.Any(assignment => assignment.EffectiveFrom > assignment.EffectiveTo))
        {
            context.Result.Fail(new ValidationResult(S["An assignment can't end before it starts."], [nameof(TimeOffPolicy.Assignments)]));
        }

        return Task.CompletedTask;
    }
}
