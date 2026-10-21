using System.ComponentModel.DataAnnotations;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Core.Handlers;
using CrestApps.OrchardCore.Core.Services;
using CrestApps.OrchardCore.Taxation.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Taxation.Handlers;

/// <summary>
/// Stamps and binds a <see cref="TaxTable"/> through <see cref="AuditedCatalogEntryHandlerBase{T}"/> and validates it.
/// </summary>
internal sealed class TaxTableHandler : AuditedCatalogEntryHandlerBase<TaxTable>
{
    private readonly INamedCatalog<TaxTable> _catalog;

    internal readonly IStringLocalizer S;

    public TaxTableHandler(
        IHttpContextAccessor httpContextAccessor,
        INamedCatalog<TaxTable> catalog,
        IClock clock,
        IStringLocalizer<TaxTableHandler> stringLocalizer)
        : base(httpContextAccessor, clock)
    {
        _catalog = catalog;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override async Task InitializedAsync(InitializedContext<TaxTable> context, CancellationToken cancellationToken = default)
    {
        await base.InitializedAsync(context, cancellationToken);

        if (context.Model.Version < 1)
        {
            context.Model.Version = 1;
        }
    }

    /// <inheritdoc/>
    public override async Task UpdatingAsync(UpdatingContext<TaxTable> context, CancellationToken cancellationToken = default)
    {
        await base.UpdatingAsync(context, cancellationToken);

        context.Model.Version++;
    }

    /// <inheritdoc/>
    public override async Task ValidatingAsync(ValidatingContext<TaxTable> context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(context.Model.Name))
        {
            context.Result.Fail(new ValidationResult(S["Name is required."], [nameof(TaxTable.Name)]));
        }
        else if (await _catalog.IsNameUsedByAnotherEntryAsync(context.Model, cancellationToken))
        {
            context.Result.Fail(new ValidationResult(S["A tax table with the same name already exists."], [nameof(TaxTable.Name)]));
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
