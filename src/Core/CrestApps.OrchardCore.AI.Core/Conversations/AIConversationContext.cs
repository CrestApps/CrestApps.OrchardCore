using CrestApps.Core.AI.Models;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.AI.Core.Conversations;

/// <summary>
/// Describes the conversation an AI request belongs to: the channel, the people on each end, the session and its
/// variables, and the records the conversation is about.
/// </summary>
/// <remarks>
/// One context is opened for each turn (or for the whole call on a live voice session) with
/// <see cref="AIConversationScope.Begin(AIConversationContext)"/>, so the system message, tool parameters and tool
/// workflows read the same values whichever channel the conversation runs on.
/// </remarks>
public sealed class AIConversationContext
{
    /// <summary>
    /// Gets or sets the channel the conversation runs on, one of <see cref="AIConversationChannels"/>.
    /// </summary>
    public string Channel { get; set; }

    /// <summary>
    /// Gets or sets whether the customer reached out or the conversation was started for them, one of
    /// <see cref="AIConversationDirections"/>.
    /// </summary>
    public string Direction { get; set; }

    /// <summary>
    /// Gets or sets the AI session the conversation is stored in.
    /// </summary>
    public AIChatSession Session { get; set; }

    /// <summary>
    /// Gets or sets the AI profile answering the conversation.
    /// </summary>
    public AIProfile Profile { get; set; }

    /// <summary>
    /// Gets or sets the omnichannel activity the conversation belongs to, when it has one.
    /// </summary>
    public string ActivityId { get; set; }

    /// <summary>
    /// Gets or sets the contact the conversation is with, when it is known.
    /// </summary>
    public ContentItem Contact { get; set; }

    /// <summary>
    /// Gets or sets the customer's address: the phone number or email address the conversation is with.
    /// </summary>
    public string CustomerAddress { get; set; }

    /// <summary>
    /// Gets or sets our address: the phone number or email address the conversation uses on our side.
    /// </summary>
    public string BusinessAddress { get; set; }

    /// <summary>
    /// Gets or sets whether the conversation is a simulated test, where the customer is played by a model. Tool
    /// workflows read it as <c>Workflow.Input.Simulation</c> to answer with test data instead of acting.
    /// </summary>
    public bool IsSimulation { get; set; }

    /// <summary>
    /// Gets the other values a channel adds for templates and workflows, such as the activity, its campaign and
    /// its subject. Keys are the names Liquid templates use.
    /// </summary>
    public Dictionary<string, object> Values { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the variables the conversation started with, before its session existed: the ones its activity carries
    /// and the ones a starting workflow set.
    /// </summary>
    public Dictionary<string, string> InitialVariables { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the conversation variables: <see cref="InitialVariables"/> overlaid with the ones stored on
    /// <see cref="Session"/>, which are newer.
    /// </summary>
    /// <returns>The variables by name.</returns>
    public IReadOnlyDictionary<string, string> GetVariables()
    {
        var variables = new Dictionary<string, string>(InitialVariables, StringComparer.OrdinalIgnoreCase);

        if (Session is not null)
        {
            foreach (var (name, value) in Session.GetConversationVariables())
            {
                variables[name] = value;
            }
        }

        return variables;
    }

    /// <summary>
    /// Sets conversation variables: on <see cref="Session"/> when the conversation has one, and in
    /// <see cref="InitialVariables"/> either way. The caller saves the session.
    /// </summary>
    /// <param name="values">The variables to set; a <see langword="null"/> value removes one.</param>
    /// <returns><see langword="true"/> when the session's variables changed.</returns>
    public bool SetVariables(IEnumerable<KeyValuePair<string, string>> values)
    {
        if (values is null)
        {
            return false;
        }

        var list = values.ToList();

        foreach (var (name, value) in list)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            if (value is null)
            {
                InitialVariables.Remove(name.Trim());
            }
            else
            {
                InitialVariables[name.Trim()] = value;
            }
        }

        return Session is not null && Session.SetConversationVariables(list);
    }
}
