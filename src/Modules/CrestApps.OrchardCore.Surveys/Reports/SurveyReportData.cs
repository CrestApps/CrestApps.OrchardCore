using System.Globalization;
using System.Security.Claims;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using CrestApps.OrchardCore.Surveys.Core.Access;
using CrestApps.OrchardCore.Surveys.Core.Results;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Surveys.Reports;

/// <summary>
/// The shared work of the survey reports: which surveys a person may read, the scope a filter selects, and local times.
/// </summary>
internal sealed class SurveyReportData
{
    private readonly SurveyStore _surveys;
    private readonly SurveyVersionStore _versions;
    private readonly IAuthorizationService _authorizationService;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILocalClock _localClock;
    private readonly SurveyRespondentNames _names;
    private readonly SurveyAccessScopes _scopes;
    private readonly SurveyIndexReader _reader;
    private readonly SurveyAnonymousDays _days;

    internal readonly IStringLocalizer S;

    public SurveyReportData(
        SurveyStore surveys,
        SurveyVersionStore versions,
        IAuthorizationService authorizationService,
        IServiceProvider serviceProvider,
        ILocalClock localClock,
        SurveyRespondentNames names,
        IStringLocalizer<SurveyReportData> stringLocalizer,
        SurveyAccessScopes scopes,
        SurveyIndexReader reader,
        SurveyAnonymousDays days = null)
    {
        _days = days ?? new SurveyAnonymousDays(serviceProvider);
        _surveys = surveys;
        _versions = versions;
        _authorizationService = authorizationService;
        _serviceProvider = serviceProvider;
        _localClock = localClock;
        _names = names;
        _scopes = scopes;
        _reader = reader;
        S = stringLocalizer;
    }

