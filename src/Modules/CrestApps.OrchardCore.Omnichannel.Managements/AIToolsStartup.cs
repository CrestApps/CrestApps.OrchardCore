using CrestApps.Core.AI;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Records;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Omnichannel.Managements;

/// <summary>
/// Registers the AI tools that work omnichannel activities and activity loads, the cadence save tool and the tool that
/// deletes a cadence or subject action, the records of the Interaction Center screens, and the record kinds of the
/// generic record tools for campaigns, campaign groups and dispositions (read and written through their editors) and
/// cadences (read-only: the cadence editor posts its steps as a list of objects the editor replay cannot post). They are
/// gated on the screens' feature because the activity list filters the tools page with are registered by it. Every tool
/// and kind checks the caller's permission itself.
/// </summary>
[RequireFeatures(AIConstants.Feature.Area)]
[Feature(OmnichannelConstants.Features.Managements)]
public sealed class AIToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIToolsStartup(IStringLocalizer<AIToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        AddActivityTools(services);
        AddActivityBatchTools(services);

        services.AddCoreAITool<SaveOmnichannelCadenceTool>(SaveOmnichannelCadenceTool.TheName)
            .WithTitle(S["Save Omnichannel Cadence"])
            .WithDescription(S["Creates or updates a cadence and its ordered steps (delay in minutes, fixed message or AI-generated). A cadence drives automated follow-up messages for the activities that use it. Read the cadence first with getRecord (kind omnichannelCadence), which returns these field names."])
            .WithCategory(S["Omnichannel"])
            .Selectable();

        services.AddCoreAITool<DeleteOmnichannelRecordTool>(DeleteOmnichannelRecordTool.TheName)
            .WithTitle(S["Delete Omnichannel Record"])
            .WithDescription(S["Deletes one omnichannel cadence or subject flow action by kind (cadence, subjectAction) and id. Dispositions, lead statuses and opportunity stages are deleted with deleteRecord; campaigns, campaign groups and addresses cannot be deleted."])
            .WithCategory(S["Omnichannel"])
            .RequireApproval()
            .Selectable();

        services.AddAIRecordKind<OmnichannelCampaign>(OmnichannelRecordKinds.CampaignKind, kind => kind
            .WithTitle(S["Omnichannel campaign"])
            .WithDescription(S["an outreach campaign (name, description, group); never deleted"])
            .WithPermission(OmnichannelConstants.Permissions.ManageCampaigns)
            .ValidateUsing(OmnichannelRecordKinds.ValidateCampaignAsync)
            .WithProjection(OmnichannelRecordKinds.DescribeCampaign)
            .WithInUseCheck(OmnichannelRecordKinds.RefuseCampaignDeleteAsync)
            .UseEditorReplay());

        services.AddAIRecordKind<OmnichannelCampaignGroup>(OmnichannelRecordKinds.CampaignGroupKind, kind => kind
            .WithTitle(S["Omnichannel campaign group"])
            .WithDescription(S["a group of campaigns; never deleted"])
            .WithPermission(OmnichannelConstants.Permissions.ManageCampaignGroups)
            .WithProjection(OmnichannelRecordKinds.DescribeCampaignGroup)
            .WithInUseCheck(OmnichannelRecordKinds.RefuseCampaignGroupDeleteAsync)
            .UseEditorReplay());

        services.AddAIRecordKind<OmnichannelDisposition>(OmnichannelRecordKinds.DispositionKind, kind => kind
            .WithTitle(S["Omnichannel disposition"])
            .WithDescription(S["an outcome an activity is completed with"])
            .WithPermission(OmnichannelConstants.Permissions.ManageDispositions)
            .UseCatalogManager<INamedCatalogManager<OmnichannelDisposition>>()
            .ValidateUsing(OmnichannelRecordKinds.ValidateDispositionAsync)
            .WithProjection(OmnichannelRecordKinds.DescribeDisposition)
            .UseEditorReplay());

        services.AddAIRecordKind<Cadence>(OmnichannelRecordKinds.CadenceKind, kind => kind
            .WithTitle(S["Omnichannel cadence"])
            .WithDescription(S["a follow-up cadence and its steps"])
            .WithPermission(OmnichannelConstants.Permissions.ManageCadences)
            .WithProjection(OmnichannelRecordKinds.DescribeCadence)
            .ReadOnly(saveWith: SaveOmnichannelCadenceTool.TheName, deleteWith: DeleteOmnichannelRecordTool.TheName));
    }

    private void AddActivityTools(IServiceCollection services)
    {
        services.AddCoreAITool<ListOmnichannelActivitiesTool>(ListOmnichannelActivitiesTool.TheName)
            .WithTitle(S["List Omnichannel Activities"])
            .WithDescription(S["Lists CRM activities with paging. Scope mine returns the caller's scheduled manual activities, contact returns one contact's scheduled or completed activities, all applies the Manage Activities filters (status, campaign, dialer profile, assignee, dates, phone number). Use it to find activity ids before any change."])
            .WithCategory(S["Omnichannel"])
            .Selectable();

        services.AddCoreAITool<GetOmnichannelActivityTool>(GetOmnichannelActivityTool.TheName)
            .WithTitle(S["Get Omnichannel Activity"])
            .WithDescription(S["Returns one activity: contact, subject type, channel, schedule, assignment, status, attempts, disposition, notes, hand-off summary and whether an AI session ran it."])
            .WithCategory(S["Omnichannel"])
            .Selectable();

        services.AddCoreAITool<CompleteOmnichannelActivityTool>(CompleteOmnichannelActivityTool.TheName)
            .WithTitle(S["Complete Omnichannel Activity"])
            .WithDescription(S["Completes an activity with a disposition and notes. The disposition's subject actions then run (follow-up activity, retry, do-not-contact flags, lead conversion), so read the subject flow first when unsure."])
            .WithCategory(S["Omnichannel"])
            .Selectable();

        services.AddCoreAITool<PurgeOmnichannelActivitiesTool>(PurgeOmnichannelActivitiesTool.TheName)
            .WithTitle(S["Purge Omnichannel Activities"])
            .WithDescription(S["Purges activities by id so they are never worked. Cannot be undone; activities on a live call are skipped."])
            .WithCategory(S["Omnichannel"])
            .RequireApproval()
            .Selectable();
    }

    private void AddActivityBatchTools(IServiceCollection services)
    {
        services.AddCoreAITool<ListActivityBatchesTool>(ListActivityBatchesTool.TheName)
            .WithTitle(S["List Activity Loads"])
            .WithDescription(S["Lists activity loads with their source, campaign, channel, status (new, started, loading, loaded), totals loaded/matched/skipped and any load error. Pass an id to get the full definition of one load."])
            .WithCategory(S["Omnichannel"])
            .Selectable();

        services.AddCoreAITool<SaveActivityBatchTool>(SaveActivityBatchTool.TheName)
            .WithTitle(S["Copy Activity Load"])
            .WithDescription(S["Copies an existing activity load definition as a new, unloaded load, optionally with a new title. Saving does not load it."])
            .WithCategory(S["Omnichannel"])
            .Selectable();

        services.AddCoreAITool<PreviewActivityBatchTool>(PreviewActivityBatchTool.TheName)
            .WithTitle(S["Preview Activity Load"])
            .WithDescription(S["Counts how many records a saved load would match and how many would be skipped and why, without creating anything. Use it before loadActivityBatch."])
            .WithCategory(S["Omnichannel"])
            .Selectable();

        services.AddCoreAITool<LoadActivityBatchTool>(LoadActivityBatchTool.TheName)
            .WithTitle(S["Load Activity Load"])
            .WithDescription(S["Starts loading a saved batch in the background, creating one activity per match. For an automated or dialer batch this leads to real calls or text messages to customers. A batch can be loaded only once."])
            .WithCategory(S["Omnichannel"])
            .RequireApproval()
            .Selectable();

        services.AddCoreAITool<DeleteActivityBatchTool>(DeleteActivityBatchTool.TheName)
            .WithTitle(S["Delete Activity Load"])
            .WithDescription(S["Deletes an activity load record. Refused while it is loading. The activities it already created are kept."])
            .WithCategory(S["Omnichannel"])
            .RequireApproval()
            .Selectable();
    }
}

