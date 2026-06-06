namespace CrestApps.OrchardCore.DncRegistry;

/// <summary>
/// Represents a national do-not-call registry provider.
/// Implementations are registered as named providers that can be selected
/// individually during import or enforced globally through site settings.
/// </summary>
public interface INationalDoNotCallRegistry
{
    /// <summary>
    /// Gets the unique key identifying this registry implementation.
    /// </summary>
    string Key { get; }

    /// <summary>
    /// Gets the localized display name of this registry.
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Gets the localized description of this registry.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Checks whether the given phone numbers are listed on this registry.
    /// </summary>
    /// <param name="phoneNumbers">The phone numbers to check.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>
    /// A set of phone numbers from the input that are listed on the registry.
    /// </returns>
    Task<HashSet<string>> GetRegisteredNumbersAsync(
        IEnumerable<string> phoneNumbers,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether the given phone numbers are listed on this registry,
    /// applying additional filtering criteria from the search context.
    /// </summary>
    /// <param name="phoneNumbers">The phone numbers to check.</param>
    /// <param name="context">The search context containing additional filters such as country.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>
    /// A set of phone numbers from the input that are listed on the registry.
    /// </returns>
    Task<HashSet<string>> GetRegisteredNumbersAsync(
        IEnumerable<string> phoneNumbers,
        NumberSearchContext context,
        CancellationToken cancellationToken = default)
        => GetRegisteredNumbersAsync(phoneNumbers, cancellationToken);
}
