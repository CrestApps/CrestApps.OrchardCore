using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Core.Services.Retention;
using CrestApps.OrchardCore.ContactCenter.Deployments.Sources;
using CrestApps.OrchardCore.ContactCenter.Deployments.Steps;
using CrestApps.OrchardCore.ContactCenter.Drivers;
using CrestApps.OrchardCore.ContactCenter.Endpoints;
using CrestApps.OrchardCore.ContactCenter.Handlers;
using CrestApps.OrchardCore.ContactCenter.Indexes;
using CrestApps.OrchardCore.ContactCenter.Migrations;
using CrestApps.OrchardCore.ContactCenter.Recipes;
using CrestApps.OrchardCore.ContactCenter.Services;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Localization;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Recipes;

namespace CrestApps.OrchardCore.ContactCenter;

/// <summary>
/// Registers the Contact Center Inbound Voice feature: inbound voice entry-point administration, qualification,
/// and queue ingress.
/// </summary>
[Feature(ContactCenterConstants.Feature.InboundVoice)]
public sealed class InboundVoiceStartup : StartupBase
{
    private readonly IStringLocalizer S;

    public InboundVoiceStartup(IStringLocalizer<InboundVoiceStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.TryAddScoped<IIvrProvider, NoIvrProvider>();

        // Calls are something a phone number does, so taking them is a capability this feature offers on the phone
        // numbers in the address list. Outbound Lines offers the same capability for dialing out.
        services.AddOmnichannelAddressCapability(OmnichannelAddressTypes.PhoneNumber, OmnichannelConstants.Channels.Phone, capability =>
        {
            capability.DisplayName = S["Voice calls"];
            capability.Description = S["Calls to and from this number."];
        });

        // Calls are a channel entry points answer, so adding an entry point offers a call one with this feature.
        services.AddEntryPointChannel(OmnichannelConstants.Channels.Phone, channel =>
        {
            channel.DisplayName = S["Voice calls"];
            channel.Description = S["Answers calls to its numbers: routes them to a queue or an agent, with opening hours, a welcome message, a phone menu and voicemail."];
        });

        services
            // The numbers customers dial in on are the contact center's own, so no transfer is ever sent back to one.
            .AddScoped<IContactCenterOwnNumberSource, EntryPointOwnNumberSource>()
            // Entry-point phone menus. They belong here because the menu lives on the entry point: the
            // resolver reads it, and the base feature has no entry points to read.
            .AddScoped<IIvrExecutionService, IvrExecutionService>()
            .AddScoped<IEntryPointFlowResolver, EntryPointFlowResolver>()
            .AddScoped<IInboundVoiceDigitsSink, InboundVoiceDigitsSink>()
            .AddScoped<IIvrCallRouter, IvrCallRouter>()
            .AddScoped<IIvrExternalTransferService, IvrExternalTransferService>()
            // Whether a menu's transfer to an outside number connected, so a failed one reroutes the caller.
            .AddScoped<IExternalTransferOutcomeSink, IvrExternalTransferOutcomeSink>()
            // A waiting caller's answer to the queue's callback offer arrives on the same key-press path as a menu.
            .AddScoped<IQueueCallbackOfferResponder, QueueCallbackOfferResponder>()
            .AddScoped<IEntryPointResolver, EntryPointResolver>()
            .AddScoped<IPendingIncomingCallOfferService, PendingIncomingCallOfferService>()
            .AddScoped<QueuedVoiceWorkOfferScopeContext>()
            .AddScoped<IContactCenterEventHandler, OfferQueuedVoiceWorkOnAvailabilityHandler>();

        // Caller-based priority: the entry point says what the number is worth, the contributors notice what
        // this particular caller is worth, and the strongest of the two decides where they land in line.
        services.AddScoped<IInboundPriorityResolver, InboundPriorityResolver>();
        services.AddScoped<IInboundPriorityContributor, ReturningCallbackPriorityContributor>();
        services.AddScoped<IInboundPriorityContributor, RepeatCallerPriorityContributor>();

        // This feature is the one that queues inbound voice work, so it replaces the do-nothing default the base
        // feature registers for tenants without it.
        services.Replace(ServiceDescriptor.Scoped<IQueuedVoiceWorkOfferService, QueuedVoiceWorkOfferService>());

        // The settings only a call entry point has: the welcome message, the phone menu, voicemail and how long an
        // agent's line rings. The entry point administration itself belongs to the Inbound Entry Points feature.
        services.AddDisplayDriver<ContactCenterEntryPoint, ContactCenterEntryPointVoiceDisplayDriver>();
        services.AddResourceConfiguration<ContactCenterIvrMenuEditorResourceConfiguration>();

        // Queue shared voicemail boxes. They belong here because only an inbound queue line delivers to one: the entry
        // point chooses the box, and the messages are the voicemails its callers leave.
        services
            .AddScoped<ISharedVoicemailStore, SharedVoicemailStore>()
            .AddScoped<ISharedVoicemailManager, SharedVoicemailManager>()
            .AddScoped<ILeadConversionRepointer, VoicemailLeadConversionRepointer>()
            .AddScoped<ISharedVoicemailAuthorizationService, SharedVoicemailAuthorizationService>()
            .AddScoped<ISharedVoicemailService, SharedVoicemailService>()
            .AddScoped<IContactCenterEventHandler, SharedVoicemailProjectionHandler>()
            .AddScoped<IContactCenterRetentionPolicy, SharedVoicemailRetentionPolicy>()
            .AddIndexProvider<SharedVoicemailIndexProvider>()
            .AddDataMigration<SharedVoicemailIndexMigrations>();
        services.AddNavigationProvider<ContactCenterSharedVoicemailAdminMenu>();
    }

    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        routes.AddVoiceIngressEndpoint();
    }
}
