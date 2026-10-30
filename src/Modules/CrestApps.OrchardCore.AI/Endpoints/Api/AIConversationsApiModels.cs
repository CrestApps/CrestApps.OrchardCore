using CrestApps.OrchardCore.AI.Core.Conversations;

namespace CrestApps.OrchardCore.AI.Endpoints.Api;

/// <summary>
/// A page of results.
/// </summary>
/// <typeparam name="T">The item type.</typeparam>
/// <param name="Items">The items on the page.</param>
/// <param name="Total">How many there are in all.</param>
/// <param name="Skip">How many were skipped.</param>
/// <param name="Take">The page size.</param>
public sealed record ApiAIPage<T>(IReadOnlyList<T> Items, int Total, int Skip, int Take)
{
    /// <summary>
    /// The largest page.
    /// </summary>
    public const int MaxTake = 100;

    internal static int ClampSkip(int? skip)
        => Math.Max(0, skip ?? 0);

    internal static int ClampTake(int? take)
        => Math.Clamp(take ?? 20, 1, MaxTake);
}

/// <summary>
/// An AI conversation, as listed.
/// </summary>
/// <param name="SessionId">The session.</param>
/// <param name="ProfileId">The AI profile.</param>
/// <param name="ProfileVersion">The version of the profile that answered, when versions were kept.</param>
/// <param name="Title">The title.</param>
/// <param name="Status">Active, Closed or Abandoned.</param>
/// <param name="CreatedUtc">When it started.</param>
/// <param name="LastActivityUtc">When something last happened.</param>
/// <param name="ClosedAtUtc">When it was closed.</param>
/// <param name="StorageMode">The storage mode applied once it ended, when one was.</param>
public sealed record ApiAIConversationSummary(
    string SessionId,
    string ProfileId,
    int? ProfileVersion,
    string Title,
    string Status,
    DateTime CreatedUtc,
    DateTime LastActivityUtc,
    DateTime? ClosedAtUtc,
    string StorageMode);

/// <summary>
/// One turn of a transcript.
/// </summary>
/// <param name="Role">Customer or Assistant.</param>
/// <param name="Text">What was said.</param>
/// <param name="CreatedUtc">When.</param>
public sealed record ApiAIConversationTurn(string Role, string Text, DateTime CreatedUtc);

/// <summary>
/// An AI conversation with its transcript, analysis and evaluations.
/// </summary>
/// <param name="Conversation">The conversation.</param>
/// <param name="ContactId">The contact it was with, when known.</param>
/// <param name="Transcript">The transcript.</param>
/// <param name="Variables">The conversation variables.</param>
/// <param name="ExtractedFields">The fields extracted from it.</param>
/// <param name="Results">The post-session analysis results, by task name.</param>
/// <param name="Evaluations">Its evaluations, oldest first.</param>
/// <param name="Flow">Where it stands in its conversation flow and the stages it went through, when it has one.</param>
public sealed record ApiAIConversation(
    ApiAIConversationSummary Conversation,
    string ContactId,
    IReadOnlyList<ApiAIConversationTurn> Transcript,
    IReadOnlyDictionary<string, string> Variables,
    IReadOnlyDictionary<string, IReadOnlyList<string>> ExtractedFields,
    IReadOnlyDictionary<string, string> Results,
    IReadOnlyList<AIConversationEvaluation> Evaluations,
    AIConversationFlowState Flow = null);

/// <summary>
/// Asks for a conversation to be evaluated.
/// </summary>
public sealed class ApiAIEvaluationRequest
{
    /// <summary>
    /// Gets or sets what the conversation is judged on, one criterion each.
    /// </summary>
    public List<string> Criteria { get; set; } = [];

    /// <summary>
    /// Gets or sets the lowest passing score, from 0 to 100. Defaults to 70.
    /// </summary>
    public int? PassingScore { get; set; }

    /// <summary>
    /// Gets or sets a label to tell evaluations apart, such as the name of a test.
    /// </summary>
    public string Label { get; set; }
}

/// <summary>
/// Asks for a test conversation with a customer the utility model plays.
/// </summary>
public sealed class ApiAISimulationRequest
{
    /// <summary>
    /// Gets or sets who the customer is and what they want.
    /// </summary>
    public string Scenario { get; set; }

    /// <summary>
    /// Gets or sets the conversation variables the profile sees.
    /// </summary>
    public Dictionary<string, string> Variables { get; set; } = [];

    /// <summary>
    /// Gets or sets the most customer turns, from 1 to 20. Defaults to 8.
    /// </summary>
    public int? MaxTurns { get; set; }

    /// <summary>
    /// Gets or sets what the conversation is judged on. Without criteria it is not evaluated.
    /// </summary>
    public List<string> Criteria { get; set; } = [];

    /// <summary>
    /// Gets or sets the lowest passing score. Defaults to 70.
    /// </summary>
    public int? PassingScore { get; set; }

    /// <summary>
    /// Gets or sets a label for the evaluation, such as the name of the test.
    /// </summary>
    public string Label { get; set; }

    /// <summary>
    /// Gets or sets the deployment that plays the customer. Empty uses the utility deployment.
    /// </summary>
    public string CustomerDeploymentName { get; set; }

    /// <summary>
    /// Gets or sets the deployment that judges the conversation. Empty uses the utility deployment.
    /// </summary>
    public string JudgeDeploymentName { get; set; }
}

/// <summary>
/// A test conversation.
/// </summary>
/// <param name="Transcript">What was said.</param>
/// <param name="CustomerEnded">Whether the customer ended it, rather than running out of turns.</param>
/// <param name="Evaluation">Its evaluation, when criteria were given.</param>
public sealed record ApiAISimulation(IReadOnlyList<ApiAIConversationTurn> Transcript, bool CustomerEnded, AIConversationEvaluation Evaluation);

/// <summary>
/// A saved version of an AI profile.
/// </summary>
/// <param name="Id">The version record.</param>
/// <param name="Version">The version number.</param>
/// <param name="CreatedUtc">When it was saved.</param>
/// <param name="Author">Who saved it.</param>
/// <param name="Note">What changed, when noted.</param>
public sealed record ApiAIProfileVersion(string Id, int Version, DateTime CreatedUtc, string Author, string Note);

/// <summary>
/// What the AI remembers about a contact.
/// </summary>
/// <param name="ContactId">The contact.</param>
/// <param name="Text">The note, or <see langword="null"/> when there is none.</param>
public sealed record ApiAIContactMemory(string ContactId, string Text);

/// <summary>
/// Replaces what the AI remembers about a contact.
/// </summary>
public sealed class ApiAIContactMemoryRequest
{
    /// <summary>
    /// Gets or sets the note. Empty forgets the contact.
    /// </summary>
    public string Text { get; set; }
}

/// <summary>
/// An AI profile, as listed.
/// </summary>
/// <param name="Id">The profile.</param>
/// <param name="Name">Its unique name.</param>
/// <param name="DisplayText">Its title.</param>
/// <param name="Type">Chat, Utility, TemplatePrompt or Agent.</param>
/// <param name="Version">The current version, when versions are kept.</param>
/// <param name="CreatedUtc">When it was made.</param>
public sealed record ApiAIProfileSummary(string Id, string Name, string DisplayText, string Type, int? Version, DateTime CreatedUtc);
