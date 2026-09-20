using System.ComponentModel.DataAnnotations;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.Core.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Deployments;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Handlers;

internal sealed class ActivityQueueGroupHandler : CatalogEntryHandlerBase<ActivityQueueGroup>
{
    private readonly TimeProvider _timeProvider;
    private readonly IActivityQueueManager _queueManager;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ActivityQueueGroupHandler"/> class.
    /// </summary>
    /// <param name="timeProvider">The time provider used to stamp audit times.</param>
    /// <param name="queueManager">The queue manager used to clear deleted group memberships.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ActivityQueueGroupHandler(
        TimeProvider timeProvider,
        IActivityQueueManager queueManager,
        IStringLocalizer<ActivityQueueGroupHandler> stringLocalizer)
    {
        _timeProvider = timeProvider;
        _queueManager = queueManager;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override Task InitializingAsync(InitializingContext<ActivityQueueGroup> context, CancellationToken cancellationToken = default)
    {
        ContactCenterDeploymentSerializer.Populate(context.Model, context.Data);

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public override Task InitializedAsync(InitializedContext<ActivityQueueGroup> context, CancellationToken cancellationToken = default)
    {
        context.Model.CreatedUtc = _timeProvider.GetUtcNow().UtcDateTime;

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public override Task UpdatingAsync(UpdatingContext<ActivityQueueGroup> context, CancellationToken cancellationToken = default)
    {
        ContactCenterDeploymentSerializer.Populate(context.Model, context.Data);

        context.Model.ModifiedUtc = _timeProvider.GetUtcNow().UtcDateTime;

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public override Task ValidatingAsync(ValidatingContext<ActivityQueueGroup> context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(context.Model.Name))
        {
            context.Result.Fail(new ValidationResult(S["Name is required."], [nameof(ActivityQueueGroup.Name)]));
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public override async Task DeletedAsync(DeletedContext<ActivityQueueGroup> context, CancellationToken cancellationToken = default)
    {
        var queues = await _queueManager.GetAllAsync(cancellationToken);

        foreach (var queue in queues.Where(queue => string.Equals(queue.QueueGroupId, context.Model.ItemId, StringComparison.Ordinal)))
        {
            queue.QueueGroupId = null;
            await _queueManager.UpdateAsync(queue, cancellationToken: cancellationToken);
        }
    }
}
