using CrestApps.OrchardCore.Skills.Models;
using CrestApps.OrchardCore.Skills.Services;
using CrestApps.OrchardCore.Staff.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Learning.Credentials.Core.Rules;

/// <summary>
/// The certification issued rule: a Skills certificate issued or renewed for a user, or for a staff member who has a
/// user account.
/// </summary>
public sealed class CredentialCertificationObserver : ICertificationRecordObserver
{
    private readonly CredentialRuleTrigger _trigger;
    private readonly IServiceProvider _services;

    /// <summary>
    /// Initializes a new instance of the <see cref="CredentialCertificationObserver"/> class.
    /// </summary>
    /// <param name="trigger">The rule trigger.</param>
    /// <param name="services">The services, for the staff directory when Staff is on.</param>
    public CredentialCertificationObserver(CredentialRuleTrigger trigger, IServiceProvider services)
    {
        _trigger = trigger;
        _services = services;
    }

    /// <inheritdoc/>
    public Task IssuedAsync(CertificationRecordContext context, CancellationToken cancellationToken = default)
        => TriggerAsync(context?.Record, cancellationToken);

    /// <inheritdoc/>
    public Task RenewedAsync(CertificationRecordContext context, CancellationToken cancellationToken = default)
        => TriggerAsync(context?.Record, cancellationToken);

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
