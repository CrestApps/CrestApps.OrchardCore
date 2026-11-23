using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Surveys.Core.Access;

/// <summary>
/// Checks a start with the access rules a start runs, with the same access context (the respondent key, the
/// effective accommodation and the context), without starting a response. The attempts left come from the respondent
/// limits rule itself, so a page never counts attempts differently from the rule that refuses the start.
/// </summary>
public sealed class SurveyStartCheck : ISurveyStartCheck
{
    private readonly SurveyStore _surveyStore;
    private readonly SurveyCollectorStore _collectorStore;
    private readonly ISurveyVersionCache _versionCache;
    private readonly SurveyAccessEvaluator _access;
    private readonly IEnumerable<ISurveyAccessRule> _rules;
    private readonly IServiceProvider _serviceProvider;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyStartCheck"/> class.
    /// </summary>
    /// <param name="surveyStore">The survey store.</param>
    /// <param name="collectorStore">The collector store.</param>
    /// <param name="versionCache">The version cache.</param>
    /// <param name="access">The access evaluator.</param>
    /// <param name="rules">The access rules.</param>
    /// <param name="serviceProvider">The service provider.</param>
    /// <param name="clock">The clock.</param>
    public SurveyStartCheck(
        SurveyStore surveyStore,
        SurveyCollectorStore collectorStore,
        ISurveyVersionCache versionCache,
        SurveyAccessEvaluator access,
        IEnumerable<ISurveyAccessRule> rules,
        IServiceProvider serviceProvider,
        IClock clock)
    {
        _surveyStore = surveyStore;
        _collectorStore = collectorStore;
        _versionCache = versionCache;
        _access = access;
        _rules = rules;
        _serviceProvider = serviceProvider;
        _clock = clock;
    }

    /// <inheritdoc />
    public async Task<SurveyStartCheckResult> CheckAsync(StartResponseRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var identity = request.Identity ?? new SurveyRespondentIdentity();
        SurveyCollector collector = null;
        Survey survey = null;

        if (!string.IsNullOrWhiteSpace(request.CollectorToken))
        {
            collector = await _collectorStore.FindByTokenAsync(request.CollectorToken.Trim(), cancellationToken);
            survey = collector is null ? null : await _surveyStore.FindByIdAsync(collector.SurveyId, cancellationToken);
        }
        else if (!string.IsNullOrEmpty(request.SurveyId))
        {
            survey = await _surveyStore.FindByIdAsync(request.SurveyId, cancellationToken);
            collector = survey is null ? null : await _collectorStore.FindDefaultAsync(survey.ItemId, cancellationToken);
        }

        if (survey is null)
        {
            return new SurveyStartCheckResult { Error = SurveyErrorCode.NotFound };
        }

        var version = await _versionCache.GetPublishedAsync(survey, cancellationToken);

        if (version?.Definition is null)
        {
            return new SurveyStartCheckResult { Error = SurveyErrorCode.NotOpen };
        }

        var overrides = _serviceProvider.GetService<ISurveyOverrideService>();
        var context = new SurveyAccessContext
        {
            Survey = survey,
            Version = version,
            Collector = collector,
            Identity = identity,
            RespondentKey = await _serviceProvider.GetRequiredService<SurveyAnonymitySecrets>().RespondentKeyAsync(survey, identity, null, cancellationToken),
            Context = request.Context,
            Override = overrides is null ? null : await overrides.FindEffectiveAsync(survey.ItemId, identity.UserId, request.Context, cancellationToken),
            Password = request.Password,
            UtcNow = _clock.UtcNow,
        };

        var decision = await _access.CheckStartAsync(context);
        var limits = _rules.OfType<RespondentLimitsRule>().FirstOrDefault();

        return new SurveyStartCheckResult
        {
            Error = decision.Error,
            AttemptsLeft = limits is null ? null : await limits.GetAttemptsLeftAsync(context),
        };
    }
}