/// <summary>
/// Registers the AI tools for the subject flows, the numbers not in service, contact consent and the automated AI calls
/// and texts, the records of the Omnichannel Management screens. Every tool checks the caller's permission itself.
/// </summary>
[RequireFeatures(AIConstants.Feature.Area)]
[Feature(OmnichannelConstants.Features.Managements)]
public sealed class ManagementAIToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ManagementAIToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ManagementAIToolsStartup(IStringLocalizer<ManagementAIToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<ListSubjectFlowsTool>(ListSubjectFlowsTool.TheName)
            .WithTitle(S["List Subject Flows"])
            .WithDescription(S["Lists the subject content types with their flow settings and, for one subject, every action per disposition (type, what it does, do-not-contact flags). Use it to explain or audit what happens after a disposition."])
            .WithCategory(S["Omnichannel"])
            .Selectable();

        services.AddCoreAITool<SaveSubjectActionTool>(SaveSubjectActionTool.TheName)
            .WithTitle(S["Save Subject Action"])
            .WithDescription(S["Creates or updates one action of a subject flow: the subject type, the disposition that triggers it, the action type (Finish, TryAgain, NewActivity, ConvertLead) and its type-specific settings."])
            .WithCategory(S["Omnichannel"])
            .Selectable();

        services.AddCoreAITool<ListNotInServiceNumbersTool>(ListNotInServiceNumbersTool.TheName)
            .WithTitle(S["List Numbers Not In Service"])
            .WithDescription(S["Lists or searches phone numbers marked as not in service, with who or what marked them and when."])
            .WithCategory(S["Omnichannel"])
            .Selectable();

        services.AddCoreAITool<SetNumberNotInServiceTool>(SetNumberNotInServiceTool.TheName)
            .WithTitle(S["Set Number Not In Service"])
            .WithDescription(S["Marks one or more phone numbers as not in service so campaigns skip them, or clears the mark so they can be loaded and dialed again. Numbers must include the country code."])
            .WithCategory(S["Omnichannel"])
            .Selectable();

        services.AddCoreAITool<StartAIActivityTool>(StartAIActivityTool.TheName)
            .WithTitle(S["Queue AI Call or Text"])
            .WithDescription(S["Queues an automated AI phone call or AI text conversation to one phone number (E.164) with a given AI profile, optionally from a chosen address, for a contact, campaign and subject, now or at a scheduled time. It does not place the call or send the text itself: the queued activity reaches a real customer and uses paid telephony and model usage when it is placed. An opted-out number is refused up front, and the activity is screened again (opt-out, do-not-call, calling window, numbers not in service) when it is placed."])
            .WithCategory(S["Omnichannel"])
            .RequireApproval()
            .Selectable();

        services.AddCoreAITool<RecordContactOptOutTool>(RecordContactOptOutTool.TheName)
            .WithTitle(S["Record Contact Opt-Out"])
            .WithDescription(S["Records that a person asked not to be contacted by phone, text message or e-mail at a number or address, optionally for a CRM contact (whose do-not-call, do-not-text or do-not-e-mail flag is set). Opted-out numbers and addresses are skipped by every send, dial and campaign. It never opts anyone back in."])
            .WithCategory(S["Omnichannel"])
            .Selectable();

        services.AddCoreAITool<CheckContactConsentTool>(CheckContactConsentTool.TheName)
            .WithTitle(S["Check Contact Consent"])
            .WithDescription(S["Tells whether a person opted out of phone calls, text messages or e-mail at a number or address, optionally for a CRM contact, and what recorded the opt-out (the contact's flags, an opt-out recorded against the address, or a do-not-call list). It asks the consent service every send, dial and campaign asks; opted-out numbers and addresses are skipped."])
            .WithCategory(S["Omnichannel"])
            .Selectable();

        services.AddCoreAITool<CancelAIActivityTool>(CancelAIActivityTool.TheName)
            .WithTitle(S["Cancel AI Call or Text"])
            .WithDescription(S["Cancels an automated call or text that has not started yet (status not started, pending or scheduled). Returns a conflict when it is already in progress or finished."])
            .WithCategory(S["Omnichannel"])
            .Selectable();
    }
}