    /// <summary>
    /// Lists the published surveys whose responses a person may read, by title. The surveys are summaries read from the
    /// survey index (<see cref="SurveyIndexReader"/>), and who may read which is worked out at once
    /// (<see cref="SurveyAccessScopes"/>), so no survey document is loaded.
    /// </summary>
    /// <param name="user">The person.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The survey summaries.</returns>
    public async Task<IReadOnlyList<Survey>> ListReadableAsync(ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        if (user is null)
        {
            return [];
        }

        var scope = await _scopes.ResolveAsync(user, SurveyOperation.ViewResponses, cancellationToken);

        if (scope.IsEmpty)
        {
            return [];
        }

        return (await _reader.ListAsync(scope.All ? null : scope.SurveyIds, publishedOnly: true, cancellationToken))
            .OrderBy(survey => survey.Title, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Gets whether a person may read the responses of every survey, so a statement over them needs no survey filter.
    /// </summary>
    /// <param name="user">The person.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when they may.</returns>
    public async Task<bool> CanReadEverySurveyAsync(ClaimsPrincipal user, CancellationToken cancellationToken)
        => user is not null && (await _scopes.ResolveAsync(user, SurveyOperation.ViewResponses, cancellationToken)).All;

    /// <summary>
    /// Decides whether a person may read the responses of a survey: the survey authorization service when there is one,
    /// otherwise View survey responses, or View own survey responses for the surveys they own.
    /// </summary>
    /// <param name="user">The person.</param>
    /// <param name="survey">The survey.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when allowed.</returns>
    public async Task<bool> CanViewResponsesAsync(ClaimsPrincipal user, Survey survey, CancellationToken cancellationToken)
    {
        if (user is null || survey is null)
        {
            return false;
        }

        if (_serviceProvider.GetService<ISurveyAuthorizationService>() is { } authorization)
        {
            return await authorization.AuthorizeAsync(user, survey, SurveyOperation.ViewResponses, cancellationToken);
        }

        if (await _authorizationService.AuthorizeAsync(user, SurveysPermissions.ViewSurveyResponses))
        {
            return true;
        }

        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);

        return !string.IsNullOrEmpty(userId)
            && string.Equals(survey.OwnerId, userId, StringComparison.Ordinal)
            && await _authorizationService.AuthorizeAsync(user, SurveysPermissions.ViewOwnSurveyResponses);
    }

    /// <summary>
    /// Decides whether a person may list the members of a user audience against a survey: one of the survey's own
    /// audiences (who may respond), or any audience for a person with Manage user audiences, who may read every member
    /// list on the audiences page anyway.
    /// </summary>
    /// <param name="user">The person.</param>
    /// <param name="survey">The survey.</param>
    /// <param name="audienceName">The audience's name.</param>
    /// <returns><see langword="true"/> when allowed.</returns>
    public async Task<bool> CanListAudienceAsync(ClaimsPrincipal user, Survey survey, string audienceName)
    {
        if (user is null || survey is null || string.IsNullOrEmpty(audienceName))
        {
            return false;
        }

        return OwnAudiences(survey).Contains(audienceName)
            || await CanListEveryAudienceAsync(user);
    }

    /// <summary>
    /// Gets which user audiences a person may pick against a survey, by the rule of <see cref="CanListAudienceAsync"/>.
    /// </summary>
    /// <param name="user">The person.</param>
    /// <param name="surveyId">The survey's id, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A test of an audience's name.</returns>
    public async Task<Func<string, bool>> ListableAudiencesAsync(ClaimsPrincipal user, string surveyId, CancellationToken cancellationToken)
    {
        if (await CanListEveryAudienceAsync(user))
        {
            return _ => true;
        }

        var survey = string.IsNullOrEmpty(surveyId) ? null : await _surveys.FindByIdAsync(surveyId, cancellationToken);

        if (survey is null || !await CanViewResponsesAsync(user, survey, cancellationToken))
        {
            return _ => false;
        }

        return OwnAudiences(survey).Contains;
    }

    // The audiences the survey is for: only while Who may respond is a list of audiences. The list is kept when the mode
    // changes, and a survey now open to everyone must not still open the members of an audience it used to name.
    private static HashSet<string> OwnAudiences(Survey survey)
        => survey.Access is { Audience: SurveyAudienceMode.Audiences, AudienceIds: { } ids }
            ? new HashSet<string>(ids, StringComparer.Ordinal)
            : [];

    /// <summary>
    /// Gets whether a person may list the members of every user audience: Manage user audiences.
    /// </summary>
    /// <param name="user">The person.</param>
    /// <returns><see langword="true"/> when allowed.</returns>
    public async Task<bool> CanListEveryAudienceAsync(ClaimsPrincipal user)
        => user is not null && await _authorizationService.AuthorizeAsync(user, CrestApps.OrchardCore.Users.Core.UserPermissions.ManageUserAudiences);

    /// <summary>
    /// Decides whether a person may see who responded to a survey, by the one rule of
    /// <see cref="SurveyRespondentNames.CanSeeIdentityAsync"/>.
    /// </summary>
    /// <param name="user">The person.</param>
    /// <param name="survey">The survey.</param>
    /// <returns><see langword="true"/> when allowed.</returns>
    public Task<bool> CanSeeIdentityAsync(ClaimsPrincipal user, Survey survey)
        => _names.CanSeeIdentityAsync(user, survey);

    /// <summary>
    /// Works out the scope a report's filter selects, or <see langword="null"/> when no readable survey is selected.
    /// </summary>
    /// <param name="context">The report context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The scope, or <see langword="null"/>.</returns>
    public async Task<SurveyReportScope> ResolveAsync(ReportContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var surveyId = context.Filter.GetOrDefault<string>(SurveyReportKeys.SurveyId);

        if (string.IsNullOrEmpty(surveyId))
        {
            return null;
        }

        var survey = await _surveys.FindByIdAsync(surveyId, cancellationToken);

        if (survey is null || !await CanViewResponsesAsync(context.User, survey, cancellationToken))
        {
            return null;
        }

        var versions = await _versions.ListBySurveyAsync(survey.ItemId, cancellationToken);
        var scope = context.Filter.GetOrDefault<string>(SurveyReportKeys.Version);
        IReadOnlyList<SurveyVersion> selected = scope switch
        {
            SurveyReportKeys.AllVersions => versions,
            _ when int.TryParse(scope, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) => versions.Where(version => version.Number == number).ToList(),
            _ => versions.Where(version => version.Number == survey.PublishedVersionNumber).ToList(),
        };

        if (selected.Count == 0)
        {
            selected = versions.Take(1).ToList();
        }

        var range = context.Filter.GetDateRange();
        var collectorId = context.Filter.GetOrDefault<string>(SurveyReportKeys.CollectorId);
        var contextKind = context.Filter.GetOrDefault<string>(SurveyReportKeys.ContextKind);
        var numbers = scope == SurveyReportKeys.AllVersions ? new List<int>() : selected.Select(version => version.Number).ToList();
        var to = range.ToUtc is { } end && end.Ticks < DateTime.MaxValue.Ticks ? end.AddTicks(1) : (DateTime?)null;
        var from = range.FromUtc;
        var anonymous = SurveyAnonymity.IsAnonymous(survey);

        if (anonymous)
        {
            // The times of an anonymous survey's responses show by the day only, so its date filter selects whole days in
            // the site's time zone: stepping the filter's time of day can't tell, minute by minute, when a response came in.
            (from, to) = SurveyAnonymity.WholeDays(from, to, await SiteTimeZoneAsync());
        }

        return new SurveyReportScope
        {
            Survey = survey,
            Versions = selected,
            AllVersions = versions.ToDictionary(version => version.ItemId, StringComparer.Ordinal),
            Definition = (selected.Count > 0 ? selected[0].Definition : null),
            IsAnonymous = anonymous,
            MinimumGroupSize = SurveyAnonymity.MinimumGroupSize(survey, await SettingsAsync()),
            CanSeeIdentity = await CanSeeIdentityAsync(context.User, survey),
            AnswerFilter = new SurveyAnswerFilter
            {
                SurveyId = survey.ItemId,
                VersionNumbers = numbers,
                CompletedFromUtc = from,
                CompletedToUtc = to,
                CollectorId = collectorId,
                ContextKind = contextKind,
            },
            ResultsScope = new SurveyResultsScope
            {
                SurveyId = survey.ItemId,
                VersionNumbers = numbers,
                FromUtc = from,
                ToUtc = to,
                CollectorId = collectorId,
                ContextKind = contextKind,
                IncludeTest = context.Filter.GetOrDefault<bool>(SurveyReportKeys.IncludeTest),
            },
        };
    }

    /// <summary>
    /// Makes the document a report shows when no survey is selected.
    /// </summary>
    /// <returns>The document.</returns>
    public ReportDocument ChooseSurvey()
        => new ReportDocument().Add(new ReportSection
        {
            Title = S["Survey"].Value,
            Kind = ReportSectionKind.Metrics,
            EmptyMessage = S["Choose a survey whose responses you may view."].Value,
        });

    /// <summary>
    /// Makes the document a report shows when an anonymous survey has too few responses to show.
    /// </summary>
    /// <param name="scope">The scope.</param>
    /// <returns>The document.</returns>
    public ReportDocument TooFewResponses(SurveyReportScope scope)
        => new ReportDocument().Add(new ReportSection
        {
            Title = scope.Survey.Title,
            Kind = ReportSectionKind.Metrics,
            EmptyMessage = S["This survey is anonymous, and fewer than {0} responses match these filters. Results are hidden so no one can be recognized.", scope.MinimumGroupSize].Value,
        });

    /// <summary>
    /// Converts a time stored in UTC to the user's or site's time zone and formats it.
    /// </summary>
    /// <param name="utc">The time in UTC.</param>
    /// <returns>The local time as text, or an empty string.</returns>
    public async Task<string> LocalAsync(DateTime? utc)
    {
        if (utc is not { } value || value == DateTime.MinValue)
        {
            return string.Empty;
        }

        var local = await _localClock.ConvertToLocalAsync(new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc)));

