using System.ComponentModel.DataAnnotations;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Core.Handlers;
using CrestApps.OrchardCore.Transactions.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Transactions.Handlers;

/// <summary>
/// Stamps who created and changed a payment term and when, reads it from recipes, and checks it before it is saved,
/// whether it comes from the admin, a recipe or code.
/// </summary>
internal sealed class PaymentTermHandler : AuditedCatalogEntryHandlerBase<PaymentTerm>
{
    internal readonly IStringLocalizer S;

    public PaymentTermHandler(
        IHttpContextAccessor httpContextAccessor,
        IClock clock,
        IStringLocalizer<PaymentTermHandler> stringLocalizer)
        : base(httpContextAccessor, clock)
    {
        S = stringLocalizer;
    }

    public override Task ValidatingAsync(ValidatingContext<PaymentTerm> context, CancellationToken cancellationToken = default)
    {
        var term = context.Model;

        if (string.IsNullOrWhiteSpace(term.Name))
        {
            context.Result.Fail(new ValidationResult(S["Name is required."], [nameof(PaymentTerm.Name)]));
        }

        if (!Enum.IsDefined(term.Kind))
        {
            context.Result.Fail(new ValidationResult(S["Choose when the payment is due."], [nameof(PaymentTerm.Kind)]));
        }

        if (term.Kind is PaymentTermKind.NetDays or PaymentTermKind.EndOfMonth && (term.Days < 0 || term.Days > 3650))
        {
            context.Result.Fail(new ValidationResult(S["The number of days must be from 0 to 3650."], [nameof(PaymentTerm.Days)]));
        }

        if (term.Kind == PaymentTermKind.DayOfNextMonth && (term.DayOfMonth < 1 || term.DayOfMonth > 31))
        {
            context.Result.Fail(new ValidationResult(S["The day of the month must be from 1 to 31."], [nameof(PaymentTerm.DayOfMonth)]));
        }

        if (term.DiscountPercent is < 0m or >= 100m)
        {
            context.Result.Fail(new ValidationResult(S["The discount must be at least 0 and less than 100 percent."], [nameof(PaymentTerm.DiscountPercent)]));
        }

        if (term.DiscountPercent is > 0m && term.DiscountDays is not > 0)
        {
            context.Result.Fail(new ValidationResult(S["Enter for how many days the discount is offered."], [nameof(PaymentTerm.DiscountDays)]));
        }

        return Task.CompletedTask;
    }
}
