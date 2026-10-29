using CrestApps.Core;
using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.AI.Core.Models;

/// <summary>
/// A QA cohort: a sample of the real conversations of some AI profiles, each scored against criteria after it closes.
/// </summary>
public sealed class AIQACohort : CatalogItem, INameAwareModel
{
    /// <summary>
    /// Gets or sets the cohort's name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether conversations are being scored.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the profiles whose conversations are sampled.
    /// </summary>
    public List<string> ProfileIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the share of conversations scored, from 1 to 100.
    /// </summary>
    public int SamplePercent { get; set; } = 20;

    /// <summary>
    /// Gets or sets what makes a conversation successful, one criterion per line.
    /// </summary>
    public string Criteria { get; set; }

    /// <summary>
    /// Gets or sets the score out of 100 a conversation needs to pass.
    /// </summary>
    public int PassingScore { get; set; } = 70;

    /// <summary>
    /// Gets or sets a value indicating whether the judge also checks the AI's answers against the profile's knowledge
    /// base and data sources.
    /// </summary>
    public bool CheckKnowledge { get; set; } = true;

    /// <summary>
    /// Gets or sets the deployment that judges, or <see langword="null"/> for the utility default.
    /// </summary>
    public string JudgeDeploymentName { get; set; }

    /// <summary>
    /// Gets or sets the last activity of the newest conversation already considered, so each is considered once.
    /// </summary>
    public DateTime? CursorUtc { get; set; }

    /// <summary>
    /// Gets or sets when the cohort was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets the label the cohort's evaluations carry.
    /// </summary>
    /// <returns>The label.</returns>
    public string GetLabel()
        => $"QA: {Name}";
}
