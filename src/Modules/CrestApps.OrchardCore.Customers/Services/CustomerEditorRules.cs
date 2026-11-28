using System.ComponentModel.DataAnnotations;
using CrestApps.OrchardCore.Customers.Core.Models;
using CrestApps.OrchardCore.Customers.Core.Services;
using CrestApps.OrchardCore.Customers.ViewModels;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;

namespace CrestApps.OrchardCore.Customers.Services;

/// <summary>
/// The rules of the customer pages, shared by the Customers admin pages and the AI tools: what the list's filters
/// select, what a customer's form must hold and how it is saved, and the history the customer's page shows. Each caller
/// words the problems itself.
/// </summary>
internal static class CustomerEditorRules
{
    /// <summary>
    /// How many recent orders a customer's page shows.
    /// </summary>
    public const int RecentOrderCount = 20;

    private static readonly EmailAddressAttribute _emailAddress = new();

    /// <summary>
    /// Returns the query of a filter of the customer list.
    /// </summary>
    /// <param name="filter">The filter.</param>
    /// <param name="search">The text the name, company or email starts with, or none.</param>
    /// <returns>The query.</returns>
    public static CustomerQuery ToQuery(CustomerListFilter filter, string search)
    {
        var query = new CustomerQuery { Search = search };

        switch (filter)
        {
            case CustomerListFilter.Active:
                query.Status = CustomerStatus.Active;
                break;
            case CustomerListFilter.WithAccount:
                query.Status = CustomerStatus.Active;
                query.HasAccount = true;
                break;
            case CustomerListFilter.Guests:
                query.Status = CustomerStatus.Active;
                query.HasAccount = false;
                break;
            case CustomerListFilter.Businesses:
                query.Status = CustomerStatus.Active;
                query.Kind = CustomerKind.Business;
                break;
            case CustomerListFilter.Disabled:
                query.Status = CustomerStatus.Disabled;
                break;
        }

        return query;
    }

    /// <summary>
    /// Returns what is wrong with a customer's form: an invalid email for a guest, a business without a company name, a
    /// customer who could not be found again, or a status of anonymized.
    /// </summary>
    /// <param name="model">The form.</param>
    /// <param name="customer">The customer it is for.</param>
    /// <returns>The problems, or an empty list.</returns>
    public static IReadOnlyList<CustomerEditProblem> Check(CustomerEditViewModel model, Customer customer)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(customer);

        var problems = new List<CustomerEditProblem>();

        // A customer with an account takes the account's email, so it is not edited here.
        var email = string.IsNullOrEmpty(customer.UserId) ? model.Email?.Trim() : customer.Email;

        if (string.IsNullOrEmpty(customer.UserId) && !string.IsNullOrEmpty(email) && !_emailAddress.IsValid(email))
        {
            problems.Add(CustomerEditProblem.InvalidEmail);
        }

        if (model.Kind == CustomerKind.Business && string.IsNullOrWhiteSpace(model.CompanyName))
        {
            problems.Add(CustomerEditProblem.CompanyNameRequired);
        }

        if (string.IsNullOrWhiteSpace(model.DisplayName) &&
            string.IsNullOrWhiteSpace(model.FirstName) &&
            string.IsNullOrWhiteSpace(model.LastName) &&
            string.IsNullOrWhiteSpace(model.CompanyName) &&
            string.IsNullOrEmpty(email))
        {
            problems.Add(CustomerEditProblem.NameRequired);
        }

        if (model.Status == CustomerStatus.Anonymized)
        {
            problems.Add(CustomerEditProblem.AnonymizedStatus);
        }

