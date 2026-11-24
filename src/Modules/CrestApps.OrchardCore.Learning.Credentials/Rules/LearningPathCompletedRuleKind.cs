using CrestApps.OrchardCore.Learning.Credentials.Services;
using CrestApps.OrchardCore.Learning.Programs;
using Microsoft.Extensions.Localization;
using YesSql;

namespace CrestApps.OrchardCore.Learning.Credentials.Rules;

/// <summary>
/// The learning path completed rule in the definition editor, when Learning Paths is on.
/// </summary>
internal sealed class LearningPathCompletedRuleKind : ICredentialRuleKind
{
    private readonly ISession _session;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="LearningPathCompletedRuleKind"/> class.
    /// </summary>
    /// <param name="session">The session.</param>
    /// <param name="stringLocalizer">The localizer.</param>
    public LearningPathCompletedRuleKind(ISession session, IStringLocalizer<LearningPathCompletedRuleKind> stringLocalizer)
    {
        _session = session;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public string Kind => CredentialsConstants.RuleKinds.LearningPathCompleted;

    /// <inheritdoc/>
    public LocalizedString DisplayName => S["Learning path completed"];

    /// <inheritdoc/>
    public Task<IReadOnlyList<CredentialRuleTarget>> ListTargetsAsync(CancellationToken cancellationToken = default)
        => ContentRuleTargets.ListAsync(_session, ProgramsConstants.LearningPathContentType, cancellationToken);
}
