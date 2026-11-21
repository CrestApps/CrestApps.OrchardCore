using CrestApps.OrchardCore.Learning.Assessments;
using CrestApps.OrchardCore.Learning.Credentials.Services;
using Microsoft.Extensions.Localization;
using YesSql;

namespace CrestApps.OrchardCore.Learning.Credentials.Rules;

/// <summary>
/// The quiz passed rule in the definition editor, when Assessments is on.
/// </summary>
internal sealed class QuizPassedRuleKind : ICredentialRuleKind
{
    private readonly ISession _session;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="QuizPassedRuleKind"/> class.
    /// </summary>
    /// <param name="session">The session.</param>
    /// <param name="stringLocalizer">The localizer.</param>
    public QuizPassedRuleKind(ISession session, IStringLocalizer<QuizPassedRuleKind> stringLocalizer)
    {
        _session = session;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public string Kind => CredentialsConstants.RuleKinds.QuizPassed;

    /// <inheritdoc/>
    public LocalizedString DisplayName => S["Quiz passed"];

    /// <inheritdoc/>
    public Task<IReadOnlyList<CredentialRuleTarget>> ListTargetsAsync(CancellationToken cancellationToken = default)
        => ContentRuleTargets.ListAsync(_session, AssessmentsConstants.ContentTypes.Quiz, cancellationToken);
}
