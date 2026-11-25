using CrestApps.Core.AI;
using CrestApps.OrchardCore.Customers.AI;
using CrestApps.OrchardCore.Customers.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Customers;

/// <summary>
/// Registers the customer AI tools whenever the Customers and AI features are on. Reading needs <c>ViewCustomers</c> and
/// changing needs <c>ManageCustomers</c>, as on the Customers pages; deleting an address and the data access and erasure
/// requests ask for approval.
/// </summary>
[RequireFeatures(AIFeatureIds.Area)]
public sealed class AIToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIToolsStartup(IStringLocalizer<AIToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<ListCustomersTool>(ListCustomersTool.TheName)
            .WithTitle(S["List Customers"])
            .WithDescription(S["Finds customers by the start of their name, company or email, with filters for status, guests or account holders and businesses, and paging. Returns id, name, kind, status, email and last activity."])
            .WithCategory(S["Customers"])
            .Selectable();

        services.AddCoreAITool<GetCustomerTool>(GetCustomerTool.TheName)
            .WithTitle(S["Get Customer"])
            .WithDescription(S["Returns one customer's profile, addresses, recent orders, merge history, privacy log and, when those features are on, store credit balances and loyalty points."])
            .WithCategory(S["Customers"])
            .Selectable();

        services.AddCoreAITool<SaveCustomerTool>(SaveCustomerTool.TheName)
            .WithTitle(S["Save Customer"])
            .WithDescription(S["Creates a person as a customer or updates one: status (Active or Disabled), names, tax exemption, phone, marketing consent, and email for guests only. Only the fields given change; an anonymized customer cannot be edited. It does not create or change businesses: a business is an Account (use listAccounts and getAccount), and a customer stored as a business is changed on its admin page."])
            .WithCategory(S["Customers"])
            .Selectable();

        services.AddCoreAITool<SaveCustomerAddressTool>(SaveCustomerAddressTool.TheName)
            .WithTitle(S["Save Customer Address"])
            .WithDescription(S["Adds an address to a customer's address book or updates one by id, after checking it against the country's rules; can mark it as the default billing or shipping address."])
            .WithCategory(S["Customers"])
            .Selectable();

        services.AddCoreAITool<DeleteCustomerAddressTool>(DeleteCustomerAddressTool.TheName)
            .WithTitle(S["Delete Customer Address"])
            .WithDescription(S["Deletes one address from a customer's address book."])
            .WithCategory(S["Customers"])
            .AsDestructive()
            .Selectable();

        services.AddCoreAITool<ExportCustomerDataTool>(ExportCustomerDataTool.TheName)
            .WithTitle(S["Export Customer Data"])
            .WithDescription(S["Returns the link that downloads everything the store holds on a customer, for a data access request. Opening the link builds the file, checks the permission again and records the export in the customer's privacy log; the data never passes through the conversation."])
            .WithCategory(S["Customers"])
            .AsSecurity()
            .Selectable();

        services.AddCoreAITool<EraseCustomerDataTool>(EraseCustomerDataTool.TheName)
            .WithTitle(S["Erase Customer Data"])
            .WithDescription(S["Erases a customer's personal data for an erasure request, everywhere the site keeps it: every module that holds personal data about the customer (orders, and any other module's records) erases its part too, orders keep their totals without the personal details, and the customer record is anonymized. This cannot be undone."])
            .WithCategory(S["Customers"])
            .AsDestructive()
            .Selectable();
    }
}

/// <summary>
/// Registers the store credit AI tool whenever the Store Credit and AI features are on. It needs <c>ManageCustomers</c>
/// and asks for approval, because store credit is money.
/// </summary>
[Feature(CustomersConstants.Features.StoreCredit)]
[RequireFeatures(AIFeatureIds.Area)]
public sealed class StoreCreditAIToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="StoreCreditAIToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public StoreCreditAIToolsStartup(IStringLocalizer<StoreCreditAIToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<AdjustCustomerStoreCreditTool>(AdjustCustomerStoreCreditTool.TheName)
            .WithTitle(S["Adjust Customer Store Credit"])
            .WithDescription(S["Adds or removes store credit for a customer in a currency, with a reason shown in the credit history. A positive amount is money the store gives away."])
            .WithCategory(S["Customers"])
            .AsExternal()
            .Selectable();
    }
}

/// <summary>
/// Registers the loyalty points AI tool whenever the Loyalty and AI features are on. It needs <c>ManageCustomers</c> and
/// asks for approval, because points are spent as money.
/// </summary>
[Feature(CustomersConstants.Features.Loyalty)]
[RequireFeatures(AIFeatureIds.Area)]
public sealed class LoyaltyAIToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="LoyaltyAIToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public LoyaltyAIToolsStartup(IStringLocalizer<LoyaltyAIToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<AdjustCustomerLoyaltyPointsTool>(AdjustCustomerLoyaltyPointsTool.TheName)
            .WithTitle(S["Adjust Customer Loyalty Points"])
            .WithDescription(S["Adds or removes loyalty points for a customer, with a reason shown in the points history."])
            .WithCategory(S["Customers"])
            .AsExternal()
            .Selectable();
    }
}
