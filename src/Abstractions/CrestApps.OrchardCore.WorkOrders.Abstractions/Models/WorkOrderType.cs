using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.WorkOrders.Models;

/// <summary>
/// A skill a work order needs, at a lowest proficiency.
/// </summary>
public sealed class RequiredSkill
{
    /// <summary>
    /// Gets or sets the skill.
    /// </summary>
    public string SkillId { get; set; }

    /// <summary>
    /// Gets or sets the lowest proficiency, from 1.
    /// </summary>
    public int MinProficiency { get; set; } = 1;
}

/// <summary>
/// A kind of job, with the defaults a new work order of it takes.
/// </summary>
public sealed class WorkOrderType : CatalogItem
{
    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the color.
    /// </summary>
    public string Color { get; set; }

    /// <summary>
    /// Gets or sets how long the job usually takes, in minutes.
    /// </summary>
    public int DefaultDurationMinutes { get; set; } = 60;

    /// <summary>
    /// Gets or sets the skills it needs.
    /// </summary>
    public List<RequiredSkill> RequiredSkills { get; set; } = [];

    /// <summary>
    /// Gets or sets the certifications it needs.
    /// </summary>
    public List<string> RequiredCertificationIds { get; set; } = [];

    /// <summary>
    /// Gets or sets how many people it needs.
    /// </summary>
    public int CrewSize { get; set; } = 1;

    /// <summary>
    /// Gets or sets the asset types of equipment it needs.
    /// </summary>
    public List<string> RequiredEquipmentTypeIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the checklists it comes with.
    /// </summary>
    public List<string> ChecklistTemplateIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the lines it comes with.
    /// </summary>
    public List<WorkOrderLine> DefaultLines { get; set; } = [];

    /// <summary>
    /// Gets or sets the service level policy.
    /// </summary>
    public string ServiceLevelPolicyId { get; set; }

    /// <summary>
    /// Gets or sets how it is billed.
    /// </summary>
    public WorkOrderBillingMode BillingMode { get; set; }

    /// <summary>
    /// Gets or sets the currency its prices are in.
    /// </summary>
    public string Currency { get; set; } = "USD";

    /// <summary>
    /// Gets or sets the fixed price, when billed at one.
    /// </summary>
    public decimal? FixedPrice { get; set; }

    /// <summary>
    /// Gets or sets the priority of a new work order.
    /// </summary>
    public WorkOrderPriority DefaultPriority { get; set; } = WorkOrderPriority.Normal;

    /// <summary>
    /// Gets or sets a value indicating whether it can't be completed without the customer's signature.
    /// </summary>
    public bool RequiresSignature { get; set; }

    /// <summary>
    /// Gets or sets how many photos it can't be completed without.
    /// </summary>
    public int RequiredPhotos { get; set; }

    /// <summary>
    /// Gets or sets when it was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }
}
