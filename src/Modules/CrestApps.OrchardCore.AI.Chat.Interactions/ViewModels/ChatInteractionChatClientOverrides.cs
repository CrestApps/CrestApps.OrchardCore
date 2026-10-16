namespace CrestApps.OrchardCore.AI.Chat.Interactions.ViewModels;

/// <summary>
/// Changes to the chat interaction client a page asks for, set as a request feature before the editor renders.
/// </summary>
/// <remarks>
/// The chat shape is built by the chat interaction display driver, so a page that reuses the editor, such as the
/// AI Profile Builder, has no other way to reach the client configuration that shape writes.
/// </remarks>
public sealed class ChatInteractionChatClientOverrides
{
    /// <summary>
    /// Gets or sets the Vue template the client renders messages with, replacing its built-in one.
    /// </summary>
    public string MessageTemplate { get; set; }

    /// <summary>
    /// Gets or sets the heading shown while the chat is empty.
    /// </summary>
    public string PlaceholderHeading { get; set; }

    /// <summary>
    /// Gets or sets the placeholder text of the message box.
    /// </summary>
    public string PromptPlaceholder { get; set; }
}
