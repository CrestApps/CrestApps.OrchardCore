namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// A declarative menu tree attached to an entry point. An entry point could map a number to exactly one queue,
/// so every caller to a shared line reached the same team and was transferred by hand.
/// <para>
/// A flow with no nodes is the "no IVR" case and behaves exactly as an entry point did before this existed, so
/// nothing changes for a tenant that has not built a menu.
/// </para>
/// </summary>
public sealed class IvrFlow
{
    /// <summary>
    /// Gets or sets the node the caller hears first.
    /// </summary>
    public string RootNodeId { get; set; }

    /// <summary>
    /// Gets or sets how many times a caller may fail to choose before the fallback takes over. A caller who
    /// cannot work the menu — a rotary phone, a bad line, a language they do not read — must still reach a
    /// person rather than being re-prompted until they give up.
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Gets or sets what happens when the retries run out.
    /// </summary>
    public IvrAction FallbackAction { get; set; }

    /// <summary>
    /// Gets or sets the menus.
    /// </summary>
    public IList<IvrNode> Nodes { get; set; } = [];
}

/// <summary>
/// One menu: what the caller hears, and what each key does.
/// </summary>
public sealed class IvrNode
{
    /// <summary>
    /// Gets or sets the identifier other nodes and the flow refer to.
    /// </summary>
    public string NodeId { get; set; }

    /// <summary>
    /// Gets or sets the spoken prompt.
    /// </summary>
    public string Prompt { get; set; }

    /// <summary>
    /// Gets or sets the media played instead of speaking the prompt, when one is configured.
    /// </summary>
    public string PromptMediaId { get; set; }

    /// <summary>
    /// Gets or sets the keys this menu accepts.
    /// </summary>
    public IList<IvrOption> Options { get; set; } = [];
}

/// <summary>
/// One key on a menu.
/// </summary>
public sealed class IvrOption
{
    /// <summary>
    /// Gets or sets the digit the caller presses.
    /// </summary>
    public string Digit { get; set; }

    /// <summary>
    /// Gets or sets what pressing it does.
    /// </summary>
    public IvrAction Action { get; set; }
}

/// <summary>
/// What a menu choice does.
/// </summary>
public sealed class IvrAction
{
    /// <summary>
    /// Gets or sets the kind of action.
    /// </summary>
    public IvrActionKind Kind { get; set; }

    /// <summary>
    /// Gets or sets what the action acts on: a queue id, an agent id, a child node id, or an approved external
    /// catalog entry.
    /// </summary>
    public string TargetId { get; set; }
}

/// <summary>
/// The kinds of thing a menu choice can do.
/// </summary>
public enum IvrActionKind
{
    /// <summary>
    /// Send the caller to a queue.
    /// </summary>
    RouteToQueue,

    /// <summary>
    /// Send the caller to a specific agent.
    /// </summary>
    RouteToAgent,

    /// <summary>
    /// Send the caller to voicemail.
    /// </summary>
    Voicemail,

    /// <summary>
    /// Transfer the caller to an approved external destination.
    /// </summary>
    ExternalTransfer,

    /// <summary>
    /// Play the current menu again.
    /// </summary>
    Repeat,

    /// <summary>
    /// Move the caller into a child menu.
    /// </summary>
    SubMenu,
}