        return local.DateTime.ToString("g", CultureInfo.CurrentCulture);
    }

    /// <summary>
    /// Formats the date only of a time stored in UTC, as the times of an anonymous survey are shown: the day in the site's
    /// time zone, never the viewer's (<see cref="SurveyAnonymousDays"/>), so changing one's own time zone can't move a
    /// response's day and tell its hour.
    /// </summary>
    /// <param name="utc">The time in UTC.</param>
    /// <returns>The site's date as text, or an empty string.</returns>
    public Task<string> LocalDayAsync(DateTime? utc)
        => _days.TextAsync(utc);

    /// <summary>
    /// Gets the day a time stored in UTC falls on in the site's time zone, as the days of an anonymous survey are read.
    /// </summary>
    /// <param name="utc">The time in UTC.</param>
    /// <returns>The day.</returns>
    public async Task<DateTime> SiteDayAsync(DateTime utc)
        => SurveyAnonymity.Day(utc, await SiteTimeZoneAsync()).ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);

    /// <summary>
    /// Gets the time the report runs in the user's or site's time zone, which relative color rules are measured from.
    /// </summary>
    /// <returns>The local time.</returns>
    public async Task<DateTime> NowAsync()
        => (await _localClock.GetLocalNowAsync()).DateTime;

    /// <summary>
    /// Converts a time stored in UTC to the local date and time.
    /// </summary>
    /// <param name="utc">The time in UTC.</param>
    /// <returns>The local date and time.</returns>
    public async Task<DateTime> ToLocalAsync(DateTime utc)
        => (await _localClock.ConvertToLocalAsync(new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)))).DateTime;

    /// <summary>
    /// Gets the site's time zone, which the days of anonymous responses are read in.
    /// </summary>
    /// <returns>The time zone (<see cref="SurveyAnonymousDays.TimeZoneAsync"/>).</returns>
    public Task<TimeZoneInfo> SiteTimeZoneAsync()
        => _days.TimeZoneAsync();

    private async Task<SurveySettings> SettingsAsync()
        => _serviceProvider.GetService<ISiteService>() is { } siteService ? await siteService.GetSettingsAsync<SurveySettings>() : null;

    /// <summary>
    /// Formats a number in the current culture.
    /// </summary>
    /// <param name="value">The number.</param>
    /// <param name="decimals">The decimals.</param>
    /// <returns>The text, or a dash.</returns>
    public static string Number(decimal? value, int decimals = 2)
        => value is { } number ? Math.Round(number, decimals, MidpointRounding.AwayFromZero).ToString("N" + decimals.ToString(CultureInfo.InvariantCulture), CultureInfo.CurrentCulture) : "-";

    /// <summary>
    /// Formats a percent held as a number from 0 to 100.
    /// </summary>
    /// <param name="value">The percent.</param>
    /// <returns>The text, or a dash.</returns>
    public static string Percent(decimal? value)
        => value is { } number ? ReportFormat.Percent((double)number / 100) : "-";
}
