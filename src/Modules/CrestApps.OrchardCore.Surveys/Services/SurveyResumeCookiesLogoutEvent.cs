using CrestApps.OrchardCore.Surveys.Core.Runtime;
using OrchardCore.Users;
using OrchardCore.Users.Events;

namespace CrestApps.OrchardCore.Surveys.Services;

/// <summary>
/// Forgets the browser's survey resume cookies when a user signs out, so the next person on a shared computer never
/// resumes the signed-out person's response.
/// </summary>
public sealed class SurveyResumeCookiesLogoutEvent : LogoutFormEventBase
{
    private readonly SurveyResumeCookies _resumeCookies;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyResumeCookiesLogoutEvent"/> class.
    /// </summary>
    /// <param name="resumeCookies">The resume cookies.</param>
    public SurveyResumeCookiesLogoutEvent(SurveyResumeCookies resumeCookies)
    {
        _resumeCookies = resumeCookies;
    }

    /// <inheritdoc />
    public override Task LoggedOutAsync(IUser user, CancellationToken cancellationToken)
    {
        _resumeCookies.ClearAll();

        return Task.CompletedTask;
    }
}
