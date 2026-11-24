using CrestApps.OrchardCore.Learning.Credentials.Services;
using OrchardCore.ContentManagement.Records;
using YesSql;

namespace CrestApps.OrchardCore.Learning.Credentials.Rules;

/// <summary>
/// Lists the published content items of a type as rule targets.
/// </summary>
internal static class ContentRuleTargets
{
    /// <summary>
    /// Lists the published content items of a type, by title.
    /// </summary>
    /// <param name="session">The session.</param>
    /// <param name="contentType">The content type.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The targets.</returns>
    public static async Task<IReadOnlyList<CredentialRuleTarget>> ListAsync(ISession session, string contentType, CancellationToken cancellationToken)
        => (await session.QueryIndex<ContentItemIndex>(index => index.ContentType == contentType && index.Published)
                .OrderBy(index => index.DisplayText)
                .ListAsync(cancellationToken))
            .Select(index => new CredentialRuleTarget(index.ContentItemId, string.IsNullOrWhiteSpace(index.DisplayText) ? index.ContentItemId : index.DisplayText))
            .ToArray();
}
