using System.ComponentModel.DataAnnotations;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Core.Deployments;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Payroll.Core.Handlers;

/// <summary>
/// Stamps pay schedules and checks them.
/// </summary>
public sealed class PayScheduleHandler : CatalogEntryHandlerBase<PaySchedule>
{
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="PayScheduleHandler"/> class.
    /// </summary>
    /// <param name="clock">The clock.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public PayScheduleHandler(IClock clock, IStringLocalizer<PayScheduleHandler> stringLocalizer)
    {
        _clock = clock;
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public override Task InitializingAsync(InitializingContext<PaySchedule> context, CancellationToken cancellationToken = default)
    {
        // A recipe or a deployment plan hands the values in; a page sets them itself and passes none.
        CatalogDeploymentSerializer.Populate(context.Model, context.Data);
        Normalize(context.Model);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task InitializedAsync(InitializedContext<PaySchedule> context, CancellationToken cancellationToken = default)
    {
        context.Model.CreatedUtc = _clock.UtcNow;

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task UpdatingAsync(UpdatingContext<PaySchedule> context, CancellationToken cancellationToken = default)
    {
        CatalogDeploymentSerializer.Populate(context.Model, context.Data);
        Normalize(context.Model);
        context.Model.ModifiedUtc = _clock.UtcNow;

        return Task.CompletedTask;
    }

    // Trims and upper-cases what a page, a recipe or a deployment plan typed.
    private static void Normalize(PaySchedule item)
    {
        item.Name = item.Name?.Trim();
    }

    /// <inheritdoc />
    public override Task ValidatingAsync(ValidatingContext<PaySchedule> context, CancellationToken cancellationToken = default)
    {
        var item = context.Model;

        Normalize(item);

        if (string.IsNullOrEmpty(item.Name))
        {
            context.Result.Fail(new ValidationResult(S["The name is required."], [nameof(PaySchedule.Name)]));
        }

        if (item.SecondPeriodStartDay is < 2 or > 28)
        {
            context.Result.Fail(new ValidationResult(S["The second period starts on a day from 2 to 28."], [nameof(PaySchedule.SecondPeriodStartDay)]));
        }

        if (item.PayDateOffsetDays is < 0 or > 60)
        {
            context.Result.Fail(new ValidationResult(S["Pay is made from 0 to 60 days after the period ends."], [nameof(PaySchedule.PayDateOffsetDays)]));
        }

        if (item.FirstPayDay is < 0 or > 31 || item.SecondPayDay is < 0 or > 31)
        {
            context.Result.Fail(new ValidationResult(S["A pay day is a day of the month from 1 to 31, or 0 for the last day."], [nameof(PaySchedule.FirstPayDay)]));
        }

        if (item.ApprovalLeadDays is < 0 or > 30)
        {
            context.Result.Fail(new ValidationResult(S["A run is approved from 0 to 30 days before the pay date."], [nameof(PaySchedule.ApprovalLeadDays)]));
        }

        return Task.CompletedTask;
    }
}

/// <summary>
/// Stamps pay codes and checks them: a unique code, a multiplier from 0 to 10.
/// </summary>
public sealed class PayCodeHandler : CatalogEntryHandlerBase<PayCode>
{
    private readonly IPayCodeLookup _lookup;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="PayCodeHandler"/> class.
    /// </summary>
    /// <param name="lookup">Finds codes by code.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public PayCodeHandler(IPayCodeLookup lookup, IClock clock, IStringLocalizer<PayCodeHandler> stringLocalizer)
    {
        _lookup = lookup;
        _clock = clock;
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public override Task InitializingAsync(InitializingContext<PayCode> context, CancellationToken cancellationToken = default)
    {
        // A recipe or a deployment plan hands the values in; a page sets them itself and passes none.
        CatalogDeploymentSerializer.Populate(context.Model, context.Data);
        Normalize(context.Model);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task InitializedAsync(InitializedContext<PayCode> context, CancellationToken cancellationToken = default)
    {
        context.Model.CreatedUtc = _clock.UtcNow;

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task UpdatingAsync(UpdatingContext<PayCode> context, CancellationToken cancellationToken = default)
    {
        CatalogDeploymentSerializer.Populate(context.Model, context.Data);
        Normalize(context.Model);
        context.Model.ModifiedUtc = _clock.UtcNow;

        return Task.CompletedTask;
    }

    // Trims and upper-cases what a page, a recipe or a deployment plan typed.
    private static void Normalize(PayCode item)
    {
        item.Code = item.Code?.Trim().ToUpperInvariant();
        item.Name = string.IsNullOrWhiteSpace(item.Name) ? item.Code : item.Name.Trim();
        item.ExportCode = string.IsNullOrWhiteSpace(item.ExportCode) ? null : item.ExportCode.Trim();
        item.Kind = string.IsNullOrWhiteSpace(item.Kind) ? PayCodeKinds.Regular : item.Kind.Trim();
    }

    /// <inheritdoc />
    public override async Task ValidatingAsync(ValidatingContext<PayCode> context, CancellationToken cancellationToken = default)
    {
        var item = context.Model;

        Normalize(item);

        if (string.IsNullOrEmpty(item.Code))
        {
            context.Result.Fail(new ValidationResult(S["The code is required."], [nameof(PayCode.Code)]));
        }
        else if (item.Code.Length > 50)
        {
            context.Result.Fail(new ValidationResult(S["The code is up to 50 characters."], [nameof(PayCode.Code)]));
        }
        else if (await _lookup.FindByCodeAsync(item.Code, cancellationToken) is { } other && other.ItemId != item.ItemId)
        {
            context.Result.Fail(new ValidationResult(S["Another pay code is {0}.", item.Code], [nameof(PayCode.Code)]));
        }

        if (item.Multiplier is < 0 or > 10)
        {
            context.Result.Fail(new ValidationResult(S["The multiplier is between 0 and 10."], [nameof(PayCode.Multiplier)]));
        }

        if (item.PremiumPerHour is < 0)
        {
            context.Result.Fail(new ValidationResult(S["The premium can't be negative."], [nameof(PayCode.PremiumPerHour)]));
        }
    }
}

/// <summary>
/// Finds a pay code by its code, for the handler (the store, or a test's).
/// </summary>
public interface IPayCodeLookup
{
    /// <summary>
    /// Finds a pay code by its code.
    /// </summary>
    /// <param name="code">The code.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The code, or <see langword="null"/>.</returns>
    Task<PayCode> FindByCodeAsync(string code, CancellationToken cancellationToken = default);
}
