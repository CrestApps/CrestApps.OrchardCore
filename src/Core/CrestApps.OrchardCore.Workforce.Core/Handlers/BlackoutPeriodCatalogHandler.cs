using System.ComponentModel.DataAnnotations;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Workforce.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Workforce.Core.Handlers;

/// <summary>
/// Stamps and checks blackout dates: a name and a range that doesn't end before it starts.
/// </summary>
internal sealed class BlackoutPeriodCatalogHandler : CatalogEntryHandlerBase<BlackoutPeriod>
{
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    public BlackoutPeriodCatalogHandler(IClock clock, IStringLocalizer<BlackoutPeriodCatalogHandler> stringLocalizer)
    {
        _clock = clock;
        S = stringLocalizer;
    }

    public override Task InitializedAsync(InitializedContext<BlackoutPeriod> context, CancellationToken cancellationToken = default)
    {
        context.Model.CreatedUtc = _clock.UtcNow;

        return Task.CompletedTask;
    }

    public override Task UpdatingAsync(UpdatingContext<BlackoutPeriod> context, CancellationToken cancellationToken = default)
    {
        context.Model.ModifiedUtc = _clock.UtcNow;

        return Task.CompletedTask;
    }

    public override Task ValidatingAsync(ValidatingContext<BlackoutPeriod> context, CancellationToken cancellationToken = default)
    {
        var item = context.Model;

        item.Name = item.Name?.Trim();

        if (string.IsNullOrEmpty(item.Name))
        {
            context.Result.Fail(new ValidationResult(S["The name is required."], [nameof(BlackoutPeriod.Name)]));
        }

        if (item.EndDate < item.StartDate)
        {
            context.Result.Fail(new ValidationResult(S["The last day can't be before the first."], [nameof(BlackoutPeriod.EndDate)]));
        }

        return Task.CompletedTask;
    }
}
