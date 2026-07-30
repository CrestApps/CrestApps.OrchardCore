using System.ComponentModel.DataAnnotations;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Deployments;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Handlers;

/// <remarks>
/// The queue group manager is resolved lazily. The queue group handler depends on the queue manager, so injecting the
/// queue group manager here would close a container cycle at construction time.
/// </remarks>
internal sealed class ActivityQueueHandler : CatalogEntryHandlerBase<ActivityQueue>
{
    private readonly IClock _clock;
    private readonly IServiceProvider _serviceProvider;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ActivityQueueHandler"/> class.
    /// </summary>
    /// <param name="clock">The clock used to stamp audit times.</param>
    /// <param name="serviceProvider">The service provider used to resolve the queue group manager when a queue is validated.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ActivityQueueHandler(
        IClock clock,
        IServiceProvider serviceProvider,
        IStringLocalizer<ActivityQueueHandler> stringLocalizer)
    {
        _clock = clock;
        _serviceProvider = serviceProvider;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override Task InitializingAsync(InitializingContext<ActivityQueue> context, CancellationToken cancellationToken = default)
    {
        ContactCenterDeploymentSerializer.Populate(context.Model, context.Data);

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public override Task InitializedAsync(InitializedContext<ActivityQueue> context, CancellationToken cancellationToken = default)
    {
        context.Model.CreatedUtc = _clock.UtcNow;

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public override Task UpdatingAsync(UpdatingContext<ActivityQueue> context, CancellationToken cancellationToken = default)
    {
        ContactCenterDeploymentSerializer.Populate(context.Model, context.Data);

        context.Model.ModifiedUtc = _clock.UtcNow;

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public override async Task ValidatingAsync(ValidatingContext<ActivityQueue> context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(context.Model.Name))
        {
            context.Result.Fail(new ValidationResult(S["Name is required."], [nameof(ActivityQueue.Name)]));
        }

        if (!string.IsNullOrWhiteSpace(context.Model.QueueGroupId) &&
            await _serviceProvider.GetRequiredService<IActivityQueueGroupManager>().FindByIdAsync(context.Model.QueueGroupId, cancellationToken) is null)
        {
            context.Result.Fail(new ValidationResult(S["Select a valid queue group."], [nameof(ActivityQueue.QueueGroupId)]));
        }
    }
}
