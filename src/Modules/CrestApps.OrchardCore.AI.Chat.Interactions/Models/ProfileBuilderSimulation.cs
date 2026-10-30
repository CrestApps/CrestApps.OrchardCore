namespace CrestApps.OrchardCore.AI.Chat.Interactions.Models;

/// <summary>
/// How a builder session runs simulated conversations: one model plays the customer in each scenario, the session's
/// instructions answer, and another model judges the conversation.
/// </summary>
public sealed class ProfileBuilderSimulationSettings
{
    /// <summary>
    /// The most scenarios one round runs.
    /// </summary>
    public const int MaxScenarios = 5;

    /// <summary>
    /// The most rounds one run takes.
    /// </summary>
    public const int MaxRounds = 5;

    /// <summary>
    /// Gets or sets the customers to play, each who they are, what they want and how they behave.
    /// </summary>
    public List<string> Scenarios { get; set; } = [];

    /// <summary>
    /// Gets or sets what every conversation must achieve, one criterion each.
    /// </summary>
    public List<string> Criteria { get; set; } = [];

    /// <summary>
    /// Gets or sets the deployment that plays the customer, or <see langword="null"/> for the utility deployment.
    /// </summary>
    public string CustomerDeploymentName { get; set; }

    /// <summary>
    /// Gets or sets the deployment that judges the conversations, or <see langword="null"/> for the utility
    /// deployment.
    /// </summary>
    public string JudgeDeploymentName { get; set; }

    /// <summary>
    /// Gets or sets the most customer turns in one conversation.
    /// </summary>
    public int MaxTurns { get; set; } = 8;

    /// <summary>
    /// Gets or sets how many rounds a run takes at most before it stops, even when a scenario still fails.
    /// </summary>
    public int Rounds { get; set; } = 3;

    /// <summary>
    /// Gets or sets the score from 0 to 100 a conversation needs to pass.
    /// </summary>
    public int PassingScore { get; set; } = 70;

    /// <summary>
    /// Gets or sets whether the coach rewrites the instructions after a round in which a scenario failed.
    /// </summary>
    public bool Improve { get; set; } = true;
}

/// <summary>
/// One simulated conversation of a builder session, with the judge's verdict.
/// </summary>
public sealed class ProfileBuilderSimulation
{
    /// <summary>
    /// Gets or sets the identifier of the run the conversation belongs to.
    /// </summary>
    public string RunId { get; set; }

    /// <summary>
    /// Gets or sets the round of the run, starting at 1.
    /// </summary>
    public int Round { get; set; }

    /// <summary>
    /// Gets or sets the revision of the instructions that answered.
    /// </summary>
    public int RevisionNumber { get; set; }

    /// <summary>
    /// Gets or sets the customer that was played.
    /// </summary>
    public string Scenario { get; set; }

    /// <summary>
    /// Gets or sets the judge's score from 0 to 100.
    /// </summary>
    public int Score { get; set; }

    /// <summary>
    /// Gets or sets whether the conversation reached the passing score and met every criterion.
    /// </summary>
    public bool Passed { get; set; }

    /// <summary>
    /// Gets or sets the judge's summary.
    /// </summary>
    public string Summary { get; set; }

    /// <summary>
    /// Gets or sets the problems the judge found and the criteria that were not met.
    /// </summary>
    public List<string> Problems { get; set; } = [];

    /// <summary>
    /// Gets or sets the conversation.
    /// </summary>
    public List<ProfileBuilderTurn> Transcript { get; set; } = [];

    /// <summary>
    /// Gets or sets the error, when the conversation could not be run or judged.
    /// </summary>
    public string Error { get; set; }

    /// <summary>
    /// Gets or sets when it ran.
    /// </summary>
    public DateTime CreatedUtc { get; set; }
}
