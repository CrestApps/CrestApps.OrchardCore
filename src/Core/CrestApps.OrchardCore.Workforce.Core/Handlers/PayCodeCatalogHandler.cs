using System.ComponentModel.DataAnnotations;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Workforce.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;
using CrestApps.OrchardCore.Workforce.Core.Services;

namespace CrestApps.OrchardCore.Workforce.Core.Handlers;

/// <summary>
/// Stamps and checks pay codes: a unique code, a sensible multiplier, and a window for a shift differential.
/// </summary>
internal sealed class PayCodeCatalogHandler : CatalogEntryHandlerBase<PayCode>
{
    private readonly IClock _clock;
    private readonly PayCodeStore _store;

    internal readonly IStringLocalizer S;

    public PayCodeCatalogHandler(PayCodeStore store, IClock clock, IStringLocalizer<PayCodeCatalogHandler> stringLocalizer)
    {
        _store = store;
        _clock = clock;
        S = stringLocalizer;
    }

    public override Task InitializedAsync(InitializedContext<PayCode> context, CancellationToken cancellationToken = default)
    {
        context.Model.CreatedUtc = _clock.UtcNow;

        return Task.CompletedTask;
    }

    public override Task UpdatingAsync(UpdatingContext<PayCode> context, CancellationToken cancellationToken = default)
    {
        context.Model.ModifiedUtc = _clock.UtcNow;

        return Task.CompletedTask;
    }

    public override async Task ValidatingAsync(ValidatingContext<PayCode> context, CancellationToken cancellationToken = default)
    {
        var item = context.Model;

        item.Code = item.Code?.Trim().ToUpperInvariant();
        item.Name = string.IsNullOrWhiteSpace(item.Name) ? item.Code : item.Name.Trim();
        item.ExportCode = string.IsNullOrWhiteSpace(item.ExportCode) ? null : item.ExportCode.Trim();

        if (string.IsNullOrEmpty(item.Code))
        {
            context.Result.Fail(new ValidationResult(S["The code is required."], [nameof(PayCode.Code)]));
        }
        else if (item.Code.Length > 50)
        {
            context.Result.Fail(new ValidationResult(S["The code is up to 50 characters."], [nameof(PayCode.Code)]));
        }
        else
        {
            var other = await _store.FindByCodeAsync(item.Code, cancellationToken);

            if (other is not null && other.ItemId != item.ItemId)
            {
                context.Result.Fail(new ValidationResult(S["Another pay code is {0}.", item.Code], [nameof(PayCode.Code)]));
            }
        }

        if (item.Multiplier is < 0 or > 10)
        {
            context.Result.Fail(new ValidationResult(S["The multiplier is between 0 and 10."], [nameof(PayCode.Multiplier)]));
        }

        if (item.Kind == PayCodeKind.ShiftDifferential)
        {
            if (!item.WindowStart.HasValue || !item.WindowEnd.HasValue || item.WindowStart == item.WindowEnd)
            {
                context.Result.Fail(new ValidationResult(S["A shift differential needs the hours it applies in."], [nameof(PayCode.WindowStart)]));
            }

            if (item.PremiumPerHour is not > 0 && item.Multiplier <= 0)
            {
                context.Result.Fail(new ValidationResult(S["A shift differential pays a premium an hour or a multiple of the rate."], [nameof(PayCode.PremiumPerHour)]));
            }
        }
    }
}
