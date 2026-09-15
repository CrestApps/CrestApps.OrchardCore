using System.ComponentModel.DataAnnotations;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.ContactCenter.ViewModels;

public class QueueViewModel
{
    public string Id { get; set; }

    public string QueueGroupId { get; set; }

    public IList<SelectListItem> QueueGroupOptions { get; set; } = [];

    [Required]
    public string Name { get; set; }

    public string Description { get; set; }

    public InteractionPriority DefaultPriority { get; set; } = InteractionPriority.Normal;

    public QueueRoutingStrategy RoutingStrategy { get; set; } = QueueRoutingStrategy.LongestIdle;

    public bool PreferStickyAgent { get; set; }

    public bool EnableSlaAging { get; set; }

    [Range(1, int.MaxValue)]
    public int SlaThresholdSeconds { get; set; } = 120;

    [Range(1, int.MaxValue)]
    public int ReservationTimeoutSeconds { get; set; } = 30;

    public UnansweredOfferAction UnansweredOfferAction { get; set; } = UnansweredOfferAction.Requeue;

    public string BusinessHoursCalendarId { get; set; }

    public IList<SelectListItem> BusinessHoursCalendarOptions { get; set; } = [];

    public QueueAfterHoursAction AfterHoursAction { get; set; } = QueueAfterHoursAction.HoldInQueue;

    public string OverflowQueueId { get; set; }

    public IList<SelectListItem> OverflowQueueOptions { get; set; } = [];

    [Range(0, int.MaxValue)]
    public int OverflowAfterSeconds { get; set; }

    /// <summary>
    /// The ordered overflow chain. When any hop is set it supersedes the single overflow queue above.
    /// </summary>
    public IList<QueueOverflowTargetViewModel> OverflowTargets { get; set; } = [];

    [Range(0, int.MaxValue)]
    public int MaxQueueSize { get; set; }

    public QueueMaxWaitAction QueueFullAction { get; set; }

    [Range(0, int.MaxValue)]
    public int MaxWaitSeconds { get; set; }

    public QueueMaxWaitAction MaxWaitAction { get; set; }

    [Range(0, int.MaxValue)]
    public int FirstResponseTargetSeconds { get; set; }

    public IList<string> RequiredSkills { get; set; } = [];

    /// <summary>
    /// The skills this queue asks of an agent, with the proficiency it needs and whether the requirement is
    /// dropped after a caller has waited. A skill listed here overrides the same tag in the plain required list.
    /// </summary>
    public IList<QueueSkillRequirementViewModel> SkillRequirements { get; set; } = [];

    public IList<SelectListItem> SkillOptions { get; set; } = [];

    public QueueTreatmentViewModel Treatment { get; set; } = new();

    public string InboundChannelEndpointId { get; set; }

    public IList<SelectListItem> InboundChannelEndpointOptions { get; set; } = [];

    public bool Enabled { get; set; } = true;
}
