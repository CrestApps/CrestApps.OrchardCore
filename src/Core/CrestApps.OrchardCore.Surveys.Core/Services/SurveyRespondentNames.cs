using System.Security.Claims;
using CrestApps.OrchardCore.Surveys.Core.Access;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using CrestApps.OrchardCore.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.Surveys.Core.Services;

/// <summary>
/// The one place that decides who may see who responded, and how a respondent is named, for the admin pages, grading,
/// reports, the API and the data sets.
/// <para>
/// The rule: who responded is shown only on an identified survey, and only to someone who may read that survey's
/// responses or grade them. An anonymous survey never shows it, whatever the viewer holds.
/// </para>
/// <para>
/// The names: a user's display name (or user name); "Guest" for an identified response given without signing in;
/// "Deleted user" for a user who no longer exists; "Anonymous" on an anonymous survey. Users are looked up once per
/// scope, and the user services are resolved on first use.
/// </para>
/// </summary>
public sealed class SurveyRespondentNames
{
    private readonly IServiceProvider _serviceProvider;
    private readonly Dictionary<string, string> _names = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Survey> _surveys = new(StringComparer.Ordinal);

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyRespondentNames"/> class.
    /// </summary>
    /// <param name="serviceProvider">The services, for the optional user, display name and survey authorization services.</param>
    /// <param name="stringLocalizer">The localizer.</param>
    public SurveyRespondentNames(
        IServiceProvider serviceProvider,
        IStringLocalizer<SurveyRespondentNames> stringLocalizer)
    {
        _serviceProvider = serviceProvider;
        S = stringLocalizer;
    }

    /// <summary>
    /// Decides whether a person may see who responded to a survey: never on an anonymous survey; otherwise when they may
    /// read its responses or grade them.
    /// </summary>
    /// <param name="user">The person.</param>
    /// <param name="survey">The survey.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when they may.</returns>
    public async Task<bool> CanSeeIdentityAsync(ClaimsPrincipal user, Survey survey, CancellationToken cancellationToken = default)
    {
        if (user is null || survey is null || SurveyAnonymity.IsAnonymous(survey))
        {
            return false;
        }

        if (_serviceProvider.GetService<ISurveyAuthorizationService>() is { } surveys)
        {
            return await surveys.AuthorizeAsync(user, survey, SurveyOperation.ViewResponses, cancellationToken)
                || await surveys.AuthorizeAsync(user, survey, SurveyOperation.Grade, cancellationToken);
        }

        if (_serviceProvider.GetService<IAuthorizationService>() is not { } authorization)
        {
            return false;
        }

        if (await authorization.AuthorizeAsync(user, SurveysPermissions.ViewSurveyResponses)
            || await authorization.AuthorizeAsync(user, SurveysPermissions.GradeSurveyResponses))
        {
            return true;
        }

        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);

        return !string.IsNullOrEmpty(userId)
            && string.Equals(survey.OwnerId, userId, StringComparison.Ordinal)
            && await authorization.AuthorizeAsync(user, SurveysPermissions.ViewOwnSurveyResponses);
    }

    /// <summary>
    /// Names the respondent of a response for someone allowed to see who responded; the survey is read from the store.
    /// </summary>
    /// <param name="response">The response.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The name.</returns>
    public async Task<string> GetAsync(SurveyResponse response, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(response);

        return await GetAsync(response, await SurveyOfAsync(response.SurveyId, cancellationToken), cancellationToken);
    }

    /// <summary>
    /// Names the respondent of a response of a survey for someone allowed to see who responded.
    /// </summary>
    /// <param name="response">The response.</param>
    /// <param name="survey">Its survey, or <see langword="null"/> when unknown (then nobody is named).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The name.</returns>
    public Task<string> GetAsync(SurveyResponse response, Survey survey, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(response);

        if (survey is null || SurveyAnonymity.IsAnonymous(survey))
        {
            return Task.FromResult(S["Anonymous"].Value);
        }

        return GetUserAsync(response.UserId, cancellationToken);
    }

    /// <summary>
    /// Names a user who responded to an identified survey.
    /// </summary>
    /// <param name="userId">The user id, or <see langword="null"/> for a guest.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The display name or user name, "Guest" without a user, or "Deleted user".</returns>
    public async Task<string> GetUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return S["Guest"].Value;
        }

        if (_names.TryGetValue(userId, out var cached))
        {
            return cached;
        }

        var user = _serviceProvider.GetService<UserManager<IUser>>() is { } userManager ? await userManager.FindByIdAsync(userId) : null;
        string name;

        if (user is null)
        {
            name = S["Deleted user"].Value;
        }
        else
        {
            var displayName = _serviceProvider.GetService<IDisplayNameProvider>() is { } displayNames ? await displayNames.GetAsync(user, cancellationToken) : null;

            name = string.IsNullOrWhiteSpace(displayName) ? user.UserName : displayName;
        }

        _names[userId] = name;

        return name;
    }

    /// <summary>
    /// Gets whether the survey of a response is anonymous (or unknown), so a view shows its times by the day only.
    /// </summary>
    /// <param name="response">The response.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    public async Task<bool> IsAnonymousAsync(SurveyResponse response, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(response);

        var survey = await SurveyOfAsync(response.SurveyId, cancellationToken);

        return survey is null || SurveyAnonymity.IsAnonymous(survey);
    }

    private async Task<Survey> SurveyOfAsync(string surveyId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(surveyId))
        {
            return null;
        }

        if (!_surveys.TryGetValue(surveyId, out var survey))
        {
            _surveys[surveyId] = survey = _serviceProvider.GetService<SurveyStore>() is { } store ? await store.FindByIdAsync(surveyId, cancellationToken) : null;
        }

        return survey;
    }
}
