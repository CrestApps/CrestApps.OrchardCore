using CrestApps.OrchardCore.Omnichannel.Core.Models;

namespace CrestApps.OrchardCore.Omnichannel.Core.Services;

/// <summary>
/// Describes the dispositions an automated conversation may choose from, for the model that has to choose one.
/// </summary>
/// <remarks>
/// A disposition carries a description of what it means in general, and the subject workflow that uses it may say
/// something more specific about when it applies to this kind of work — the same disposition can mean different
/// things on a sales follow-up and on a service call. The workflow's wording wins when there is one, because it
/// was written with the general description in front of the author and is therefore the more complete answer.
/// <para>
/// Both the voice and the SMS conclusion ask this question, so it is answered once here. Two copies of this rule
/// would be two places for the guidance an operator wrote to quietly not reach the model.
/// </para>
/// </remarks>
public static class SubjectDispositionGuidance
{
    /// <summary>
    /// Pairs each disposition with the description the model should judge it by.
    /// </summary>
    /// <param name="dispositions">The dispositions on offer.</param>
    /// <param name="actions">Every subject action configured, of any subject type.</param>
    /// <param name="subjectContentType">The subject content type this conversation is about.</param>
    public static IReadOnlyList<DispositionChoice> Describe(
        IEnumerable<OmnichannelDisposition> dispositions,
        IEnumerable<SubjectAction> actions,
        string subjectContentType)
    {
        if (dispositions is null)
        {
            return [];
        }

        var guidance = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (actions is not null && !string.IsNullOrEmpty(subjectContentType))
        {
            foreach (var action in actions)
            {
                if (string.IsNullOrEmpty(action?.DispositionId) ||
                    string.IsNullOrWhiteSpace(action.DispositionGuidance) ||
                    !string.Equals(action.SubjectContentType, subjectContentType, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // First one wins rather than last, so a duplicate configuration cannot silently change what the
                // model is told from one call to the next.
                guidance.TryAdd(action.DispositionId, action.DispositionGuidance.Trim());
            }
        }

        return
        [
            .. dispositions
                .Where(disposition => disposition is not null)
                .Select(disposition => new DispositionChoice
                {
                    Id = disposition.ItemId,
                    Name = disposition.Name,
                    Description = guidance.TryGetValue(disposition.ItemId ?? string.Empty, out var specific)
                        ? specific
                        : disposition.Description,
                })
        ];
    }
}

/// <summary>
/// One disposition as the model sees it.
/// </summary>
public sealed class DispositionChoice
{
    /// <summary>
    /// Gets the disposition identifier the model returns to choose this one.
    /// </summary>
    public string Id { get; init; }

    /// <summary>
    /// Gets the disposition name.
    /// </summary>
    public string Name { get; init; }

    /// <summary>
    /// Gets what this disposition means, as specifically as it has been described.
    /// </summary>
    public string Description { get; init; }
}
