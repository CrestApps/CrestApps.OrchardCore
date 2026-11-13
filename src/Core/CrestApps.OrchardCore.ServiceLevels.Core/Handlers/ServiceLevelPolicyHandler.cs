using System.ComponentModel.DataAnnotations;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Core.Deployments;
using CrestApps.OrchardCore.ServiceLevels.Core.Services;
using CrestApps.OrchardCore.ServiceLevels.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ServiceLevels.Core.Handlers;

/// <summary>
/// Tidies and validates service level policies, and stamps when they were created and changed.
/// </summary>
internal sealed class ServiceLevelPolicyHandler : CatalogEntryHandlerBase<ServiceLevelPolicy>
{
    private readonly ServiceLevelPolicyStore _store;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    public ServiceLevelPolicyHandler(
        ServiceLevelPolicyStore store,
        IClock clock,
        IStringLocalizer<ServiceLevelPolicyHandler> stringLocalizer)
    {
        _store = store;
        _clock = clock;
        S = stringLocalizer;
    }

    public override Task InitializingAsync(InitializingContext<ServiceLevelPolicy> context, CancellationToken cancellationToken = default)
    {
        // A recipe creates the entry from its JSON.
        CatalogDeploymentSerializer.Populate(context.Model, context.Data);

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public override Task InitializedAsync(InitializedContext<ServiceLevelPolicy> context, CancellationToken cancellationToken = default)
    {
        context.Model.CreatedUtc = _clock.UtcNow;

        return Task.CompletedTask;
    }

    public override Task CreatingAsync(CreatingContext<ServiceLevelPolicy> context, CancellationToken cancellationToken = default)
    {
        Tidy(context.Model);

        if (context.Model.CreatedUtc == default)
        {
            context.Model.CreatedUtc = _clock.UtcNow;
        }

        return Task.CompletedTask;
    }

    public override Task UpdatingAsync(UpdatingContext<ServiceLevelPolicy> context, CancellationToken cancellationToken = default)
    {
        // A recipe updates the entry from its JSON; the editor has already changed it.
        CatalogDeploymentSerializer.Populate(context.Model, context.Data);
        Tidy(context.Model);
        context.Model.ModifiedUtc = _clock.UtcNow;

        return Task.CompletedTask;
    }

    public override async Task ValidatingAsync(ValidatingContext<ServiceLevelPolicy> context, CancellationToken cancellationToken = default)
    {
        var policy = context.Model;

        Tidy(policy);

        if (string.IsNullOrEmpty(policy.Name))
        {
            context.Result.Fail(new ValidationResult(S["The name is required."], [nameof(ServiceLevelPolicy.Name)]));
        }

        if (string.IsNullOrEmpty(policy.Scope))
        {
            context.Result.Fail(new ValidationResult(S["Choose what the policy is for."], [nameof(ServiceLevelPolicy.Scope)]));
        }

        if (policy.WarningPercent is < 1 or > 99)
        {
            context.Result.Fail(new ValidationResult(S["The warning point is a percentage from 1 to 99."], [nameof(ServiceLevelPolicy.WarningPercent)]));
        }

        if (!string.IsNullOrEmpty(policy.Name) && !string.IsNullOrEmpty(policy.Scope)
            && await _store.FindByNameAsync(policy.Scope, policy.Name, cancellationToken) is { } existing
            && !string.Equals(existing.ItemId, policy.ItemId, StringComparison.Ordinal))
        {
            context.Result.Fail(new ValidationResult(S["Another policy has this name."], [nameof(ServiceLevelPolicy.Name)]));
        }
    }

    // One target per metric and priority, without the empty ones the editor posts.
    private static void Tidy(ServiceLevelPolicy policy)
    {
        policy.Name = policy.Name?.Trim();
        policy.Description = string.IsNullOrWhiteSpace(policy.Description) ? null : policy.Description.Trim();
        policy.Scope = policy.Scope?.Trim();
        policy.HoursScheduleId = string.IsNullOrWhiteSpace(policy.HoursScheduleId) ? null : policy.HoursScheduleId.Trim();

        policy.Targets = [.. (policy.Targets ?? [])
            .Where(target => target is not null && target.Minutes > 0 && !string.IsNullOrWhiteSpace(target.Metric))
            .Select(target => new ServiceLevelTarget
            {
                Metric = target.Metric.Trim(),
                Priority = string.IsNullOrWhiteSpace(target.Priority) ? null : target.Priority.Trim(),
                Minutes = target.Minutes,
            })
            .GroupBy(target => (target.Metric.ToUpperInvariant(), target.Priority?.ToUpperInvariant()))
            .Select(group => group.Last())];
    }
}
