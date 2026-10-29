using CrestApps.Core;
using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.AI.Core.Models;

/// <summary>
/// A simulation test of an AI profile: a customer the AI talks to, what the conversation must achieve, and the
/// values and tool results it runs with.
/// </summary>
public sealed class AITestCase : CatalogItem, INameAwareModel
{
    /// <summary>
    /// Gets or sets the test's name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the AI profile under test.
    /// </summary>
    public string ProfileId { get; set; }

    /// <summary>
    /// Gets or sets the version of the profile under test, or <see langword="null"/> for its current version.
    /// </summary>
    public int? ProfileVersion { get; set; }

    /// <summary>
    /// Gets or sets the simulated customer: who they are, what they want and how they behave.
    /// </summary>
    public string Scenario { get; set; }

    /// <summary>
    /// Gets or sets what the conversation must achieve, one criterion per line.
    /// </summary>
    public string Criteria { get; set; }

    /// <summary>
    /// Gets or sets the conversation variables the test runs with.
    /// </summary>
    public Dictionary<string, string> Variables { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets or sets the result each named tool returns instead of running.
    /// </summary>
    public Dictionary<string, string> MockedTools { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets or sets the most customer turns the conversation runs.
    /// </summary>
    public int MaxTurns { get; set; } = 8;

    /// <summary>
    /// Gets or sets the score out of 100 a run needs to pass.
    /// </summary>
    public int PassingScore { get; set; } = 70;

    /// <summary>
    /// Gets or sets the deployment that plays the customer, or <see langword="null"/> for the utility default.
    /// </summary>
    public string CustomerDeploymentName { get; set; }

    /// <summary>
    /// Gets or sets the deployment that judges the run, or <see langword="null"/> for the utility default.
    /// </summary>
    public string JudgeDeploymentName { get; set; }

    /// <summary>
    /// Gets or sets the conversation the test was made from, if any.
    /// </summary>
    public string SourceSessionId { get; set; }

    /// <summary>
    /// Gets or sets when the test was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets when the test was last changed.
    /// </summary>
    public DateTime? ModifiedUtc { get; set; }

    /// <summary>
    /// Gets or sets the outcome of the test's latest run.
    /// </summary>
    public AITestRunStatus? LastStatus { get; set; }

    /// <summary>
    /// Gets or sets when the test last ran.
    /// </summary>
    public DateTime? LastRunUtc { get; set; }
}
