using System.ComponentModel.DataAnnotations;
using CrestApps.Core;
using CrestApps.OrchardCore.Agreements.Models;
using CrestApps.OrchardCore.Agreements.Services;
using CrestApps.OrchardCore.Calendars.Recurrence;
using CrestApps.OrchardCore.WorkOrders.Models;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.WorkOrders.Core.Services;

/// <summary>
/// Checks an agreement's Work Orders terms: each visit schedule's type, rule and lead days, and the price of a visit
/// when each is invoiced.
/// </summary>
internal sealed class WorkOrderAgreementTermsValidator : IAgreementTermsValidator
{
    internal readonly IStringLocalizer S;

    public WorkOrderAgreementTermsValidator(IStringLocalizer<WorkOrderAgreementTermsValidator> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<ValidationResult>> ValidateAsync(Agreement agreement, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(agreement);

        var errors = new List<ValidationResult>();

        if (!agreement.TryGet<WorkOrderAgreementTerms>(out var terms) || terms is null)
        {
            return Task.FromResult<IReadOnlyList<ValidationResult>>(errors);
        }

        if (terms.Pricing == AgreementPricing.PerVisit && terms.PricePerVisit <= 0)
        {
            errors.Add(new ValidationResult(S["Enter the price of a visit."], [nameof(WorkOrderAgreementTerms.PricePerVisit)]));
        }

        foreach (var schedule in terms.Schedules ?? [])
        {
            var name = string.IsNullOrWhiteSpace(schedule.Name) ? S["a schedule"].Value : schedule.Name;

            if (string.IsNullOrEmpty(schedule.TypeId))
            {
                errors.Add(new ValidationResult(S["Choose the work order type of {0}.", name], [nameof(WorkOrderAgreementTerms.Schedules)]));
            }

            if (!RecurrenceRule.TryParse(schedule.Rule, out _, out var error))
            {
                errors.Add(new ValidationResult(S["When {0} repeats isn't a valid rule: {1}", name, error], [nameof(WorkOrderAgreementTerms.Schedules)]));
            }

            if (schedule.LeadDays is < 0 or > 365)
            {
                errors.Add(new ValidationResult(S["{0} is made between 0 and 365 days ahead.", name], [nameof(WorkOrderAgreementTerms.Schedules)]));
            }
        }

        return Task.FromResult<IReadOnlyList<ValidationResult>>(errors);
    }
}
