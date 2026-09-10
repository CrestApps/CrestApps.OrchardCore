using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Services;
using CrestApps.OrchardCore.ContactCenter.ViewModels;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.ContactCenter.Drivers;

internal sealed class ActivityQueueDisplayDriver : DisplayDriver<ActivityQueue>
{
    private readonly ContactCenterAdminFormOptionsProvider _optionsProvider;
    private readonly IActivityQueueGroupManager _queueGroupManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="ActivityQueueDisplayDriver"/> class.
    /// </summary>
    /// <param name="optionsProvider">The admin form options provider.</param>
    /// <param name="queueGroupManager">The queue-group manager.</param>
    public ActivityQueueDisplayDriver(
        ContactCenterAdminFormOptionsProvider optionsProvider,
        IActivityQueueGroupManager queueGroupManager)
    {
        _optionsProvider = optionsProvider;
        _queueGroupManager = queueGroupManager;
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> DisplayAsync(ActivityQueue queue, BuildDisplayContext context)
    {
        var queueGroup = string.IsNullOrEmpty(queue.QueueGroupId)
            ? null
            : await _queueGroupManager.FindByIdAsync(queue.QueueGroupId);

        return await CombineAsync(
            Initialize<QueueSummaryViewModel>("ActivityQueue_Fields_SummaryAdmin", model =>
            {
                model.Queue = queue;
                model.QueueGroupName = queueGroup?.Name;
            })
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1"),
            View("ActivityQueue_Buttons_SummaryAdmin", queue)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Actions:5"),
            View("ActivityQueue_DefaultMeta_SummaryAdmin", queue)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:5")
        );
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> EditAsync(ActivityQueue queue, BuildEditorContext context)
    {
        var viewModel = CreateViewModel(queue);

        await _optionsProvider.PopulateQueueEditorAsync(viewModel);

        return Initialize<QueueViewModel>("ActivityQueueFields_Edit", model =>
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
            model.Enabled = viewModel.Enabled;
        }).Location("Content:1");
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
        queue.RequiredSkills = SkillTag.NormalizeAll(model.RequiredSkills);
        queue.SkillRequirements = ReadSkillRequirements(model.SkillRequirements);
        queue.InboundChannelEndpointId = string.IsNullOrWhiteSpace(model.InboundChannelEndpointId)
            ? null
            : model.InboundChannelEndpointId.Trim();
        queue.BusinessHoursCalendarId = string.IsNullOrWhiteSpace(model.BusinessHoursCalendarId)
            ? null
            : model.BusinessHoursCalendarId.Trim();
        queue.AfterHoursAction = model.AfterHoursAction;
        queue.OverflowQueueId = string.IsNullOrWhiteSpace(model.OverflowQueueId) || string.Equals(model.OverflowQueueId, queue.ItemId, StringComparison.Ordinal)
            ? null
            : model.OverflowQueueId.Trim();
        queue.OverflowAfterSeconds = Math.Max(0, model.OverflowAfterSeconds);
        queue.OverflowTargets = ReadOverflowTargets(model.OverflowTargets, queue.ItemId);
        queue.MaxQueueSize = Math.Max(0, model.MaxQueueSize);
        queue.QueueFullAction = model.QueueFullAction;
        queue.MaxWaitSeconds = Math.Max(0, model.MaxWaitSeconds);
        queue.MaxWaitAction = model.MaxWaitAction;
        queue.FirstResponseTargetSeconds = Math.Max(0, model.FirstResponseTargetSeconds);
        queue.Treatment = ReadTreatment(model.Treatment ?? new QueueTreatmentViewModel());
        queue.Enabled = model.Enabled;

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
    /// Reads the skill rows, keeping one per skill and only those that name a skill; a blank row is one the
    /// operator added and never filled in.
    /// </summary>
    private static List<QueueSkillRequirement> ReadSkillRequirements(IEnumerable<QueueSkillRequirementViewModel> rows)
    {
        var requirements = new List<QueueSkillRequirement>();
        var seen = new HashSet<SkillTag>();

        foreach (var row in rows ?? [])
        {
            if (row is null || !SkillTag.TryCreate(row.SkillId, out var skill) || !seen.Add(skill))
            {
                continue;
            }

            requirements.Add(new QueueSkillRequirement
            {
                SkillId = skill.Value,
                MinimumProficiency = Math.Clamp(row.MinimumProficiency, AgentSkill.MinimumProficiency, AgentSkill.MaximumProficiency),
                Required = row.Required,
                RelaxAfterSeconds = row.RelaxAfterSeconds > 0 ? row.RelaxAfterSeconds : null,
            });
        }

        return requirements;
    }

    /// <summary>
    /// Reads the overflow hops, dropping blank rows and a hop back to this queue, keeping the first row for a
    /// queue named twice, and ordering them the way callers walk them.
    /// </summary>
    private static List<QueueOverflowTarget> ReadOverflowTargets(IEnumerable<QueueOverflowTargetViewModel> rows, string queueId)
    {
        var targets = new List<QueueOverflowTarget>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows ?? [])
        {
            var targetQueueId = row?.QueueId?.Trim();

            if (string.IsNullOrEmpty(targetQueueId) ||
                string.Equals(targetQueueId, queueId, StringComparison.OrdinalIgnoreCase) ||
                !seen.Add(targetQueueId))
            {
                continue;
            }

            targets.Add(new QueueOverflowTarget
            {
                QueueId = targetQueueId,
                AfterSeconds = Math.Max(0, row.AfterSeconds),
            });
        }

        return targets
            .OrderBy(target => target.AfterSeconds)
            .ToList();
    }

    private static QueueTreatmentSettings ReadTreatment(QueueTreatmentViewModel model)
    {
        var callbackKey = model.CallbackDtmfKey?.Trim();
        var minimumEstimate = Math.Max(0, model.MinimumEstimateSeconds);

        return new QueueTreatmentSettings
        {
            WelcomeMessage = string.IsNullOrWhiteSpace(model.WelcomeMessage) ? null : model.WelcomeMessage.Trim(),
            AnnouncementIntervalSeconds = Math.Max(0, model.AnnouncementIntervalSeconds),
            AnnouncePosition = model.AnnouncePosition,
            AnnounceEstimatedWait = model.AnnounceEstimatedWait,
            HoldMusicMediaId = string.IsNullOrWhiteSpace(model.HoldMusicMediaId) ? null : model.HoldMusicMediaId.Trim(),
            CallbackDtmfKey = string.IsNullOrEmpty(callbackKey) ? null : callbackKey,
            CallbackOfferAfterSeconds = Math.Max(0, model.CallbackOfferAfterSeconds),
            MinimumEstimateSeconds = minimumEstimate,
            MaximumEstimateSeconds = Math.Max(minimumEstimate, model.MaximumEstimateSeconds),
            AverageHandleTimeSeconds = Math.Max(0, model.AverageHandleTimeSeconds),
        };
    }
}
