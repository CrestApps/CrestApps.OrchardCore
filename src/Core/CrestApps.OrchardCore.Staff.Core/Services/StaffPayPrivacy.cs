using System.Security.Claims;
using CrestApps.OrchardCore.Staff.Models;
using CrestApps.OrchardCore.Users;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Staff.Services;

/// <summary>
/// Whether a pay card on a staff profile shows, with the viewer's password confirmed (<see cref="IPasswordConfirmation"/>):
/// pay is private, so someone at a computer left signed in doesn't see it. Each module's pay card asks it; the first that
/// can't show renders the one confirmation prompt (<see cref="PromptShape"/>) and the others hide. A person's own pay asks
/// every time their profile is opened; someone else's, such as payroll's view of an employee, asks once a few minutes.
/// </summary>
public static class StaffPayPrivacy
{
    /// <summary>
    /// The shape of the prompt, with a <c>ConfirmUrl</c> property.
    /// </summary>
    public const string PromptShape = "StaffPayConfirmation";

    /// <summary>
    /// The address hash of the profile's Pay section, so the confirmation comes back to it.
    /// </summary>
    public const string PaySectionHash = "staff-pay";

    private const string PromptedKey = "CrestApps.Staff.PayConfirmationPrompted";

    /// <summary>
    /// Decides what a pay card does for the current request, with the confirmation of the last few minutes.
    /// </summary>
    /// <param name="httpContext">The request.</param>
    /// <returns>Show the card, render the prompt in its place, or hide it.</returns>
    public static Task<StaffPayVisibility> DecideAsync(HttpContext httpContext)
        => DecideAsync(httpContext, staffMember: null);

    /// <summary>
    /// Decides what a pay card of a staff member's profile does for the current request. On the viewer's own profile the
    /// card shows only right after they typed their password for this page, so their pay asks every time it is opened.
    /// </summary>
    /// <param name="httpContext">The request.</param>
    /// <param name="staffMember">The staff member whose profile shows the card.</param>
    /// <returns>Show the card, render the prompt in its place, or hide it.</returns>
    public static async Task<StaffPayVisibility> DecideAsync(HttpContext httpContext, StaffMember staffMember)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        if (httpContext.RequestServices.GetService<IPasswordConfirmation>() is { } confirmation
            && await (IsOwnProfile(httpContext.User, staffMember)
                ? confirmation.IsJustConfirmedAsync(httpContext.User, httpContext.RequestAborted)
                : confirmation.IsConfirmedAsync(httpContext.User, httpContext.RequestAborted)))
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
    /// Returns the confirmation page that comes back to the current page, at its Pay section.
    /// </summary>
    /// <param name="httpContext">The request.</param>
    /// <returns>The page, or <see langword="null"/> without the confirmation service.</returns>
    public static string ConfirmUrl(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        return httpContext.RequestServices.GetService<IPasswordConfirmation>()?.GetConfirmationUrl(
            httpContext.Request.PathBase + httpContext.Request.Path + httpContext.Request.QueryString + "#" + PaySectionHash);
    }

    private static bool IsOwnProfile(ClaimsPrincipal user, StaffMember staffMember)
        => !string.IsNullOrEmpty(staffMember?.UserId)
            && string.Equals(staffMember.UserId, user?.FindFirstValue(ClaimTypes.NameIdentifier), StringComparison.Ordinal);
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
