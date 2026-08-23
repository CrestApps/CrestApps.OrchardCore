using System.ComponentModel.DataAnnotations;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Deployments;
using CrestApps.OrchardCore.ContactCenter.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Handlers;

internal sealed class ContactCenterEntryPointHandler : CatalogEntryHandlerBase<ContactCenterEntryPoint>
{
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterEntryPointHandler"/> class.
    /// </summary>
    /// <param name="clock">The clock used to stamp audit times.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ContactCenterEntryPointHandler(
        IClock clock,
        IStringLocalizer<ContactCenterEntryPointHandler> stringLocalizer)
    {
        _clock = clock;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override Task InitializingAsync(InitializingContext<ContactCenterEntryPoint> context, CancellationToken cancellationToken = default)
    {
        ContactCenterDeploymentSerializer.Populate(context.Model, context.Data);

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public override Task InitializedAsync(InitializedContext<ContactCenterEntryPoint> context, CancellationToken cancellationToken = default)
    {
        context.Model.CreatedUtc = _clock.UtcNow;

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public override Task UpdatingAsync(UpdatingContext<ContactCenterEntryPoint> context, CancellationToken cancellationToken = default)
    {
        ContactCenterDeploymentSerializer.Populate(context.Model, context.Data);

        context.Model.ModifiedUtc = _clock.UtcNow;

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public override Task ValidatingAsync(ValidatingContext<ContactCenterEntryPoint> context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(context.Model.Name))
        {
            context.Result.Fail(new ValidationResult(S["Name is required."], [nameof(ContactCenterEntryPoint.Name)]));
        }

        // An entry point routes to a specific agent or a queue, never both. The routed-to target is required for the
        // kind of routing selected, and this rule lives here so a recipe import and an editor enforce the same set.
        if (context.Model.TargetType == EntryPointTargetType.Agent && string.IsNullOrWhiteSpace(context.Model.TargetAgentId))
        {
            context.Result.Fail(new ValidationResult(S["Select the agent this entry point routes calls to."], [nameof(ContactCenterEntryPoint.TargetAgentId)]));
        }

        if (context.Model.TargetType == EntryPointTargetType.Queue && string.IsNullOrWhiteSpace(context.Model.TargetQueueId))
        {
            context.Result.Fail(new ValidationResult(S["Select the queue this entry point routes calls to."], [nameof(ContactCenterEntryPoint.TargetQueueId)]));
        }

        return Task.CompletedTask;
    }
}
