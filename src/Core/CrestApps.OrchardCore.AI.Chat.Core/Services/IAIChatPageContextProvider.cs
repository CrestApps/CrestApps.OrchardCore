namespace CrestApps.OrchardCore.AI.Chat.Core.Services;

/// <summary>
/// Adds what an admin page is about to the context the admin chat widget sends with each message, such as the content
/// item being edited. The model reads it as data, never as instructions.
/// </summary>
/// <remarks>
/// Each module adds the context of the concept it owns. Providers run when the widget renders, once per admin page,
/// so they read only what the page already loaded or what a cached service answers.
/// </remarks>
public interface IAIChatPageContextProvider
{
    /// <summary>
    /// Adds context entries for the page.
    /// </summary>
    /// <param name="context">The page and the entries collected so far.</param>
    /// <returns>A task that completes when the entries are added.</returns>
    Task PopulateAsync(AIChatPageContext context);
}
