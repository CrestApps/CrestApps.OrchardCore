using System.ComponentModel.DataAnnotations;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
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

        // A menu that cannot be run is refused here rather than discovered by a caller: the state machine copes
        // with a missing sub-menu or a dead key by retrying, which is the wrong moment to find out.
        if (context.Model.IvrFlow is not null)
        {
            foreach (var error in IvrFlowValidator.Validate(context.Model.IvrFlow))
            {
                context.Result.Fail(new ValidationResult(Describe(error), [nameof(ContactCenterEntryPoint.IvrFlow)]));
            }
        }

        return Task.CompletedTask;
    }

    private string Describe(IvrFlowValidationError error)
        => error.Kind switch
        {
            IvrFlowValidationErrorKind.RootNodeMissing => S["The IVR menu names no root menu (RootNodeId)."],
            IvrFlowValidationErrorKind.RootNodeNotFound => S["The IVR root menu '{0}' is not among the menus.", error.TargetId],
            IvrFlowValidationErrorKind.NodeIdMissing => S["An IVR menu has no NodeId."],
            IvrFlowValidationErrorKind.NodeIdDuplicate => S["More than one IVR menu is named '{0}'.", error.NodeId],
            IvrFlowValidationErrorKind.NodePromptMissing => S["IVR menu '{0}' has neither a Prompt nor a PromptMediaId, so callers would hear nothing.", error.NodeId],
            IvrFlowValidationErrorKind.NodeHasNoOptions => S["IVR menu '{0}' offers no keys, so callers could never leave it.", error.NodeId],
            IvrFlowValidationErrorKind.OptionDigitInvalid => S["IVR menu '{0}' has an option whose key '{1}' is not a single telephone key (0-9, * or #).", error.NodeId, error.Digit],
            IvrFlowValidationErrorKind.OptionDigitDuplicate => S["IVR menu '{0}' answers key '{1}' more than once.", error.NodeId, error.Digit],
            IvrFlowValidationErrorKind.OptionActionMissing => S["IVR menu '{0}' key '{1}' has no action.", error.NodeId, error.Digit],
            IvrFlowValidationErrorKind.ActionTargetMissing => error.NodeId is null
                ? S["The IVR fallback action needs a TargetId."]
                : S["IVR menu '{0}' key '{1}' has an action that needs a TargetId.", error.NodeId, error.Digit],
            IvrFlowValidationErrorKind.SubMenuNotFound => S["IVR menu '{0}' key '{1}' opens sub-menu '{2}', which does not exist.", error.NodeId, error.Digit, error.TargetId],
            IvrFlowValidationErrorKind.MaxRetriesInvalid => S["The IVR MaxRetries must be at least 1."],
            _ => S["The IVR menu is not valid."],
        };
}
