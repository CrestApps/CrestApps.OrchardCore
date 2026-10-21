using CrestApps.OrchardCore.Addresses.Models;

namespace CrestApps.OrchardCore.Addresses.Core.Autocomplete;

/// <summary>
/// Suggests addresses as a street is typed, and gives the parts of the one picked, so the address editor can fill its
/// fields. A provider calls a service such as Google Places with the store's key; the browser never sees the key.
/// </summary>
public interface IAddressAutocompleteProvider
{
    /// <summary>
    /// Returns whether the provider is set up to answer, such as with an API key.
    /// </summary>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns addresses that start with what was typed, in a country when one is given.
    /// </summary>
    Task<IReadOnlyList<AddressSuggestion>> SuggestAsync(AddressSuggestionRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the parts of a suggested address, or <see langword="null"/> when it can't be found.
    /// </summary>
    Task<Address> ResolveAsync(string suggestionId, string sessionToken, CancellationToken cancellationToken = default);
}

/// <summary>
/// An address suggested for what was typed.
/// </summary>
public sealed record AddressSuggestion(string Id, string Text);

/// <summary>
/// What was typed, and where.
/// </summary>
public sealed class AddressSuggestionRequest
{
    public string Text { get; init; }

    /// <summary>
    /// Gets the two-letter code of the country to suggest in, or <see langword="null"/> for anywhere.
    /// </summary>
    public string Country { get; init; }

    /// <summary>
    /// Gets the token that groups a shopper's typing and their pick into one session, which some services bill as one.
    /// </summary>
    public string SessionToken { get; init; }

    /// <summary>
    /// Gets the language to suggest in, such as de.
    /// </summary>
    public string Language { get; init; }
}

public static class AddressAutocompleteProviderExtensions
{
    /// <summary>
    /// Returns the first provider set up to answer, or <see langword="null"/>.
    /// </summary>
    public static async Task<IAddressAutocompleteProvider> GetAvailableAsync(this IEnumerable<IAddressAutocompleteProvider> providers, CancellationToken cancellationToken = default)
    {
        foreach (var provider in providers ?? [])
        {
            if (await provider.IsAvailableAsync(cancellationToken))
            {
                return provider;
            }
        }

        return null;
    }
}
