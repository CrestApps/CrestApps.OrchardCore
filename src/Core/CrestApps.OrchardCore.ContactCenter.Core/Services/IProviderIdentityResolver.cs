namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Resolves a provider technical name or alias to its canonical technical identity. Canonicalization
/// runs before any inbox delivery, event, or call identity key is built so that provider-contributed
/// aliases (for example <c>Default Asterisk</c>) collapse to a single stable identity (for example
/// <c>Asterisk</c>).
/// </summary>
public interface IProviderIdentityResolver
{
    /// <summary>
    /// Resolves the specified provider name or alias to its canonical technical name.
    /// </summary>
    /// <param name="providerName">The provider technical name or alias to canonicalize.</param>
    /// <returns>
    /// The canonical technical name when the supplied name matches a contributed identity or alias;
    /// otherwise the supplied name unchanged. A <see langword="null"/> or empty input is returned as-is.
    /// </returns>
    string Canonicalize(string providerName);
}
