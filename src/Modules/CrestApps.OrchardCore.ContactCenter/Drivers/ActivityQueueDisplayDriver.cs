using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Services;
using CrestApps.OrchardCore.ContactCenter.ViewModels;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.ContactCenter.Drivers;

internal sealed class ActivityQueueDisplayDriver : DisplayDriver<ActivityQueue>
{
    private readonly ContactCenterAdminFormOptionsProvider _optionsProvider;
    private readonly IActivityQueueGroupManager _queueGroupManager;
    private readonly bool _routesFromEntryPoints;

    /// <summary>
    /// Initializes a new instance of the <see cref="ActivityQueueDisplayDriver"/> class.
    /// </summary>
    /// <param name="optionsProvider">The admin form options provider.</param>
    /// <param name="queueGroupManager">The queue-group manager.</param>
    /// <param name="entryPointManagers">The entry point catalog, present only while inbound entry points are enabled.</param>
    public ActivityQueueDisplayDriver(
        ContactCenterAdminFormOptionsProvider optionsProvider,
        IActivityQueueGroupManager queueGroupManager,
        IEnumerable<IContactCenterEntryPointManager> entryPointManagers)
    {
        _optionsProvider = optionsProvider;
        _queueGroupManager = queueGroupManager;

        // With inbound entry points, a number is routed from its entry point; the queue's own number would be a second
        // place to route it from, so it is not offered.
        _routesFromEntryPoints = entryPointManagers.Any();
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> DisplayAsync(ActivityQueue queue, BuildDisplayContext context)
    {
        var queueGroup = string.IsNullOrEmpty(queue.QueueGroupId)
            ? null
            : await _queueGroupManager.FindByIdAsync(queue.QueueGroupId);

        // The row's classes and attributes are rendered on its row element, in both admin list layouts.
        if (context.DisplayType is OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid
            && context.Shape is Shape rowShape)
        {
            rowShape.Classes.Add("item");
            rowShape.Attributes["data-filter-value"] = queue.Name?.ToLowerInvariant() ?? string.Empty;
        }

        return await CombineAsync(
            View("ActivityQueue_Fields_SummaryAdmin", queue)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            View("ActivityQueue_Description_SummaryAdmin", queue)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Description:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:5")
                .RenderWhen(() => Task.FromResult(!string.IsNullOrWhiteSpace(queue.Description))),
            Initialize<QueueSummaryViewModel>("ActivityQueue_DefaultTags_SummaryAdmin", model =>
            {
                model.Queue = queue;
                model.QueueGroupName = queueGroup?.Name;
            })
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Routing:5"),
            View("ActivityQueue_Buttons_SummaryAdmin", queue).Location("Actions:5"),
            View("ActivityQueue_DefaultMeta_SummaryAdmin", queue)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Modified:5"),
            View("ActivityQueue_CloneActionsMenu_SummaryAdmin", queue).Location("ActionsMenu:5")
        );
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> EditAsync(ActivityQueue queue, BuildEditorContext context)
    {
        var viewModel = CreateViewModel(queue);
        viewModel.RoutesFromEntryPoints = _routesFromEntryPoints;

        await _optionsProvider.PopulateQueueEditorAsync(viewModel);

        // A queue has more settings than one screen can take in, so they are grouped in cards by what they govern.
        // Every card edits the same model under the same prefix, so the one form still posts all of them together.
        void Populate(QueueViewModel model)
        {
            model.Id = viewModel.Id;
            model.QueueGroupId = viewModel.QueueGroupId;
            model.QueueGroupOptions = viewModel.QueueGroupOptions;
            model.Name = viewModel.Name;
            model.Description = viewModel.Description;
            model.DefaultPriority = viewModel.DefaultPriority;
            model.RoutingStrategy = viewModel.RoutingStrategy;
            model.PreferStickyAgent = viewModel.PreferStickyAgent;
            model.EnableSlaAging = viewModel.EnableSlaAging;
            model.SlaThresholdSeconds = viewModel.SlaThresholdSeconds;
            model.ReservationTimeoutSeconds = viewModel.ReservationTimeoutSeconds;
            model.UnansweredOfferAction = viewModel.UnansweredOfferAction;
            model.RequiredSkills = viewModel.RequiredSkills;
            model.SkillRequirements = viewModel.SkillRequirements;
            model.SkillOptions = viewModel.SkillOptions;
            model.InboundChannelEndpointId = viewModel.InboundChannelEndpointId;
            model.InboundChannelEndpointOptions = viewModel.InboundChannelEndpointOptions;
            model.RoutesFromEntryPoints = viewModel.RoutesFromEntryPoints;
            model.BusinessHoursCalendarId = viewModel.BusinessHoursCalendarId;
            model.BusinessHoursCalendarOptions = viewModel.BusinessHoursCalendarOptions;
            model.AfterHoursAction = viewModel.AfterHoursAction;
            model.OverflowQueueId = viewModel.OverflowQueueId;
            model.OverflowQueueOptions = viewModel.OverflowQueueOptions;
            model.OverflowAfterSeconds = viewModel.OverflowAfterSeconds;
            model.OverflowTargets = viewModel.OverflowTargets;
            model.MaxQueueSize = viewModel.MaxQueueSize;
            model.QueueFullAction = viewModel.QueueFullAction;
            model.MaxWaitSeconds = viewModel.MaxWaitSeconds;
            model.MaxWaitAction = viewModel.MaxWaitAction;
            model.FirstResponseTargetSeconds = viewModel.FirstResponseTargetSeconds;
            model.Treatment = viewModel.Treatment;
            model.HoldMusicMediaOptions = viewModel.HoldMusicMediaOptions;
            model.Enabled = viewModel.Enabled;
        }

        return Combine(
            Initialize<QueueViewModel>("ActivityQueueGeneral_Edit", Populate).Location("Content:1%General;1"),
            Initialize<QueueViewModel>("ActivityQueueRouting_Edit", Populate).Location("Content:1%Routing;2"),
            Initialize<QueueViewModel>("ActivityQueueServiceLevels_Edit", Populate).Location("Content:1%Service levels;3"),
            Initialize<QueueViewModel>("ActivityQueueSkills_Edit", Populate).Location("Content:1%Skills;4"),
            Initialize<QueueViewModel>("ActivityQueueHoursAndOverflow_Edit", Populate).Location("Content:1%Hours and overflow;5"),
            Initialize<QueueViewModel>("ActivityQueueLimits_Edit", Populate).Location("Content:1%Limits;6"),
            Initialize<QueueViewModel>("ActivityQueueWaiting_Edit", Populate).Location("Content:1%While callers wait;7"),
            Initialize<QueueViewModel>("ActivityQueueCallback_Edit", Populate).Location("Content:1%Callback;8"));
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(ActivityQueue queue, UpdateEditorContext context)
    {
        var model = new QueueViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        queue.Name = model.Name?.Trim();
        queue.QueueGroupId = string.IsNullOrWhiteSpace(model.QueueGroupId)
            ? null
            : model.QueueGroupId.Trim();
        queue.Description = model.Description?.Trim();
        queue.DefaultPriority = model.DefaultPriority;
        queue.RoutingStrategy = model.RoutingStrategy;
        queue.PreferStickyAgent = model.PreferStickyAgent;
        queue.EnableSlaAging = model.EnableSlaAging;
        queue.SlaThresholdSeconds = model.SlaThresholdSeconds;
        queue.ReservationTimeoutSeconds = model.ReservationTimeoutSeconds;
        queue.UnansweredOfferAction = model.UnansweredOfferAction;
        queue.RequiredSkills = model.RequiredSkills;
        queue.SkillRequirements = ReadSkillRequirements(model.SkillRequirements);
        if (!_routesFromEntryPoints)
        {
            queue.InboundChannelEndpointId = string.IsNullOrWhiteSpace(model.InboundChannelEndpointId)
                ? null
                : model.InboundChannelEndpointId.Trim();
        }
        queue.BusinessHoursCalendarId = string.IsNullOrWhiteSpace(model.BusinessHoursCalendarId)
            ? null
            : model.BusinessHoursCalendarId.Trim();
        queue.AfterHoursAction = model.AfterHoursAction;
        queue.OverflowQueueId = model.OverflowQueueId;
        queue.OverflowAfterSeconds = model.OverflowAfterSeconds;
        queue.OverflowTargets = ReadOverflowTargets(model.OverflowTargets);
        queue.MaxQueueSize = model.MaxQueueSize;
        queue.QueueFullAction = model.QueueFullAction;
        queue.MaxWaitSeconds = model.MaxWaitSeconds;
        queue.MaxWaitAction = model.MaxWaitAction;
        queue.FirstResponseTargetSeconds = model.FirstResponseTargetSeconds;
        queue.Treatment = ReadTreatment(model.Treatment ?? new QueueTreatmentViewModel());
        queue.Enabled = model.Enabled;

        // Skill tags, overflow hops, counts and the treatment are tidied by the same rules the AI queue tool applies.
        ActivityQueueEditorRules.Normalize(queue);

        // Whether the limits and treatment make sense together (an overflow action with nowhere to overflow
        // to, a callback key that is not a telephone key) is the queue's own rule, enforced by
        // ActivityQueueHandler so a recipe import and this editor reject the same queues.
        return await EditAsync(queue, context);
    }

    private static QueueViewModel CreateViewModel(ActivityQueue queue)
    {
        var treatment = queue.Treatment ?? new QueueTreatmentSettings();

        return new QueueViewModel
        {
            Id = queue.ItemId,
            QueueGroupId = queue.QueueGroupId,
            Name = queue.Name,
            Description = queue.Description,
            DefaultPriority = queue.DefaultPriority,
            RoutingStrategy = queue.RoutingStrategy,
            PreferStickyAgent = queue.PreferStickyAgent,
            EnableSlaAging = queue.EnableSlaAging,
            SlaThresholdSeconds = queue.SlaThresholdSeconds,
            ReservationTimeoutSeconds = queue.ReservationTimeoutSeconds,
            UnansweredOfferAction = queue.UnansweredOfferAction,
            RequiredSkills = queue.RequiredSkills,
            SkillRequirements = queue.SkillRequirements
                .Where(requirement => requirement is not null)
                .Select(requirement => new QueueSkillRequirementViewModel
                {
                    SkillId = requirement.SkillId,
                    MinimumProficiency = requirement.MinimumProficiency,
                    Required = requirement.Required,
                    RelaxAfterSeconds = requirement.RelaxAfterSeconds ?? 0,
                })
                .ToList(),
            InboundChannelEndpointId = queue.InboundChannelEndpointId,
            BusinessHoursCalendarId = queue.BusinessHoursCalendarId,
            AfterHoursAction = queue.AfterHoursAction,
            OverflowQueueId = queue.OverflowQueueId,
            OverflowAfterSeconds = queue.OverflowAfterSeconds,
            OverflowTargets = queue.OverflowTargets
                .Where(target => target is not null)
                .Select(target => new QueueOverflowTargetViewModel
                {
                    QueueId = target.QueueId,
                    AfterSeconds = target.AfterSeconds,
                })
                .ToList(),
            MaxQueueSize = queue.MaxQueueSize,
            QueueFullAction = queue.QueueFullAction,
            MaxWaitSeconds = queue.MaxWaitSeconds,
            MaxWaitAction = queue.MaxWaitAction,
            FirstResponseTargetSeconds = queue.FirstResponseTargetSeconds,
            Treatment = new QueueTreatmentViewModel
            {
                WelcomeMessage = treatment.WelcomeMessage,
                AnnouncementIntervalSeconds = treatment.AnnouncementIntervalSeconds,
                AnnouncePosition = treatment.AnnouncePosition,
                AnnounceEstimatedWait = treatment.AnnounceEstimatedWait,
                HoldMusicMediaId = treatment.HoldMusicMediaId,
                CallbackDtmfKey = treatment.CallbackDtmfKey,
                CallbackOfferAfterSeconds = treatment.CallbackOfferAfterSeconds,
                MinimumEstimateSeconds = treatment.MinimumEstimateSeconds,
                MaximumEstimateSeconds = treatment.MaximumEstimateSeconds,
                AverageHandleTimeSeconds = treatment.AverageHandleTimeSeconds,
            },
            Enabled = queue.Enabled,
        };
    }

    /// <summary>
    /// Reads the skill rows as entered; <see cref="ActivityQueueEditorRules"/> keeps one per skill and only those that
    /// name a skill, since a blank row is one the operator added and never filled in.
    /// </summary>
    private static List<QueueSkillRequirement> ReadSkillRequirements(IEnumerable<QueueSkillRequirementViewModel> rows)
        => (rows ?? [])
            .Where(row => row is not null)
            .Select(row => new QueueSkillRequirement
            {
                SkillId = row.SkillId,
                MinimumProficiency = row.MinimumProficiency,
                Required = row.Required,
                RelaxAfterSeconds = row.RelaxAfterSeconds,
            })
            .ToList();

    /// <summary>
    /// Reads the overflow hops as entered; <see cref="ActivityQueueEditorRules"/> drops blank rows and a hop back to
    /// this queue, keeps the first row for a queue named twice, and orders them the way callers walk them.
    /// </summary>
    private static List<QueueOverflowTarget> ReadOverflowTargets(IEnumerable<QueueOverflowTargetViewModel> rows)
        => (rows ?? [])
            .Where(row => row is not null)
            .Select(row => new QueueOverflowTarget
            {
                QueueId = row.QueueId,
                AfterSeconds = row.AfterSeconds,
            })
            .ToList();

    private static QueueTreatmentSettings ReadTreatment(QueueTreatmentViewModel model)
        => new()
        {
            WelcomeMessage = model.WelcomeMessage,
            AnnouncementIntervalSeconds = model.AnnouncementIntervalSeconds,
            AnnouncePosition = model.AnnouncePosition,
            AnnounceEstimatedWait = model.AnnounceEstimatedWait,
            HoldMusicMediaId = model.HoldMusicMediaId,
            CallbackDtmfKey = model.CallbackDtmfKey,
            CallbackOfferAfterSeconds = model.CallbackOfferAfterSeconds,
            MinimumEstimateSeconds = model.MinimumEstimateSeconds,
            MaximumEstimateSeconds = model.MaximumEstimateSeconds,
            AverageHandleTimeSeconds = model.AverageHandleTimeSeconds,
        };
}
