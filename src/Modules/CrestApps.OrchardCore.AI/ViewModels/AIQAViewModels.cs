using CrestApps.OrchardCore.AI.Core.Conversations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.AI.ViewModels;

/// <summary>
/// The QA dashboard.
/// </summary>
public class AIQADashboardViewModel
{
    /// <summary>
    /// Gets or sets the cohort shown, or <see langword="null"/> for every scored conversation.
    /// </summary>
    public string CohortId { get; set; }

    /// <summary>
    /// Gets or sets the period in days.
    /// </summary>
    public int Days { get; set; }

    /// <summary>
    /// Gets or sets the cohorts to choose from.
    /// </summary>
    public IList<SelectListItem> Cohorts { get; set; } = [];

    /// <summary>
    /// Gets or sets the periods to choose from.
    /// </summary>
    public IList<SelectListItem> Periods { get; set; } = [];

    /// <summary>
    /// Gets or sets the figures.
    /// </summary>
    public AIQADashboard Dashboard { get; set; } = new();
}

/// <summary>
/// The editor of a QA cohort.
/// </summary>
public class AIQACohortViewModel
{
    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the cohort scores conversations.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets the profiles sampled.
    /// </summary>
    public string[] ProfileIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the share sampled.
    /// </summary>
    public int SamplePercent { get; set; }

    /// <summary>
    /// Gets or sets the criteria, one per line.
    /// </summary>
    public string Criteria { get; set; }

    /// <summary>
    /// Gets or sets the passing score.
    /// </summary>
    public int PassingScore { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether answers are checked against the knowledge base.
    /// </summary>
    public bool CheckKnowledge { get; set; }

    /// <summary>
    /// Gets or sets the deployment that judges.
    /// </summary>
    public string JudgeDeploymentName { get; set; }

    /// <summary>
    /// Gets or sets the profiles to choose from.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> Profiles { get; set; } = [];

    /// <summary>
    /// Gets or sets the deployments to choose from.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> Deployments { get; set; } = [];
}
