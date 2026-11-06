using System.Text.Json;
using CrestApps.OrchardCore.Customers.Core.Models;
using CrestApps.OrchardCore.Customers.Core.Services;
using CrestApps.OrchardCore.PersonalData;

namespace CrestApps.OrchardCore.Customers.Handlers;

/// <summary>
/// Erases and exports the personal data every other module keeps about a customer, through
/// <see cref="IPersonalDataService"/>: their commerce records (checkouts, transactions, payments, refunds,
/// subscriptions, installment plans, coupon redemptions, payment-provider customers and the verticals' records), and
/// the records of every other module with a personal-data contributor, such as the CRM, Workforce and Scheduling. The
/// guest retention expiry only reaches the contributors that take part in it, which are the commerce records.
/// </summary>
/// <remarks>
/// The customer is identified by their account, when they have one, by their email, which is what ties their guest
/// checkouts to them, and by their phone number. Names are cleared rather than replaced, the same way the customer
/// record itself is. It runs before the other handlers, because those erase the very checkout emails and owners it
/// follows to the person's guest transactions, subscriptions and installment plans.
/// </remarks>
public sealed class PersonalDataCustomerPrivacyHandler : ICustomerPrivacyHandler
{
    /// <summary>
    /// The key the personal data is exported under.
    /// </summary>
    public const string ExportKey = "personalData";

    private static readonly JsonSerializerOptions _options = JsonSerializerOptions.Web;

    private readonly IPersonalDataService _personalDataService;

    /// <summary>
    /// Initializes a new instance of the <see cref="PersonalDataCustomerPrivacyHandler"/> class.
    /// </summary>
    /// <param name="personalDataService">The personal data service.</param>
    public PersonalDataCustomerPrivacyHandler(IPersonalDataService personalDataService)
    {
        _personalDataService = personalDataService;
    }

    /// <inheritdoc/>
    public int Order => -100;

    /// <inheritdoc/>
    public async Task ErasingAsync(CustomerPrivacyContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var subject = CreateSubject(context);

        if (subject is null)
        {
            return;
        }

        // The guest retention expiry reaches only the commerce records, as it always has; the CRM, Workforce, Scheduling
        // and the other modules' records are only erased when an admin erases the customer.
        if (context.Action == CustomerPrivacyAction.Expired)
        {
            await _personalDataService.ExpireAsync(subject, cancellationToken);

            return;
        }

        await _personalDataService.AnonymizeAsync(subject, replacementText: null, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task ExportingAsync(CustomerExportContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var subject = CreateSubject(context);

        if (subject is null)
        {
            return;
        }

        var export = await _personalDataService.ExportAsync(subject, cancellationToken);

        context.Export[ExportKey] = JsonSerializer.SerializeToNode(export, _options);
    }

    private static PersonalDataSubject CreateSubject(CustomerPrivacyContext context)
    {
        var customer = context.Customer;

        if (string.IsNullOrWhiteSpace(context.UserId) &&
            string.IsNullOrWhiteSpace(customer.Email) &&
            string.IsNullOrWhiteSpace(customer.Phone))
        {
            return null;
        }

        var subject = new PersonalDataSubject(context.UserId, customer.Email, customer.Phone);

        subject.AddName(customer.DisplayName);

        if (!string.IsNullOrWhiteSpace(customer.FirstName) && !string.IsNullOrWhiteSpace(customer.LastName))
        {
            subject.AddName($"{customer.FirstName} {customer.LastName}");
        }

        return subject;
    }
}
