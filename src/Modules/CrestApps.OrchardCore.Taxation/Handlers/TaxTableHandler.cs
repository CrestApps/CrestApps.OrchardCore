using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Taxation.Deployments;
using CrestApps.OrchardCore.Taxation.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Taxation.Handlers;

internal sealed class TaxTableHandler : CatalogEntryHandlerBase<TaxTable>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    public TaxTableHandler(
        IHttpContextAccessor httpContextAccessor,
        IClock clock,
        IStringLocalizer<TaxTableHandler> stringLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _clock = clock;
        S = stringLocalizer;
    }

    public override Task InitializingAsync(InitializingContext<TaxTable> context, CancellationToken cancellationToken = default)
    {
        TaxationDeploymentSerializer.Populate(context.Model, context.Data);

        return Task.CompletedTask;
    }

    public override Task UpdatingAsync(UpdatingContext<TaxTable> context, CancellationToken cancellationToken = default)
    {
        context.Model.ModifiedUtc = _clock.UtcNow;
        context.Model.Version++;

        TaxationDeploymentSerializer.Populate(context.Model, context.Data);

        return Task.CompletedTask;
    }

    public override Task InitializedAsync(InitializedContext<TaxTable> context, CancellationToken cancellationToken = default)
    {
        context.Model.CreatedUtc = _clock.UtcNow;

        if (context.Model.Version < 1)
        {
            context.Model.Version = 1;
        }

        var user = _httpContextAccessor.HttpContext?.User;

        if (user is not null)
        {
            context.Model.OwnerId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            context.Model.Author = user.Identity?.Name;
        }

        return Task.CompletedTask;
    }

    public override Task ValidatingAsync(ValidatingContext<TaxTable> context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(context.Model.Name))
        {
            context.Result.Fail(new ValidationResult(S["Name is required."], [nameof(TaxTable.Name)]));
        }

        if (context.Model.EffectiveFromUtc.HasValue &&
            context.Model.EffectiveToUtc.HasValue &&
            context.Model.EffectiveToUtc.Value < context.Model.EffectiveFromUtc.Value)
        {
            context.Result.Fail(new ValidationResult(S["The effective end date cannot be earlier than the effective start date."], [nameof(TaxTable.EffectiveToUtc)]));
        }

        if (context.Model.Rows is { Count: > 0 } rows)
        {
            for (var i = 0; i < rows.Count; i++)
            {
                var row = rows[i];

                if (row.Minimum < 0m)
                {
                    context.Result.Fail(new ValidationResult(S["Row {0}: the minimum cannot be negative.", i + 1], [nameof(TaxTable.Rows)]));
                }

                if (row.Maximum.HasValue && row.Maximum.Value <= row.Minimum)
                {
                    context.Result.Fail(new ValidationResult(S["Row {0}: the maximum must be greater than the minimum.", i + 1], [nameof(TaxTable.Rows)]));
                }
            }

            ValidateRowRanges(context, rows);
        }

        return Task.CompletedTask;
    }

    // Table-driven methods progressively tax each bracket, so overlapping or multiple open-ended rows would
    // double-count tax. Rows must therefore form ordered, non-overlapping ranges with at most one open-ended
    // row placed above every bounded range.
    private void ValidateRowRanges(ValidatingContext<TaxTable> context, IList<TaxTableRow> rows)
    {
        var openEnded = rows.Where(row => !row.Maximum.HasValue).ToList();

        if (openEnded.Count > 1)
        {
            context.Result.Fail(new ValidationResult(S["Only one row may be open-ended (without a maximum)."], [nameof(TaxTable.Rows)]));
        }

        var bounded = rows
            .Where(row => row.Maximum.HasValue && row.Maximum.Value > row.Minimum)
            .OrderBy(row => row.Minimum)
            .ToList();

        for (var i = 1; i < bounded.Count; i++)
        {
            if (bounded[i].Minimum < bounded[i - 1].Maximum.Value)
            {
                context.Result.Fail(new ValidationResult(S["The rows must not overlap. The range starting at {0} overlaps the previous range.", bounded[i].Minimum], [nameof(TaxTable.Rows)]));

                break;
            }
        }

        if (openEnded.Count == 1 && bounded.Count > 0)
        {
            var highestBound = bounded.Max(row => row.Maximum.Value);

            if (openEnded[0].Minimum < highestBound)
            {
                context.Result.Fail(new ValidationResult(S["The open-ended row must start at or after the end of every bounded range."], [nameof(TaxTable.Rows)]));
            }
        }
    }
}
