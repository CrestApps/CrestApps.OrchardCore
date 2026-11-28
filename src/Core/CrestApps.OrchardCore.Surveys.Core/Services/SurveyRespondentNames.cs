using System.Security.Claims;
using CrestApps.OrchardCore.Surveys.Core.Access;
using CrestApps.OrchardCore.Surveys.Core.Indexes;
using YesSql;
using ISession = YesSql.ISession;
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
    private readonly Dictionary<string, string> _secrets = new(StringComparer.Ordinal);

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

    /// <summary>
    /// Gets the label staff see for a response (see <see cref="SurveyAnonymity.ResponseLabel"/>): its number, or on an
    /// anonymous (or unknown) survey a keyed hash of its id under the survey's anonymity secret.
    /// </summary>
    /// <param name="response">The response.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The label.</returns>
    public async Task<string> GetLabelAsync(SurveyResponse response, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(response);

        var survey = await SurveyOfAsync(response.SurveyId, cancellationToken);

        return SurveyAnonymity.ResponseLabel(survey, response, await SecretOfAsync(survey, cancellationToken));
    }

    /// <summary>
    /// Gets the label staff see for a response of a survey already loaded, which may be a summary without its secret.
    /// </summary>
    /// <param name="survey">The survey, or <see langword="null"/> to look it up.</param>
    /// <param name="response">The response.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The label.</returns>
    public async Task<string> GetLabelAsync(Survey survey, SurveyResponse response, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(response);

        if (survey is not null && !SurveyAnonymity.IsAnonymous(survey))
        {
            return SurveyAnonymity.ResponseLabel(survey, response, null);
        }

        return await GetLabelAsync(response, cancellationToken);
    }

    /// <summary>
    /// Finds the responses of an anonymous survey whose label is a text, ignoring case and surrounding spaces: the
    /// label is a keyed hash, so the survey's response ids are read from the index and hashed, a page at a time.
    /// </summary>
    /// <param name="survey">The survey.</param>
    /// <param name="label">The label searched for.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The ids of the matching responses; usually one, or none.</returns>
    public async Task<IReadOnlyList<string>> FindByLabelAsync(Survey survey, string label, CancellationToken cancellationToken = default)
    {
        var search = label?.Trim().ToLowerInvariant();

        if (survey is null || string.IsNullOrEmpty(search) || search.Length != SurveyAnonymity.AnonymousLabelLength
            || _serviceProvider.GetService<ISession>() is not { } session)
        {
            return [];
        }

        var secret = await SecretOfAsync(await SurveyOfAsync(survey.ItemId, cancellationToken) ?? survey, cancellationToken);
        var surveyId = survey.ItemId;
        var matches = new List<string>();
        var lastId = 0L;

        while (true)
        {
            var rows = (await session.QueryIndex<SurveyResponseIndex>(index => index.SurveyId == surveyId && index.Id > lastId, collection: SurveysConstants.ResponsesCollectionName)
                .OrderBy(index => index.Id)
                .Take(LabelSearchPageSize)
                .ListAsync(cancellationToken)).ToList();

            matches.AddRange(rows
                .Where(row => string.Equals(SurveyAnonymity.AnonymousLabel(secret, row.ItemId), search, StringComparison.Ordinal))
                .Select(row => row.ItemId));

            if (rows.Count < LabelSearchPageSize)
            {
                return matches;
            }

            lastId = rows[^1].Id;
        }
    }

    // How many index rows a label search reads at a time.
    private const int LabelSearchPageSize = 2000;

    // The anonymity secret of an anonymous survey in plain text, read once per scope; empty when it can't be read on
    // this node (the labels are then empty rather than guessable).
    private async Task<string> SecretOfAsync(Survey survey, CancellationToken cancellationToken)
    {
        if (survey is null || !SurveyAnonymity.IsAnonymous(survey) || _serviceProvider.GetService<SurveyAnonymitySecrets>() is not { } secrets)
        {
            return null;
        }

        if (!_secrets.TryGetValue(survey.ItemId ?? string.Empty, out var secret))
        {
            try
            {
                secret = await secrets.RevealAsync(survey, cancellationToken);
            }
            catch (SurveyAnonymitySecretUnreadableException)
            {
                secret = null;
            }

            _secrets[survey.ItemId ?? string.Empty] = secret;
        }

        return secret;
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
