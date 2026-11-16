using CrestApps.OrchardCore.Users;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Staff.Services;

/// <summary>
/// Whether a pay card on a staff profile shows, with the viewer's password confirmed (<see cref="IPasswordConfirmation"/>):
/// pay is private, so someone at a computer left signed in doesn't see it. Each module's pay card asks it; the first that
/// can't show renders the one confirmation prompt (<see cref="PromptShape"/>) and the others hide.
/// </summary>
public static class StaffPayPrivacy
{
    /// <summary>
    /// The shape of the prompt, with a <c>ConfirmUrl</c> property.
    /// </summary>
    public const string PromptShape = "StaffPayConfirmation";

    private const string PromptedKey = "CrestApps.Staff.PayConfirmationPrompted";

    /// <summary>
    /// Decides what a pay card does for the current request.
    /// </summary>
    /// <param name="httpContext">The request.</param>
    /// <returns>Show the card, render the prompt in its place, or hide it.</returns>
    public static async Task<StaffPayVisibility> DecideAsync(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        if (httpContext.RequestServices.GetService<IPasswordConfirmation>() is { } confirmation
            && await confirmation.IsConfirmedAsync(httpContext.User, httpContext.RequestAborted))
        {
            return StaffPayVisibility.Show;
        }

        if (httpContext.Items.ContainsKey(PromptedKey))
        {
            return StaffPayVisibility.Hide;
        }

        httpContext.Items[PromptedKey] = true;

        return StaffPayVisibility.Prompt;
    }

    /// <summary>
    /// Returns the confirmation page that comes back to the current page.
    /// </summary>
    /// <param name="httpContext">The request.</param>
    /// <returns>The page, or <see langword="null"/> without the confirmation service.</returns>
    public static string ConfirmUrl(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        return httpContext.RequestServices.GetService<IPasswordConfirmation>()?.GetConfirmationUrl(httpContext.Request.PathBase + httpContext.Request.Path + httpContext.Request.QueryString);
    }
}

/// <summary>
/// What a pay card on a staff profile does.
/// </summary>
public enum StaffPayVisibility
{
    /// <summary>
    /// The viewer confirmed their password: the card shows.
    /// </summary>
    Show,

    /// <summary>
    /// The card shows the confirmation prompt in its place.
    /// </summary>
    Prompt,

    /// <summary>
    /// Another card shows the prompt: this one shows nothing.
    /// </summary>
    Hide,
}
