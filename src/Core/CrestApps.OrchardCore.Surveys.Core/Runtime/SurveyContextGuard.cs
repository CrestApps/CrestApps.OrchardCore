using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.Surveys.Core.Runtime;

/// <summary>
/// Decides whether a context a caller names (through the Surveys API) may be used: its kind must be registered with
/// <c>AddSurveyContextKind</c>, and a registered <see cref="ISurveyContextOwnershipCheck"/> of the kind must say the
/// caller owns the record. Anything else is refused, so a caller can neither split attempt limits across made-up
/// contexts nor start workflows with a context kind that does not exist.
/// </summary>
public sealed class SurveyContextGuard
{
    private readonly SurveyContextKindOptions _kinds;
    private readonly IEnumerable<ISurveyContextOwnershipCheck> _checks;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyContextGuard"/> class.
    /// </summary>
    /// <param name="kinds">The registered context kinds.</param>
    /// <param name="checks">The ownership checks.</param>
    public SurveyContextGuard(IOptions<SurveyContextKindOptions> kinds, IEnumerable<ISurveyContextOwnershipCheck> checks)
    {
        _kinds = kinds?.Value ?? new SurveyContextKindOptions();
        _checks = checks ?? [];
    }

    /// <summary>
    /// Gets whether the respondent may use a context for a response to a survey.
    /// </summary>
    /// <param name="survey">The survey.</param>
    /// <param name="context">The context.</param>
    /// <param name="identity">The respondent.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when the kind is registered and the respondent owns the record.</returns>
    public async Task<bool> IsAllowedAsync(Survey survey, RecordRef context, SurveyRespondentIdentity identity, CancellationToken cancellationToken = default)
    {
        if (survey is null || context.IsEmpty || string.IsNullOrEmpty(context.Kind) || string.IsNullOrEmpty(context.Id))
        {
            return false;
        }

        // Kinds are compared exactly: the record kind is part of the stored context and of the attempt counts.
        if (!_kinds.Kinds.TryGetValue(context.Kind, out var descriptor) || !string.Equals(descriptor?.Kind, context.Kind, StringComparison.Ordinal))
        {
            return false;
        }

        var checkedAny = false;

        foreach (var check in _checks)
        {
            if (!check.CanCheck(context.Kind))
            {
                continue;
            }

            checkedAny = true;

            if (!await check.IsOwnerAsync(survey, context, identity ?? new SurveyRespondentIdentity(), cancellationToken))
            {
                return false;
            }
        }

        return checkedAny;
    }
}