        return problems;
    }

    /// <summary>
    /// Copies a checked form onto the customer. The email changes only for a guest.
    /// </summary>
    /// <param name="model">The form.</param>
    /// <param name="customer">The customer.</param>
    /// <param name="now">The current time.</param>
    public static void Apply(CustomerEditViewModel model, Customer customer, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(customer);

        customer.Kind = model.Kind;
        customer.Status = model.Status;
        customer.DisplayName = Trim(model.DisplayName);
        customer.FirstName = Trim(model.FirstName);
        customer.LastName = Trim(model.LastName);
        customer.CompanyName = Trim(model.CompanyName);
        customer.TaxId = Trim(model.TaxId);
        customer.IsTaxExempt = model.IsTaxExempt;
        customer.TaxExemptionReason = model.IsTaxExempt ? Trim(model.TaxExemptionReason) : null;
        customer.Phone = Trim(model.Phone);
        customer.AcceptsMarketing = model.AcceptsMarketing;

        if (string.IsNullOrEmpty(customer.UserId))
        {
            customer.Email = Trim(model.Email);
        }

        customer.ModifiedUtc = now;
    }

    /// <summary>
    /// Fills a customer's form with what is stored.
    /// </summary>
    /// <param name="customer">The customer.</param>
    /// <returns>The form.</returns>
    public static CustomerEditViewModel ToViewModel(Customer customer)
    {
        ArgumentNullException.ThrowIfNull(customer);

        return new()
        {
            CustomerId = customer.ItemId,
            UserId = customer.UserId,
            CreatedUtc = customer.CreatedUtc,
            LastActivityUtc = customer.LastActivityUtc,
            Kind = customer.Kind,
            Status = customer.Status,
            DisplayName = customer.DisplayName,
            FirstName = customer.FirstName,
            LastName = customer.LastName,
            CompanyName = customer.CompanyName,
            TaxId = customer.TaxId,
            IsTaxExempt = customer.IsTaxExempt,
            TaxExemptionReason = customer.TaxExemptionReason,
            Email = customer.Email,
            Phone = customer.Phone,
            AcceptsMarketing = customer.AcceptsMarketing,
        };
    }

    /// <summary>
    /// Returns whether a customer's address book can be changed: an anonymized customer keeps nothing personal, and a
    /// merged guest's addresses moved to the customer it was merged into.
    /// </summary>
    /// <param name="customer">The customer.</param>
    /// <returns><see langword="true"/> when the address book can be changed.</returns>
    public static bool IsEditable(Customer customer)
        => customer is not null && customer.Status is not (CustomerStatus.Anonymized or CustomerStatus.Merged);

    /// <summary>
    /// Reads what a customer's page shows of their history: recent paid and unpaid orders, when Orders is on (an account
    /// holder's include those from before the customer record existed), the customer a merged guest moved to, and the
    /// guests merged into this customer.
    /// </summary>
    /// <param name="customer">The customer.</param>
    /// <param name="store">The customer store.</param>
    /// <param name="orders">The order store, or <see langword="null"/> when Orders is off.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The history.</returns>
    public static async Task<CustomerHistory> GetHistoryAsync(Customer customer, ICustomerStore store, IOrderStore orders, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(customer);
        ArgumentNullException.ThrowIfNull(store);

        var history = new CustomerHistory();

        if (orders is not null)
        {
            var (count, recent) = await orders.PageAsync(new OrderQuery
            {
                UserId = customer.UserId,
                CustomerId = string.IsNullOrEmpty(customer.UserId) ? customer.ItemId : null,
                Statuses = [OrderStatus.Paid, OrderStatus.AwaitingPayment],
            }, 0, RecentOrderCount, cancellationToken);

            history.HasOrders = true;
            history.OrderCount = count;
            history.Orders = recent;
        }

        if (!string.IsNullOrEmpty(customer.MergedIntoCustomerId) && await store.FindByIdAsync(customer.MergedIntoCustomerId, cancellationToken) is { } into)
        {
            history.MergedInto = into;
            history.MergedUtc = customer.MergedUtc;
        }

        var merged = new List<Customer>();

        foreach (var mergedId in customer.MergedCustomerIds ?? [])
        {
            if (await store.FindByIdAsync(mergedId, cancellationToken) is { } guest)
            {
                merged.Add(guest);
            }
        }

        history.MergedCustomers = merged;

        return history;
    }

    private static string Trim(string value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>
/// What a customer's page shows of their history.
/// </summary>
internal sealed class CustomerHistory
{
    /// <summary>
    /// Gets or sets whether orders are kept (the Orders feature is on).
    /// </summary>
    public bool HasOrders { get; set; }

    /// <summary>
    /// Gets or sets how many paid and unpaid orders the customer has.
    /// </summary>
    public int OrderCount { get; set; }

    /// <summary>
    /// Gets or sets the most recent of them.
    /// </summary>
    public IReadOnlyList<Order> Orders { get; set; } = [];

    /// <summary>
    /// Gets or sets the customer this merged guest moved to.
    /// </summary>
    public Customer MergedInto { get; set; }

    public DateTime? MergedUtc { get; set; }

    /// <summary>
    /// Gets or sets the guests merged into this customer.
    /// </summary>
    public IReadOnlyList<Customer> MergedCustomers { get; set; } = [];
}

/// <summary>
/// What can be wrong with a customer's form.
/// </summary>
internal enum CustomerEditProblem
{
    InvalidEmail,
    CompanyNameRequired,
    NameRequired,
    AnonymizedStatus,
}
