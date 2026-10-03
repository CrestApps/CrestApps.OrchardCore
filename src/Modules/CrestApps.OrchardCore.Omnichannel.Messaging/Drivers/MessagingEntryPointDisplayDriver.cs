using CrestApps.Core;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Channels;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Drivers;

/// <summary>
/// Adds what an inbound entry point that answers a messaging channel has beyond the target and opening hours every
/// entry point has: how a queue's conversations are handed out, the auto-reply, and the auto-reply sent while closed.
/// </summary>
internal sealed class MessagingEntryPointDisplayDriver : DisplayDriver<ContactCenterEntryPoint>
{
    private readonly IMessagingChannelResolver _channelResolver;
    private readonly IShellFeaturesManager _shellFeaturesManager;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="MessagingEntryPointDisplayDriver"/> class.
    /// </summary>
    /// <param name="channelResolver">The enabled messaging channels.</param>
    /// <param name="shellFeaturesManager">The shell features manager, for whether routed distribution is on.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public MessagingEntryPointDisplayDriver(
        IMessagingChannelResolver channelResolver,
        IShellFeaturesManager shellFeaturesManager,
        IStringLocalizer<MessagingEntryPointDisplayDriver> stringLocalizer)
    {
        _channelResolver = channelResolver;
        _shellFeaturesManager = shellFeaturesManager;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override IDisplayResult Edit(ContactCenterEntryPoint entryPoint, BuildEditorContext context)
    {
        if (!AnswersMessages(entryPoint))
        {
            return null;
        }

        async ValueTask PopulateAsync(MessagingEntryPointViewModel model)
        {
            var settings = entryPoint.GetOrCreate<MessagingEntryPointSettings>();
            var routedDistributionEnabled = await IsRoutedDistributionEnabledAsync();

            // A stored "Routed" mode on a tenant that no longer runs push distribution behaves as a shared pool (the
            // inbound chain already falls back that way), so the editor shows what actually happens.
            model.DistributionMode = settings.DistributionMode == ConversationDistributionMode.Routed && !routedDistributionEnabled
                ? ConversationDistributionMode.SharedPool
                : settings.DistributionMode;
            model.AutoReplyMessage = settings.AutoReplyMessage;
            model.ClosedAutoReplyMessage = settings.ClosedAutoReplyMessage;
            model.DistributionModes =
            [
                new SelectListItem(S["Shared pool (claim to own)"], nameof(ConversationDistributionMode.SharedPool)),
            ];

            if (routedDistributionEnabled)
            {
                model.DistributionModes.Add(new SelectListItem(S["Routed (assign to an available agent)"], nameof(ConversationDistributionMode.Routed)));
            }
        }

        return Combine(
            Initialize<MessagingEntryPointViewModel>("MessagingEntryPoint_Edit", PopulateAsync).Location("Content:3%Routing;2"),
            Initialize<MessagingEntryPointViewModel>("MessagingEntryPointClosed_Edit", PopulateAsync).Location("Content:2%Hours;3"));
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(ContactCenterEntryPoint entryPoint, UpdateEditorContext context)
    {
        if (!AnswersMessages(entryPoint))
        {
            return null;
        }

        var model = new MessagingEntryPointViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var distributionMode = model.DistributionMode;

        if (distributionMode == ConversationDistributionMode.Routed && !await IsRoutedDistributionEnabledAsync())
        {
            distributionMode = ConversationDistributionMode.SharedPool;
        }

        entryPoint.Put(new MessagingEntryPointSettings
        {
            DistributionMode = distributionMode,
            AutoReplyMessage = string.IsNullOrWhiteSpace(model.AutoReplyMessage) ? null : model.AutoReplyMessage.Trim(),
            ClosedAutoReplyMessage = string.IsNullOrWhiteSpace(model.ClosedAutoReplyMessage) ? null : model.ClosedAutoReplyMessage.Trim(),
        });

        return Edit(entryPoint, context);
    }

    private bool AnswersMessages(ContactCenterEntryPoint entryPoint)
        => _channelResolver.Get(entryPoint.GetChannel()) is not null;

    private async Task<bool> IsRoutedDistributionEnabledAsync()
    {
        var features = await _shellFeaturesManager.GetEnabledFeaturesAsync();

        return features.Any(feature => string.Equals(feature.Id, MessagingConstants.Feature.RoutedDistribution, StringComparison.Ordinal));
    }
}
