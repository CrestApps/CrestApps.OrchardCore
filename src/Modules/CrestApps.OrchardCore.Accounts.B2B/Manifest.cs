using CrestApps.OrchardCore;
using CrestApps.OrchardCore.Accounts;
using CrestApps.OrchardCore.Accounts.B2B;
using CrestApps.OrchardCore.Commerce;
using CrestApps.OrchardCore.Orders.Core;
using CrestApps.OrchardCore.Transactions;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Accounts B2B",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version
)]

[assembly: Feature(
    Id = B2BAccountsConstants.Feature,
    Name = "Accounts B2B",
    Description = "Lets an account buy as a business: several buyers and their roles, shared locations, payment terms with a deposit and net days, purchase order numbers, quotes, price lists, approvals and budgets for buyers' orders, and punchout.",
    Category = "Commerce",
    Dependencies =
    [
        // The business is an account: the B2B part is added to the Account type. Its buyers are the account's members,
        // and the places it ships to are the account's locations.
        AccountsConstants.Feature,
        AccountsConstants.MembersFeature,
        AccountsConstants.LocationsFeature,
        CommerceConstants.Features.Area,
        OrderConstants.Features.Orders,

        // An account's terms are owed as transactions: what its deposit doesn't cover goes on its account in the ledger.
        TransactionsConstants.Features.Area,
    ]
)]
