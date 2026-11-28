using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using CrestApps.OrchardCore.Users;
using OrchardCore;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Surveys.Core.Scoring;

/// <summary>
/// Grants, lists and revokes accommodations, and combines those that apply to a respondent into the most generous one.
/// </summary>
public sealed class SurveyOverrideService : ISurveyOverrideService
{
    private readonly SurveyOverrideStore _store;
    private readonly IEnumerable<IUserAudienceService> _audiences;
    private readonly ISurveyScoringEventDispatcher _dispatcher;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyOverrideService"/> class.
    /// </summary>
    /// <param name="store">The accommodation store.</param>
    /// <param name="audiences">The user audience service, when the Audiences feature is on.</param>
    /// <param name="dispatcher">Tells observers.</param>
    /// <param name="clock">The clock.</param>
    public SurveyOverrideService(
        SurveyOverrideStore store,
        IEnumerable<IUserAudienceService> audiences,
        ISurveyScoringEventDispatcher dispatcher,
        IClock clock)
    {
        _store = store;
        _audiences = audiences;
        _dispatcher = dispatcher;
        _clock = clock;
    }

    /// <inheritdoc />
    public async Task<SurveyErrorCode> GrantAsync(SurveyOverride surveyOverride, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(surveyOverride);

        if (string.IsNullOrEmpty(surveyOverride.SurveyId)
            || string.IsNullOrWhiteSpace(surveyOverride.Reason)
            || (string.IsNullOrEmpty(surveyOverride.UserId) && string.IsNullOrEmpty(surveyOverride.AudienceId))
            || surveyOverride.ExtraMinutes < 0
            || surveyOverride.ExtraAttempts < 0
            || (surveyOverride.OpensUtc is { } opens && surveyOverride.ClosesUtc is { } closes && closes <= opens))
        {
            return SurveyErrorCode.InvalidAnswer;
        }

        surveyOverride.ItemId = string.IsNullOrEmpty(surveyOverride.ItemId) ? IdGenerator.GenerateId() : surveyOverride.ItemId;
        surveyOverride.Reason = surveyOverride.Reason.Trim();
        surveyOverride.CreatedUtc = _clock.UtcNow;

        await _store.CreateAsync(surveyOverride, cancellationToken);
        _dispatcher.OverrideGranted(surveyOverride);

        return SurveyErrorCode.None;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<SurveyOverride>> ListAsync(string surveyId, CancellationToken cancellationToken = default)
        => _store.ListBySurveyAsync(surveyId, cancellationToken);

    /// <inheritdoc />
    public async Task<bool> RevokeAsync(string overrideId, CancellationToken cancellationToken = default)
    {
        var existing = string.IsNullOrEmpty(overrideId) ? null : await _store.FindByIdAsync(overrideId, cancellationToken);

        return existing is not null && await _store.DeleteAsync(existing, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<SurveyOverride> FindEffectiveAsync(string surveyId, string userId, RecordRef? context, CancellationToken cancellationToken = default)
    {
        // Accommodations are granted to users and audiences of users, so a guest has none. Only the respondent's own
        // records and the audience ones are read: the runner asks on every request.
        if (string.IsNullOrEmpty(surveyId) || string.IsNullOrEmpty(userId))
        {
            return null;
        }

        var audience = _audiences.FirstOrDefault();
        var applicable = new List<SurveyOverride>();

        foreach (var candidate in await _store.ListForUserAsync(surveyId, userId, cancellationToken))
        {
            if (candidate.Context is { } scope && (context is null || !scope.Equals(context.Value)))
            {
                continue;
            }

            if (!string.IsNullOrEmpty(candidate.UserId))
            {
                if (string.Equals(candidate.UserId, userId, StringComparison.Ordinal))
                {
                    applicable.Add(candidate);
                }

                continue;
            }

            if (!string.IsNullOrEmpty(candidate.AudienceId)
                && !string.IsNullOrEmpty(userId)
                && audience is not null
                && await audience.IsMemberAsync(candidate.AudienceId, userId, cancellationToken))
            {
                applicable.Add(candidate);
            }
        }

        return Combine(surveyId, userId, context, applicable);
    }

    /// <summary>
    /// Combines accommodations into the most generous one: the most extra time and attempts, the earliest opening and
    /// the latest closing.
    /// </summary>
    /// <param name="surveyId">The survey.</param>
    /// <param name="userId">The user.</param>
    /// <param name="context">The context.</param>
    /// <param name="overrides">The accommodations that apply.</param>
    /// <returns>The combined accommodation, or <see langword="null"/> when none applies.</returns>
    public static SurveyOverride Combine(string surveyId, string userId, RecordRef? context, IReadOnlyCollection<SurveyOverride> overrides)
    {
        if (overrides is null || overrides.Count == 0)
        {
            return null;
        }

        return new SurveyOverride
        {
            SurveyId = surveyId,
            UserId = userId,
            Context = context,
            ExtraMinutes = overrides.Max(item => item.ExtraMinutes),
            ExtraAttempts = overrides.Max(item => item.ExtraAttempts),
            OpensUtc = overrides.Where(item => item.OpensUtc is not null).Select(item => item.OpensUtc).Min(),
            ClosesUtc = overrides.Where(item => item.ClosesUtc is not null).Select(item => item.ClosesUtc).Max(),
            Reason = string.Join("; ", overrides.Select(item => item.Reason).Where(reason => !string.IsNullOrEmpty(reason)).Distinct(StringComparer.Ordinal)),
        };
    }
}
