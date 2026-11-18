using CrestApps.OrchardCore.Learning.Credentials.Core.Services;
using CrestApps.OrchardCore.Skills.Models;
using CrestApps.OrchardCore.Skills.Services;
using CrestApps.OrchardCore.Staff.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Learning.Credentials.Core.Rules;

/// <summary>
/// The certification issued rule: a Skills certificate issued or renewed for a user, or for a staff member who has a
/// user account. When the certificate is revoked, the credentials it awarded are revoked with it.
/// </summary>
public sealed class CredentialCertificationObserver : ICertificationRecordObserver
{
    private readonly CredentialRuleTrigger _trigger;
    private readonly IServiceProvider _services;
    private readonly CredentialSourceRevoker _revoker;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="CredentialCertificationObserver"/> class.
    /// </summary>
    /// <param name="trigger">The rule trigger.</param>
    /// <param name="services">The services, for the staff directory when Staff is on.</param>
    /// <param name="revoker">Revokes the awards that rest on a revoked certificate.</param>
    /// <param name="stringLocalizer">The localizer of the revocation reason.</param>
    public CredentialCertificationObserver(
        CredentialRuleTrigger trigger,
        IServiceProvider services,
        CredentialSourceRevoker revoker,
        IStringLocalizer<CredentialCertificationObserver> stringLocalizer)
    {
        _trigger = trigger;
        _services = services;
        _revoker = revoker;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public Task IssuedAsync(CertificationRecordContext context, CancellationToken cancellationToken = default)
        => TriggerAsync(context?.Record, cancellationToken);

    /// <inheritdoc/>
    public Task RenewedAsync(CertificationRecordContext context, CancellationToken cancellationToken = default)
        => TriggerAsync(context?.Record, cancellationToken);

    /// <inheritdoc/>
    public Task RevokedAsync(CertificationRecordContext context, CancellationToken cancellationToken = default)
    {
        if (context?.Record is not { } record || string.IsNullOrEmpty(record.ItemId))
        {
            return Task.CompletedTask;
        }

        return _revoker.RevokeAsync(
            CredentialsConstants.RuleKinds.CertificationIssued,
            record.ItemId,
            context.Actor,
            S["The certificate {0} that this credential rests on was revoked.", record.Number ?? record.ItemId],
            cancellationToken);
    }

    private async Task TriggerAsync(CertificationRecord record, CancellationToken cancellationToken)
    {
        if (record is null || record.Status == CertificationStatus.Revoked)
        {
            return;
        }

        var userId = await UserIdAsync(record, cancellationToken);

        await _trigger.TriggerAsync(CredentialsConstants.RuleKinds.CertificationIssued, record.CertificationId, userId, record.ItemId, cancellationToken);
    }

    private async Task<string> UserIdAsync(CertificationRecord record, CancellationToken cancellationToken)
    {
        var kind = SkillSubjectKind.Parse(record.SubjectKind);

        if (kind == SkillSubjectKind.User)
        {
            return record.SubjectId;
        }

        if (kind == SkillSubjectKind.Staff && _services.GetService<IStaffDirectory>() is { } directory)
        {
            var members = await directory.GetAsync([record.SubjectId], cancellationToken);

            return members.TryGetValue(record.SubjectId, out var member) ? member.UserId : null;
        }

        return null;
    }
}
