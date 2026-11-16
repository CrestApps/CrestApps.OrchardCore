using System.ComponentModel.DataAnnotations;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Agreements.Core.Indexes;
using CrestApps.OrchardCore.Agreements.Models;
using CrestApps.OrchardCore.Agreements.Services;
using CrestApps.OrchardCore.Core.Deployments;
using CrestApps.OrchardCore.Numbering;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Agreements.Core.Services;

/// <summary>
/// Numbers, stamps and checks agreements, for the editor and recipes alike. A recipe's values are applied here.
/// </summary>
internal sealed class AgreementHandler : CatalogEntryHandlerBase<Agreement>
{
    // The most hours a block or a month's allowance may cover.
    private const decimal MaxHours = 100_000;

    private readonly ISession _session;
    private readonly INumberSequence _numberSequence;
    private readonly IClock _clock;
    private readonly IEnumerable<IAgreementTermsValidator> _termsValidators;

    internal readonly IStringLocalizer S;

    public AgreementHandler(
        ISession session,
        INumberSequence numberSequence,
        IClock clock,
        IEnumerable<IAgreementTermsValidator> termsValidators,
        IStringLocalizer<AgreementHandler> stringLocalizer)
    {
        _session = session;
        _numberSequence = numberSequence;
        _clock = clock;
        _termsValidators = termsValidators;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override Task InitializingAsync(InitializingContext<Agreement> context, CancellationToken cancellationToken = default)
    {
        CatalogDeploymentSerializer.Populate(context.Model, context.Data);

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public override Task InitializedAsync(InitializedContext<Agreement> context, CancellationToken cancellationToken = default)
    {
        if (context.Model.CreatedUtc == default)
        {
            context.Model.CreatedUtc = _clock.UtcNow;
        }

        if (context.Model.StartDate == default)
        {
            context.Model.StartDate = DateOnly.FromDateTime(_clock.UtcNow);
        }

        context.Model.Hours ??= new AgreementHours();
        context.Model.HoursWarnings ??= new AgreementHoursWarnings();

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public override Task UpdatingAsync(UpdatingContext<Agreement> context, CancellationToken cancellationToken = default)
    {
        CatalogDeploymentSerializer.Populate(context.Model, context.Data);
        context.Model.Name = context.Model.Name?.Trim();
        context.Model.ModifiedUtc = _clock.UtcNow;

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public override async Task CreatingAsync(CreatingContext<Agreement> context, CancellationToken cancellationToken = default)
    {
        context.Model.Name = context.Model.Name?.Trim();

        if (string.IsNullOrWhiteSpace(context.Model.Number))
        {
            context.Model.Number = await _numberSequence.NextAsync(AgreementsConstants.NumberSeries, AgreementsConstants.NumberPrefix, 5, new NumberSequenceOptions
            {
                SeedAsync = async token =>
                {
                    var numbers = await _session.QueryIndex<AgreementIndex>(index => index.Number.StartsWith(AgreementsConstants.NumberPrefix), collection: AgreementsConstants.CollectionName).ListAsync(token);

                    return NumberSequenceExtensions.HighestNumber(numbers.Select(index => index.Number), AgreementsConstants.NumberPrefix);
                },
            }, cancellationToken);
        }
    }

    /// <inheritdoc/>
    public override async Task ValidatingAsync(ValidatingContext<Agreement> context, CancellationToken cancellationToken = default)
    {
        var agreement = context.Model;
        var hours = agreement.Hours ?? new AgreementHours();

        if (string.IsNullOrWhiteSpace(agreement.Name))
        {
            context.Result.Fail(new ValidationResult(S["The name is required."], [nameof(Agreement.Name)]));
        }

        if (agreement.EndDate < agreement.StartDate)
        {
            context.Result.Fail(new ValidationResult(S["The agreement can't end before it starts."], [nameof(Agreement.EndDate)]));
        }

        if (agreement.ExpiringNoticeDays is < 0 or > 365)
        {
            context.Result.Fail(new ValidationResult(S["The notice before the end is between 0 and 365 days."], [nameof(Agreement.ExpiringNoticeDays)]));
        }

        if (!Enum.IsDefined(hours.Kind))
        {
            context.Result.Fail(new ValidationResult(S["Choose how the agreement covers hours."], [nameof(Agreement.Hours)]));
        }
        else if (hours.Kind != AgreementHoursKind.None && (hours.Hours <= 0 || hours.Hours > MaxHours))
        {
            context.Result.Fail(new ValidationResult(S["Enter the hours the agreement covers, more than none and at most {0}.", MaxHours], [nameof(Agreement.Hours)]));
        }

        if (!AgreementsConstants.RoundingMinutes.Contains(hours.RoundingMinutes))
        {
            context.Result.Fail(new ValidationResult(S["Round time to none, 6, 15 or 30 minutes."], [nameof(Agreement.Hours)]));
        }

        foreach (var validator in _termsValidators)
        {
            foreach (var error in await validator.ValidateAsync(agreement, cancellationToken) ?? [])
            {
                context.Result.Fail(error);
            }
        }
    }
}
