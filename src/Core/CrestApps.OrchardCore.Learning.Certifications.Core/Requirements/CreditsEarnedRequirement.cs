using System.Globalization;
using CrestApps.OrchardCore.Learning.Certifications.Core.Services;
using CrestApps.OrchardCore.Learning.Certifications.Models;
using CrestApps.OrchardCore.Learning.Certifications.Services;
using CrestApps.OrchardCore.Ledgers;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Learning.Certifications.Core.Requirements;

/// <summary>
/// A certification requirement met by earning a number of education credits of a type in the cycle: the requirement's
/// target is the credit type and its minimum is the number of credits.
/// </summary>
public sealed class CreditsEarnedRequirement : ICertificationRequirement
{
    private readonly IQuantityLedger<EducationCreditLedger> _ledger;
    private readonly EducationCreditTypeStore _types;
    private readonly IClock _clock;
    private readonly ILocalClock _localClock;

    internal readonly IStringLocalizer S;

    public CreditsEarnedRequirement(IQuantityLedger<EducationCreditLedger> ledger, EducationCreditTypeStore types, IClock clock, ILocalClock localClock, IStringLocalizer<CreditsEarnedRequirement> stringLocalizer)
    {
        _ledger = ledger;
        _types = types;
        _clock = clock;
        _localClock = localClock;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public string Kind => CertificationsConstants.RequirementKinds.CreditsEarned;

    /// <inheritdoc/>
    public LocalizedString DisplayName => S["Education credits earned"];

    /// <inheritdoc/>
    public async Task<CertificationEvidence> FindEvidenceAsync(CertificationRequirementContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var requirement = context.Requirement;
        var needed = requirement.MinimumGrade ?? 0;

        if (string.IsNullOrEmpty(requirement.TargetId) || needed <= 0)
        {
            return null;
        }

        var today = DateOnly.FromDateTime((await _localClock.GetLocalNowAsync()).DateTime);
        var from = context.SinceUtc is { } since
            ? DateOnly.FromDateTime((await _localClock.ConvertToLocalAsync(new DateTimeOffset(DateTime.SpecifyKind(since, DateTimeKind.Utc)))).DateTime)
            : DateOnly.MinValue;
        var earned = await _ledger.SumAsync(context.UserId, requirement.TargetId, from, today, cancellationToken);

        if (earned < needed)
        {
            return null;
        }

        return new CertificationEvidence
        {
            RequirementId = requirement.Id,
            Kind = Kind,
            TargetId = requirement.TargetId,
            SourceId = $"credits:{requirement.TargetId}:{from:yyyyMMdd}",
            Title = await GetTitleAsync(requirement, cancellationToken),
            CompletedUtc = _clock.UtcNow,
            Grade = earned,
        };
    }

    /// <inheritdoc/>
    public Task<IReadOnlyCollection<string>> ListUsersSinceAsync(CertificationRequirement requirement, DateTime sinceUtc, CancellationToken cancellationToken = default)
        => string.IsNullOrEmpty(requirement?.TargetId)
            ? Task.FromResult<IReadOnlyCollection<string>>([])
            : _ledger.ListAccountsSinceAsync(requirement.TargetId, sinceUtc, cancellationToken);

    /// <inheritdoc/>
    public async Task<string> GetTitleAsync(CertificationRequirement requirement, CancellationToken cancellationToken = default)
    {
        var type = string.IsNullOrEmpty(requirement?.TargetId) ? null : await _types.FindByIdAsync(requirement.TargetId, cancellationToken);
        var amount = (requirement?.MinimumGrade ?? 0).ToString("0.##", CultureInfo.CurrentCulture);

        return type is null ? S["{0} credits", amount] : S["{0} {1}", amount, type.Name];
    }
}
