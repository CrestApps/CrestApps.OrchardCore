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

/// <summary>
/// The editor of an AI alert rule.
/// </summary>
public class AIAlertRuleViewModel
{
    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the rule is checked.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets the metric.
    /// </summary>
    public AIConversationMetric Metric { get; set; }

    /// <summary>
    /// Gets or sets the profile measured; empty for every profile.
    /// </summary>
    public string ProfileId { get; set; }

    /// <summary>
    /// Gets or sets how the value is compared.
    /// </summary>
    public AIConversationAlertCondition Condition { get; set; }

    /// <summary>
    /// Gets or sets the threshold.
    /// </summary>
    public double Threshold { get; set; }

    /// <summary>
    /// Gets or sets the window in minutes.
    /// </summary>
    public int WindowMinutes { get; set; }

    /// <summary>
    /// Gets or sets the fewest samples a window needs.
    /// </summary>
    public int MinimumSampleSize { get; set; }

    /// <summary>
    /// Gets or sets how often the rule is checked, in minutes.
    /// </summary>
    public int CheckEveryMinutes { get; set; }

    /// <summary>
    /// Gets or sets the addresses emailed, comma separated.
    /// </summary>
    public string NotifyEmails { get; set; }

    /// <summary>
    /// Gets or sets the profiles to choose from.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> Profiles { get; set; } = [];
}

/// <summary>
/// The editor of the AI cost settings.
/// </summary>
public class AICostSettingsViewModel
{
    /// <summary>
    /// Gets or sets the currency.
    /// </summary>
    public string Currency { get; set; }

    /// <summary>
    /// Gets or sets the carrier's price of a call minute.
    /// </summary>
    public decimal TelephonyPerMinute { get; set; }

    /// <summary>
    /// Gets or sets the carrier's price of a text message.
    /// </summary>
    public decimal SmsPerMessage { get; set; }

    /// <summary>
    /// Gets or sets the deployment prices, one per line.
    /// </summary>
    public string DeploymentPrices { get; set; }
}

/// <summary>
/// The editor of the contact memory instructions.
/// </summary>
public class AIContactMemorySettingsViewModel
{
    /// <summary>
    /// Gets or sets the tenant's own instructions; empty uses the built-in ones.
    /// </summary>
    public string Instructions { get; set; }

    /// <summary>
    /// Gets or sets the built-in instructions, shown as the placeholder.
    /// </summary>
    [BindNever]
    public string DefaultInstructions { get; set; }
}
