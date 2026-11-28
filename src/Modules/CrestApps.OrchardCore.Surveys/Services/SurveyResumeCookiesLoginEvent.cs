using CrestApps.OrchardCore.Surveys.Core.Runtime;
using OrchardCore.Users;
using OrchardCore.Users.Events;

namespace CrestApps.OrchardCore.Surveys.Services;

/// <summary>
/// Forgets the browser's survey resume cookies when a user signs in, so a person who signs in on a shared computer
/// never resumes the response of whoever used it before.
/// </summary>
public sealed class SurveyResumeCookiesLoginEvent : LoginFormEventBase
{
    private readonly SurveyResumeCookies _resumeCookies;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyResumeCookiesLoginEvent"/> class.
    /// </summary>
    /// <param name="resumeCookies">The resume cookies.</param>
    public SurveyResumeCookiesLoginEvent(SurveyResumeCookies resumeCookies)
    {
        _resumeCookies = resumeCookies;
    }

    /// <inheritdoc />
    public override Task LoggedInAsync(IUser user)
    {
        _resumeCookies.ClearAll();

        return Task.CompletedTask;
    }
}
