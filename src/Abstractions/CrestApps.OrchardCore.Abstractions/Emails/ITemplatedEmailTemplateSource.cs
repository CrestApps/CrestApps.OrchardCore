namespace CrestApps.OrchardCore.Emails;

/// <summary>
/// Keeps the site's templates of one group of email kinds, such as the store's templates of its emails in the
/// Commerce settings. <see cref="ITemplatedEmailTemplates"/> asks each source in turn and renders what the first one
/// that knows the kind gives.
/// </summary>
public interface ITemplatedEmailTemplateSource
{
    /// <summary>
    /// Returns the site's template of a kind and the layout that goes around it, or <see langword="null"/> when the
    /// source doesn't keep that kind.
    /// </summary>
    /// <param name="kind">The kind of email.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The template and layout, or <see langword="null"/>.</returns>
    Task<TemplatedEmailOverride> FindAsync(string kind, CancellationToken cancellationToken = default);
}
