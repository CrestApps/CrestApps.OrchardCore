using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Learning.Credentials.Services;

/// <summary>
/// A kind of award rule the credential definition editor offers. Register one with <c>AddCredentialRuleKind</c>; the
/// module that owns what the rule watches awards through <see cref="ICredentialAwardService"/>.
/// </summary>
public interface ICredentialRuleKind
{
    /// <summary>
    /// Gets the kind, an open string stored on the rule.
    /// </summary>
    string Kind { get; }

    /// <summary>
    /// Gets the name shown in the editor.
    /// </summary>
    LocalizedString DisplayName { get; }

    /// <summary>
    /// Lists what a rule of this kind may watch.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The targets.</returns>
    Task<IReadOnlyList<CredentialRuleTarget>> ListTargetsAsync(CancellationToken cancellationToken = default);
}
