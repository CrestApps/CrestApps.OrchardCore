using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Workforce.ContactCenter.Indexes;
using CrestApps.OrchardCore.Workforce.ContactCenter.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Workforce.ContactCenter.Services;

/// <summary>
/// Stores adherence rule sets.
/// </summary>
public sealed class AdherenceRuleSetStore : DocumentCatalog<AdherenceRuleSet, AdherenceRuleSetIndex>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AdherenceRuleSetStore"/> class.
    /// </summary>
    /// <param name="session">The session.</param>
    public AdherenceRuleSetStore(ISession session)
        : base(session)
    {
        CollectionName = WorkforceConstants.CollectionName;
    }
}

/// <summary>
/// Manages adherence rule sets.
/// </summary>
public sealed class AdherenceRuleSetManager : CatalogManager<AdherenceRuleSet>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AdherenceRuleSetManager"/> class.
    /// </summary>
    /// <param name="store">The store.</param>
    /// <param name="handlers">The handlers.</param>
    /// <param name="logger">The logger.</param>
    public AdherenceRuleSetManager(
        AdherenceRuleSetStore store,
        IEnumerable<ICatalogEntryHandler<AdherenceRuleSet>> handlers,
        ILogger<CatalogManager<AdherenceRuleSet>> logger)
        : base(store, handlers, logger)
    {
    }
}

/// <summary>
/// Validates and stamps adherence rule sets.
/// </summary>
internal sealed class AdherenceRuleSetHandler : CatalogEntryHandlerBase<AdherenceRuleSet>
{
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AdherenceRuleSetHandler"/> class.
    /// </summary>
    /// <param name="clock">The clock.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AdherenceRuleSetHandler(IClock clock, IStringLocalizer<AdherenceRuleSetHandler> stringLocalizer)
    {
        _clock = clock;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override Task InitializedAsync(InitializedContext<AdherenceRuleSet> context, CancellationToken cancellationToken = default)
    {
        context.Model.CreatedUtc = _clock.UtcNow;
        context.Model.Rules = AdherenceCalculator.DefaultRules();

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public override Task UpdatingAsync(UpdatingContext<AdherenceRuleSet> context, CancellationToken cancellationToken = default)
    {
        context.Model.ModifiedUtc = _clock.UtcNow;

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public override Task ValidatingAsync(ValidatingContext<AdherenceRuleSet> context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(context.Model.Name))
        {
            context.Result.Fail(new System.ComponentModel.DataAnnotations.ValidationResult(S["The name is required."], [nameof(AdherenceRuleSet.Name)]));
        }

        foreach (var rule in context.Model.Rules)
        {
            if (rule.StartGraceMinutes < 0 || rule.EndGraceMinutes < 0 || rule.MinimumOutMinutes < 0 || rule.AlertAfterMinutes < 0)
            {
                context.Result.Fail(new System.ComponentModel.DataAnnotations.ValidationResult(S["Minutes can't be negative."], [nameof(AdherenceRuleSet.Rules)]));
                break;
            }
        }

        return Task.CompletedTask;
    }
}
