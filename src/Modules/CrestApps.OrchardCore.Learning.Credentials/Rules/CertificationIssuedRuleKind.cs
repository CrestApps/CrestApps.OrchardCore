using CrestApps.OrchardCore.Learning.Credentials.Services;
using CrestApps.OrchardCore.Skills.Services;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Learning.Credentials.Rules;

/// <summary>
/// The certification issued rule in the definition editor, when Learning Certifications is on.
/// </summary>
internal sealed class CertificationIssuedRuleKind : ICredentialRuleKind
{
    private readonly ICertificationManager _certifications;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="CertificationIssuedRuleKind"/> class.
    /// </summary>
    /// <param name="certifications">The Skills certifications.</param>
    /// <param name="stringLocalizer">The localizer.</param>
    public CertificationIssuedRuleKind(ICertificationManager certifications, IStringLocalizer<CertificationIssuedRuleKind> stringLocalizer)
    {
        _certifications = certifications;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public string Kind => CredentialsConstants.RuleKinds.CertificationIssued;

    /// <inheritdoc/>
    public LocalizedString DisplayName => S["Certification issued"];

    /// <inheritdoc/>
    public async Task<IReadOnlyList<CredentialRuleTarget>> ListTargetsAsync(CancellationToken cancellationToken = default)
        => (await _certifications.GetEnabledAsync(cancellationToken))
            .OrderBy(certification => certification.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(certification => new CredentialRuleTarget(certification.ItemId, certification.Name))
            .ToArray();
}
