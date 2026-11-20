using System.ComponentModel.DataAnnotations;
using CrestApps.Core;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Agreements.Models;
using CrestApps.OrchardCore.Agreements.Services;
using CrestApps.OrchardCore.ServiceLevels.Services;
using CrestApps.OrchardCore.Tickets.Models;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Tickets.Agreements;

/// <summary>
/// Checks an agreement's ticket terms: the desks it covers exist, and its policy is a ticket policy.
/// </summary>
internal sealed class TicketAgreementTermsValidator : IAgreementTermsValidator
{
    private readonly ICatalog<TicketDesk> _desks;
    private readonly IServiceLevelPolicyManager _policies;

    internal readonly IStringLocalizer S;

    public TicketAgreementTermsValidator(ICatalog<TicketDesk> desks, IServiceLevelPolicyManager policies, IStringLocalizer<TicketAgreementTermsValidator> stringLocalizer)
    {
        _desks = desks;
        _policies = policies;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ValidationResult>> ValidateAsync(Agreement agreement, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(agreement);

        var errors = new List<ValidationResult>();

        if (!agreement.TryGet<TicketAgreementTerms>(out var terms) || terms is null)
        {
            return errors;
        }

        if (terms.DeskIds is { Count: > 0 })
        {
            var desks = (await _desks.GetAllAsync(cancellationToken)).Select(desk => desk.ItemId).ToHashSet(StringComparer.Ordinal);

            if (terms.DeskIds.Any(deskId => !desks.Contains(deskId)))
            {
                errors.Add(new ValidationResult(S["Choose desks that exist."], [nameof(TicketAgreementTerms.DeskIds)]));
            }
        }

        if (!string.IsNullOrEmpty(terms.ServiceLevelPolicyId)
            && !(await _policies.ListByScopeAsync(TicketsConstants.ServiceLevelScope, cancellationToken)).Any(policy => policy.ItemId == terms.ServiceLevelPolicyId))
        {
            errors.Add(new ValidationResult(S["Choose a ticket service-level policy that exists."], [nameof(TicketAgreementTerms.ServiceLevelPolicyId)]));
        }

        return errors;
    }
}