/// <summary>
/// Registers the AI tool that saves the business's own numbers and addresses, and the read-only
/// <c>omnichannelAddress</c> record kind that lists and reads them: an address is created for an address type the record
/// tools cannot pass, so <c>saveOmnichannelAddress</c> writes addresses. They belong to the Channel Endpoints feature,
/// which can be on without the full management screens. Every tool and kind checks the caller's permission itself.
/// </summary>
[RequireFeatures(AIConstants.Feature.Area)]
[Feature(OmnichannelConstants.Features.ChannelEndpoints)]
public sealed class ChannelEndpointsAIToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChannelEndpointsAIToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ChannelEndpointsAIToolsStartup(IStringLocalizer<ChannelEndpointsAIToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<SaveOmnichannelAddressTool>(SaveOmnichannelAddressTool.TheName)
            .WithTitle(S["Save Omnichannel Address"])
            .WithDescription(S["Creates or updates an omnichannel address: display name, address type, value, capabilities (phone, SMS, ...) and provider. Feature-specific settings are set on the address's admin page. An agent can hold only one number per channel; validation refuses a conflict. Read an address first with getRecord (kind omnichannelAddress), which returns these field names."])
            .WithCategory(S["Omnichannel"])
            .Selectable();

        services.AddAIRecordKind<OmnichannelChannelEndpoint>(OmnichannelRecordKinds.AddressKind, kind => kind
            .WithTitle(S["Omnichannel address"])
            .WithDescription(S["one of the business's own numbers or addresses (type, value, capabilities, provider)"])
            .WithPermission(OmnichannelConstants.Permissions.ManageChannelEndpoints)
            .WithProjection(OmnichannelRecordKinds.DescribeAddress)
            .ReadOnly(saveWith: SaveOmnichannelAddressTool.TheName));
    }
}

