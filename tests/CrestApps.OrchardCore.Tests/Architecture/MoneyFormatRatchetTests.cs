using System.Text;
using System.Text.RegularExpressions;

namespace CrestApps.OrchardCore.Tests.Architecture;

/// <summary>
/// DRY-11: a fixed two-decimal format shows a yen price as 1200.00 and a dinar price with too few places. Money is shown
/// with <c>CurrencyScale.Format</c> or <c>ProductPriceFormatter</c>, which know each currency's places. Machine formats
/// (feeds, structured data, CSV for another system) are listed here; the list may only shrink.
/// </summary>
public sealed partial class MoneyFormatRatchetTests
{
    private static readonly string[] _commerceFolders =
    [
        "src/Core/CrestApps.OrchardCore.Carts.Core",
        "src/Core/CrestApps.OrchardCore.Checkout.Core",
        "src/Core/CrestApps.OrchardCore.Commerce.Core",
        "src/Core/CrestApps.OrchardCore.Customers.Core",
        "src/Core/CrestApps.OrchardCore.Inventory.Core",
        "src/Core/CrestApps.OrchardCore.Orders.Core",
        "src/Core/CrestApps.OrchardCore.Products.Core",
        "src/Core/CrestApps.OrchardCore.Shipping.Core",
        "src/Core/CrestApps.OrchardCore.Subscriptions.Core",
        "src/Core/CrestApps.OrchardCore.Taxation.Core",
        "src/Core/CrestApps.OrchardCore.Transactions.Core",
        "src/Modules/CrestApps.OrchardCore.Accounts.B2B",
        "src/Modules/CrestApps.OrchardCore.AgenticCommerce",
        "src/Modules/CrestApps.OrchardCore.Bookings",
        "src/Modules/CrestApps.OrchardCore.Carts",
        "src/Modules/CrestApps.OrchardCore.Checkout",
        "src/Modules/CrestApps.OrchardCore.Commerce",
        "src/Modules/CrestApps.OrchardCore.Customers",
        "src/Modules/CrestApps.OrchardCore.Downloads",
        "src/Modules/CrestApps.OrchardCore.Fundraising",
        "src/Modules/CrestApps.OrchardCore.Inventory",
        "src/Modules/CrestApps.OrchardCore.Marketplace",
        "src/Modules/CrestApps.OrchardCore.Markets",
        "src/Modules/CrestApps.OrchardCore.Memberships",
        "src/Modules/CrestApps.OrchardCore.Orders",
        "src/Modules/CrestApps.OrchardCore.PayLater",
        "src/Modules/CrestApps.OrchardCore.PointOfSale",
        "src/Modules/CrestApps.OrchardCore.Products",
        "src/Modules/CrestApps.OrchardCore.Receipts",
        "src/Modules/CrestApps.OrchardCore.Shipping",
        "src/Modules/CrestApps.OrchardCore.Stripe",
        "src/Modules/CrestApps.OrchardCore.Subscriptions",
        "src/Modules/CrestApps.OrchardCore.Taxation",
        "src/Modules/CrestApps.OrchardCore.Transactions",
    ];

    private static readonly Dictionary<string, int> _known = new(StringComparer.OrdinalIgnoreCase)
    {
        // Machine formats: files and markup read by another system.
        ["src/Modules/CrestApps.OrchardCore.Orders/Services/OrderCsv.cs"] = 1,
        ["src/Modules/CrestApps.OrchardCore.Products/ContentTransfer/ProductPartContentImportHandler.cs"] = 1,
        ["src/Modules/CrestApps.OrchardCore.Products/ContentTransfer/StoreExportReaderTransformer.cs"] = 1,
        ["src/Modules/CrestApps.OrchardCore.Products/Feeds/ProductFeedWriter.cs"] = 1,
        ["src/Modules/CrestApps.OrchardCore.Products/StructuredData/ProductStructuredDataProvider.cs"] = 1,
        ["src/Modules/CrestApps.OrchardCore.Subscriptions/Reports/SubscriptionReportBase.cs"] = 1,

        // Shown to people, to move to CurrencyScale.Format (DRY-11).
        ["src/Modules/CrestApps.OrchardCore.Orders/Views/DraftOrdersAdmin/Edit.cshtml"] = 1,
        ["src/Modules/CrestApps.OrchardCore.Orders/Views/OrdersAdmin/_OrderActions.cshtml"] = 1,
        ["src/Modules/CrestApps.OrchardCore.PayLater/Drivers/OrderOfflinePaymentDisplayDriver.cs"] = 1,
        ["src/Modules/CrestApps.OrchardCore.Products/Views/ProductPart.Edit.cshtml"] = 1,
        ["src/Modules/CrestApps.OrchardCore.Shipping/Services/ShippingSetupStep.cs"] = 1,
        ["src/Modules/CrestApps.OrchardCore.Shipping/Views/ShippingZones/Rates.cshtml"] = 1,
        ["src/Modules/CrestApps.OrchardCore.Taxation/Views/Items/TaxRule.DefaultTags.SummaryAdmin.cshtml"] = 1,
    };

    [Fact]
    public void NoNewFixedTwoDecimalMoneyFormats()
    {
        var root = FindRepositoryRoot();
        var actual = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var folder in _commerceFolders)
        {
            var path = Path.Combine(root, folder);

            if (!Directory.Exists(path))
            {
                continue;
            }

            foreach (var file in SourceTreeFiles.Enumerate(path, "*.*", SourceTreeFiles.IsBuildOutputDirectory))
            {
                if (!file.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) && !file.EndsWith(".cshtml", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var count = FixedTwoDecimals().Count(File.ReadAllText(file));

                if (count > 0)
                {
                    actual[Path.GetRelativePath(root, file).Replace('\\', '/')] = count;
                }
            }
        }

        var problems = new List<string>();

        foreach (var (file, count) in actual)
        {
            if (count > _known.GetValueOrDefault(file))
            {
                problems.Add($"{file} formats money with a fixed two decimals {count} time(s). Use CurrencyScale.Format or ProductPriceFormatter.");
            }
        }

        foreach (var (file, known) in _known)
        {
            if (actual.GetValueOrDefault(file) < known)
            {
                problems.Add($"{file} has fewer fixed formats now; lower or remove its entry.");
            }
        }

        Assert.True(problems.Count == 0, new StringBuilder().AppendLine().AppendJoin(Environment.NewLine, problems.Order(StringComparer.Ordinal)).ToString());
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "src")) &&
                Directory.Exists(Path.Combine(directory.FullName, "tests", "CrestApps.OrchardCore.Tests")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("The repository root could not be found.");
    }

    [GeneratedRegex(@"""(F2|N2|0\.00|#,##0\.00)""|:F2\}|:N2\}|:0\.00\}", RegexOptions.CultureInvariant)]
    private static partial Regex FixedTwoDecimals();
}
