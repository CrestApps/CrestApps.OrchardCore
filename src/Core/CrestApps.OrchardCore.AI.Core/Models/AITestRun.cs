using CrestApps.Core.Models;
using CrestApps.OrchardCore.AI.Core.Conversations;

namespace CrestApps.OrchardCore.AI.Core.Models;

/// <summary>
/// One run of an <see cref="AITestCase"/>: the simulated conversation and the judge's verdict.
/// </summary>
public sealed class AITestRun : CatalogItem
{
    /// <summary>
    /// Gets or sets the test that ran.
    /// </summary>
    public string TestCaseId { get; set; }

    /// <summary>
    /// Gets or sets the test's name when it ran.
    /// </summary>
    public string TestCaseName { get; set; }

    /// <summary>
    /// Gets or sets the profile under test.
    /// </summary>
    public string ProfileId { get; set; }

    /// <summary>
    /// Gets or sets the version of the profile that answered.
    /// </summary>
    public int? ProfileVersion { get; set; }

    /// <summary>
    /// Gets or sets the batch the run belongs to, so the runs started together are reported together.
    /// </summary>
    public string BatchId { get; set; }

    /// <summary>
    /// Gets or sets where the run stands.
    /// </summary>
    public AITestRunStatus Status { get; set; }

    /// <summary>
    /// Gets or sets when the run was queued.
    /// </summary>
    public DateTime QueuedUtc { get; set; }

    /// <summary>
    /// Gets or sets when the run finished.
    /// </summary>
    public DateTime? CompletedUtc { get; set; }

    /// <summary>
    /// Gets or sets who started the run.
    /// </summary>
    public string RequestedBy { get; set; }

    /// <summary>
    /// Gets or sets the judge's score out of 100.
    /// </summary>
    public int? Score { get; set; }

    /// <summary>
    /// Gets or sets the judge's summary.
    /// </summary>
    public string Summary { get; set; }

    /// <summary>
    /// Gets or sets why the run could not finish.
    /// </summary>
    public string Error { get; set; }

    /// <summary>
    /// Gets or sets each criterion and whether it was met.
    /// </summary>
    public List<AIConversationCriterionResult> Criteria { get; set; } = [];

    /// <summary>
    /// Gets or sets the simulated conversation.
    /// </summary>
    public List<AIConversationTurn> Turns { get; set; } = [];

    /// <summary>
    /// Gets or sets the evaluation the judge recorded.
    /// </summary>
    public string EvaluationId { get; set; }
}