/// <summary>
/// Registers the AI tool of the Omnichannel CRM that converts leads, and the lead statuses and opportunity stages as
/// record kinds of the generic record tools, read and written through their editors with the rules of their admin pages.
/// Every tool and kind checks the caller's permission itself.
/// </summary>
[RequireFeatures(AIConstants.Feature.Area)]
[Feature(OmnichannelConstants.Features.Crm)]
public sealed class CrmAIToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="CrmAIToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public CrmAIToolsStartup(IStringLocalizer<CrmAIToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<ConvertLeadTool>(ConvertLeadTool.TheName)
            .WithTitle(S["Convert Lead"])
            .WithDescription(S["Converts a lead into a contact, optionally attaching it to a new or existing account and creating an opportunity. With preview it returns the matching contacts and accounts instead of converting. A converted lead cannot be converted again."])
            .WithCategory(S["Omnichannel"])
            .Selectable();

        services.AddAIRecordKind<LeadStatus>(OmnichannelRecordKinds.LeadStatusKind, kind => kind
            .WithTitle(S["Lead status"])
            .WithDescription(S["a lead status (order, kind, default and contacted flags)"])
            .WithPermission(OmnichannelConstants.Permissions.ManageLeadStatuses)
            .UseCatalogManager<INamedCatalogManager<LeadStatus>>()
            .ValidateUsing(OmnichannelRecordKinds.ValidateLeadStatusAsync)
            .SaveUsing(OmnichannelRecordKinds.SaveLeadStatusAsync)
            .WithProjection(OmnichannelRecordKinds.DescribeLeadStatus)
            .WithInUseCheck(OmnichannelRecordKinds.GetLeadStatusUseAsync)
            .UseEditorReplay());

        services.AddAIRecordKind<OpportunityStage>(OmnichannelRecordKinds.OpportunityStageKind, kind => kind
            .WithTitle(S["Opportunity stage"])
            .WithDescription(S["an opportunity pipeline stage (order, probability, closed, won)"])
            .WithPermission(OmnichannelConstants.Permissions.ManageOpportunityStages)
            .UseCatalogManager<INamedCatalogManager<OpportunityStage>>()
            .ValidateUsing(OmnichannelRecordKinds.ValidateOpportunityStageAsync)
            .WithProjection(OmnichannelRecordKinds.DescribeOpportunityStage)
            .UseEditorReplay());
    }
}
