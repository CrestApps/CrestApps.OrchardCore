using System.ComponentModel.DataAnnotations;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Deployments;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Handlers;

internal sealed class ContactCenterEntryPointHandler : CatalogEntryHandlerBase<ContactCenterEntryPoint>
{
    private readonly IClock _clock;
    private readonly IOmnichannelChannelEndpointStore _addressStore;
    private readonly IContactCenterEntryPointStore _entryPointStore;
    private readonly EntryPointAIAgentOptions _aiAgentOptions;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterEntryPointHandler"/> class.
    /// </summary>
    /// <param name="clock">The clock used to stamp audit times.</param>
    /// <param name="addressStore">The address list entry points pick their numbers from.</param>
    /// <param name="entryPointStore">The entry points, read directly because the catalog manager runs this handler.</param>
    /// <param name="aiAgentOptions">The channels other than calls whose traffic an AI agent can answer.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ContactCenterEntryPointHandler(
        IClock clock,
        IOmnichannelChannelEndpointStore addressStore,
        IContactCenterEntryPointStore entryPointStore,
        IOptions<EntryPointAIAgentOptions> aiAgentOptions,
        IStringLocalizer<ContactCenterEntryPointHandler> stringLocalizer)
    {
        _clock = clock;
        _addressStore = addressStore;
        _entryPointStore = entryPointStore;
        _aiAgentOptions = aiAgentOptions.Value;
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
    public override async Task ValidatingAsync(ValidatingContext<ContactCenterEntryPoint> context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(context.Model.Name))
        {
            context.Result.Fail(new ValidationResult(S["Name is required."], [nameof(ContactCenterEntryPoint.Name)]));
        }

        // An entry point routes to a specific agent or a queue, never both. The routed-to target is required for the
        // kind of routing selected, and this rule lives here so a recipe import and an editor enforce the same set.
        if (context.Model.TargetType == EntryPointTargetType.Agent && string.IsNullOrWhiteSpace(context.Model.TargetAgentId))
        {
            context.Result.Fail(new ValidationResult(S["Select the agent this entry point routes to."], [nameof(ContactCenterEntryPoint.TargetAgentId)]));
        }

        if (context.Model.TargetType == EntryPointTargetType.Queue && string.IsNullOrWhiteSpace(context.Model.TargetQueueId))
        {
            context.Result.Fail(new ValidationResult(S["Select the queue this entry point routes to."], [nameof(ContactCenterEntryPoint.TargetQueueId)]));
        }

        if (context.Model.TargetType == EntryPointTargetType.AIAgent)
        {
            // Calls are answered by an AI voice agent; another channel only when a feature lets an AI answer it.
            var channel = context.Model.GetChannel();
            var answersCalls = string.Equals(channel, OmnichannelConstants.Channels.Phone, StringComparison.OrdinalIgnoreCase);

            if (!answersCalls && !_aiAgentOptions.Channels.Contains(channel))
            {
                context.Result.Fail(new ValidationResult(S["An AI agent cannot answer this entry point's channel. Turn on the feature that lets an AI answer it, or route to a queue or an agent."], [nameof(ContactCenterEntryPoint.TargetType)]));
            }

            if (string.IsNullOrWhiteSpace(context.Model.TargetAIProfileId))
            {
                context.Result.Fail(new ValidationResult(answersCalls
                    ? S["Select the AI agent that answers this entry point's calls."]
                    : S["Select the AI agent that answers this entry point's messages."], [nameof(ContactCenterEntryPoint.TargetAIProfileId)]));
            }
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

        await ValidateAddressesAsync(context, cancellationToken);
    }

    // A number is answered from one place on each channel. Each picked address must exist and be used for the entry
    // point's channel, and no other enabled entry point may answer it on that channel; otherwise which one took the
    // traffic would depend on the order they happened to be read in.
    private async Task ValidateAddressesAsync(ValidatingContext<ContactCenterEntryPoint> context, CancellationToken cancellationToken)
    {
        var entryPoint = context.Model;
        var addressIds = entryPoint.AddressIds ?? [];

        if (addressIds.Count == 0)
        {
            return;
        }

        var channel = entryPoint.GetChannel();
        var addresses = await _addressStore.GetAllAsync(cancellationToken);
        var picked = new List<OmnichannelChannelEndpoint>();

        foreach (var addressId in addressIds)
        {
            var address = addresses.FirstOrDefault(candidate => candidate.IsKnownAs(addressId));

            if (address is null)
            {
                context.Result.Fail(new ValidationResult(S["One of the selected numbers no longer exists."], [nameof(ContactCenterEntryPoint.AddressIds)]));

                continue;
            }

            if (!address.HasCapability(channel))
            {
                context.Result.Fail(new ValidationResult(
                    S["{0} is not used for {1}. Tick it on the address first.", address.DisplayText ?? address.Value, channel],
                    [nameof(ContactCenterEntryPoint.AddressIds)]));

                continue;
            }

            picked.Add(address);
        }

        if (!entryPoint.Enabled || picked.Count == 0)
        {
            return;
        }

        var others = (await _entryPointStore.GetAllAsync(cancellationToken))
            .Where(other => other.Enabled &&
                !string.Equals(other.ItemId, entryPoint.ItemId, StringComparison.Ordinal) &&
                string.Equals(other.GetChannel(), channel, StringComparison.OrdinalIgnoreCase));

        foreach (var other in others)
        {
            foreach (var address in picked.Where(address => (other.AddressIds ?? []).Any(address.IsKnownAs)))
            {
                context.Result.Fail(new ValidationResult(
                    S["{0} is already answered by the entry point '{1}'. Remove it there first.", address.DisplayText ?? address.Value, other.Name],
                    [nameof(ContactCenterEntryPoint.AddressIds)]));
            }
        }
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
