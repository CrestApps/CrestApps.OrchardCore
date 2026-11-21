using CrestApps.OrchardCore.Learning.Credentials.Services;
using Microsoft.Extensions.Localization;
using YesSql;

namespace CrestApps.OrchardCore.Learning.Credentials.Rules;

/// <summary>
/// The course completed rule in the definition editor.
/// </summary>
internal sealed class CourseCompletedRuleKind : ICredentialRuleKind
{
    private readonly ISession _session;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="CourseCompletedRuleKind"/> class.
    /// </summary>
    /// <param name="session">The session.</param>
    /// <param name="stringLocalizer">The localizer.</param>
    public CourseCompletedRuleKind(ISession session, IStringLocalizer<CourseCompletedRuleKind> stringLocalizer)
    {
        _session = session;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public string Kind => CredentialsConstants.RuleKinds.CourseCompleted;

    /// <inheritdoc/>
    public LocalizedString DisplayName => S["Course completed"];

    /// <inheritdoc/>
    public Task<IReadOnlyList<CredentialRuleTarget>> ListTargetsAsync(CancellationToken cancellationToken = default)
        => ContentRuleTargets.ListAsync(_session, LearningConstants.ContentTypes.Course, cancellationToken);
}
