using System.Linq.Expressions;
using CrestApps.OrchardCore.Customers.Core;
using CrestApps.OrchardCore.Customers.Core.Indexes;
using CrestApps.OrchardCore.Customers.Core.Models;
using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Reports.Designer.DataSources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using OrchardCore.DataSources;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Customers.Reports.DataSources;

/// <summary>
/// One row per customer, guests and anonymized customers included: who they are, how to reach them, whether they have
/// an account, and when they became a customer and were last active. The personal details are those the customer's
/// admin page shows, and an anonymized customer has none. Tax ids, the reason for a tax exemption, the outside services'
/// ids, the street lines of addresses and the privacy log are never exposed.
/// </summary>
public sealed class CustomersReportDataSet : IndexedReportDataSet<Customer, CustomerIndex>, ICustomerReportDataSet
{
    /// <summary>
    /// The name of the field that holds when the customer was added, the data set's main date.
    /// </summary>
    public const string CreatedUtcField = "CreatedUtc";

    /// <summary>
    /// Initializes a new instance of the <see cref="CustomersReportDataSet"/> class.
    /// </summary>
    /// <param name="session">The YesSql session customers are read from.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public CustomersReportDataSet(
        ISession session,
        IAuthorizationService authorizationService,
        IStringLocalizer<CustomersReportDataSet> stringLocalizer)
        : base(
            new DataSetDescriptor(
                CommerceReportDataSets.CustomerSource.Customers,
                stringLocalizer["Customers"],
                stringLocalizer["One row per customer, guests included: name, contact details, kind, status, account, default addresses and dates."]),
            session,
            authorizationService,
            CustomersConstants.CollectionName,
            CustomerPermissions.ViewCustomers)
    {
        var S = stringLocalizer;
        var customer = S["Customer"].Value;
        var contact = S["Contact"].Value;
        var addresses = S["Addresses"].Value;
        var dates = S["Dates"].Value;
        var customers = CommerceReportDataSets.Reference(CommerceReportDataSets.CustomerSource.Name, CommerceReportDataSets.CustomerSource.Customers);

        AddField(CommerceReportDataSets.ItemIdField, S["Customer ID"], DataFieldType.Text, record => record.ItemId, customer).Identifier();
        AddField("Name", S["Name"], DataFieldType.Text, record => record.GetName(), customer).Description = S["The name the store shows for the customer: their display name, their name or company, or their email."].Value;
        AddField(nameof(Customer.FirstName), S["First name"], DataFieldType.Text, record => record.FirstName, customer);
        AddField(nameof(Customer.LastName), S["Last name"], DataFieldType.Text, record => record.LastName, customer);
        AddField(nameof(Customer.CompanyName), S["Company"], DataFieldType.Text, record => record.CompanyName, customer);
        AddField(nameof(Customer.Kind), S["Kind"], DataFieldType.Text, record => record.Kind, customer);
        AddField(nameof(Customer.Status), S["Status"], DataFieldType.Text, record => record.Status, customer);
        AddField(nameof(CustomerIndex.HasAccount), S["Has an account"], DataFieldType.Boolean, record => !string.IsNullOrEmpty(record.UserId), customer);
        AddField(nameof(Customer.UserId), S["User ID"], DataFieldType.Text, record => record.UserId, customer).Identifier(ReportDataReferences.User());
        AddField(nameof(Customer.IsTaxExempt), S["Exempt from tax"], DataFieldType.Boolean, record => record.IsTaxExempt, customer);
        AddField("IsAnonymized", S["Anonymized"], DataFieldType.Boolean, record => record.AnonymizedUtc.HasValue, customer);
        AddField(nameof(Customer.MergedIntoCustomerId), S["Merged into customer ID"], DataFieldType.Text, record => record.MergedIntoCustomerId, customer).Identifier(customers);
        AddField("MergedCustomerCount", S["Merged guests"], DataFieldType.Integer, record => record.MergedCustomerIds?.Count ?? 0, customer);

        AddField(nameof(Customer.Email), S["Email"], DataFieldType.Text, record => record.Email, contact);
        AddField(nameof(Customer.Phone), S["Phone"], DataFieldType.Text, record => record.Phone, contact);
        AddField(nameof(Customer.AcceptsMarketing), S["Accepts marketing"], DataFieldType.Boolean, record => record.AcceptsMarketing, contact);

        AddField("AddressCount", S["Addresses"], DataFieldType.Integer, record => record.Addresses?.Count ?? 0, addresses);
        AddField("BillingCountry", S["Billing country"], DataFieldType.Text, record => record.GetDefaultBillingAddress()?.Address?.Country, addresses);
        AddField("BillingRegion", S["Billing region"], DataFieldType.Text, record => record.GetDefaultBillingAddress()?.Address?.Region, addresses);
        AddField("BillingCity", S["Billing city"], DataFieldType.Text, record => record.GetDefaultBillingAddress()?.Address?.City, addresses);
        AddField("BillingPostalCode", S["Billing postal code"], DataFieldType.Text, record => record.GetDefaultBillingAddress()?.Address?.PostalCode, addresses);
        AddField("ShippingCountry", S["Shipping country"], DataFieldType.Text, record => record.GetDefaultShippingAddress()?.Address?.Country, addresses);
        AddField("ShippingRegion", S["Shipping region"], DataFieldType.Text, record => record.GetDefaultShippingAddress()?.Address?.Region, addresses);
        AddField("ShippingCity", S["Shipping city"], DataFieldType.Text, record => record.GetDefaultShippingAddress()?.Address?.City, addresses);
        AddField("ShippingPostalCode", S["Shipping postal code"], DataFieldType.Text, record => record.GetDefaultShippingAddress()?.Address?.PostalCode, addresses);

        AddField(CreatedUtcField, S["Customer since"], DataFieldType.DateTime, record => record.CreatedUtc, dates);
        AddField(nameof(Customer.LastActivityUtc), S["Last activity"], DataFieldType.DateTime, record => record.LastActivityUtc, dates);
        AddField(nameof(Customer.ModifiedUtc), S["Last changed"], DataFieldType.DateTime, record => record.ModifiedUtc, dates);
        AddField(nameof(Customer.MergedUtc), S["Merged"], DataFieldType.DateTime, record => record.MergedUtc, dates);
        AddField(nameof(Customer.AnonymizedUtc), S["Anonymized on"], DataFieldType.DateTime, record => record.AnonymizedUtc, dates);

        CompleteDescription();
    }

    /// <inheritdoc/>
    protected override (string Field, Expression<Func<CustomerIndex, DateTime>> Column)? DateColumn
        => (CreatedUtcField, index => index.CreatedUtc);

    /// <inheritdoc/>
    protected override IReadOnlyDictionary<string, Expression<Func<CustomerIndex, string>>> KeyColumns =>
        new Dictionary<string, Expression<Func<CustomerIndex, string>>>(StringComparer.Ordinal)
        {
            [CommerceReportDataSets.ItemIdField] = index => index.ItemId,
            [nameof(Customer.UserId)] = index => index.UserId,
        };

    /// <inheritdoc/>
    protected override void DescribeColumns(ReportIndexConditions<CustomerIndex> columns)
    {
        columns
            .Enum(nameof(Customer.Status), index => index.Status)
            .Enum(nameof(Customer.Kind), index => index.Kind);
    }
}
