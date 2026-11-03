using CrestApps.Core;
using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Models;

/// <summary>
/// The working state of one AI Profile Builder session: the feedback the author gave while role-playing, every
/// revision of the instructions that feedback produced, and the last regression replay.
/// </summary>
/// <remarks>
/// The conversation itself is an ordinary <see cref="CrestApps.Core.AI.Models.ChatInteraction"/>, so role-play gets
/// streaming, tools, data sources and both voice modes from the chat interaction hub unchanged. This record is kept
/// apart from the interaction on purpose: the settings panel saves the whole interaction through the hub, and a
/// ledger stored on the interaction would be lost whenever an autosave raced a feedback request.
/// </remarks>
public sealed class ProfileBuilderSession : CatalogItem, IModifiedUtcAwareModel
{
    /// <summary>
    /// Gets or sets the identifier of the chat interaction that carries the role-play conversation.
    /// </summary>
    public string ChatInteractionId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the user who owns the session.
    /// </summary>
    public string OwnerId { get; set; }

    /// <summary>
    /// Gets or sets the name of the user who owns the session.
    /// </summary>
    public string Author { get; set; }

    /// <summary>
    /// Gets or sets the instruction the assistant is given to open a conversation it starts itself, such as an
    /// automated SMS or call. It becomes the profile's initial prompt.
    /// </summary>
    public string InitialPrompt { get; set; }

    /// <summary>
    /// Gets or sets the deployment that reads feedback and rewrites the instructions. When empty, the interaction's
    /// chat deployment does it, because rewriting instructions well needs a capable model.
    /// </summary>
    public string CoachDeploymentName { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the AI profile this session was started from or last saved to.
    /// </summary>
    public string ProfileId { get; set; }

    /// <summary>
    /// Gets or sets every piece of feedback given in this session, oldest first. Entries are never removed: a reply
    /// that was regenerated away stays here, so the fix it led to is never forgotten by a later rewrite.
    /// </summary>
    public List<ProfileBuilderFeedback> Feedback { get; set; } = [];

    /// <summary>
    /// Gets or sets every revision of the instructions, oldest first. The last one is the current revision.
    /// </summary>
    public List<ProfileBuilderRevision> Revisions { get; set; } = [];

    /// <summary>
    /// Gets or sets the result of the last regression replay, or <see langword="null"/> when none has run.
    /// </summary>
    public ProfileBuilderReplay LastReplay { get; set; }

    /// <summary>
    /// Gets or sets how the session runs simulated conversations.
    /// </summary>
    public ProfileBuilderSimulationSettings SimulationSettings { get; set; } = new();

    /// <summary>
    /// Gets or sets the simulated conversations, oldest first. Only the most recent are kept.
    /// </summary>
    public List<ProfileBuilderSimulation> Simulations { get; set; } = [];

    /// <summary>
    /// Gets or sets when the session was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets when the session was last changed.
    /// </summary>
    public DateTime? ModifiedUtc { get; set; }
}
