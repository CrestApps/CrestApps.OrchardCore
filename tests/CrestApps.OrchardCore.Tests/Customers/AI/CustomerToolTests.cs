using CrestApps.OrchardCore.Addresses.Core;
using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Customers;
using CrestApps.OrchardCore.Customers.AI;
using CrestApps.OrchardCore.Customers.Core.Models;
using CrestApps.OrchardCore.Customers.Core.Services;
using CrestApps.OrchardCore.Customers.Models;
using CrestApps.OrchardCore.Customers.Services;
using CrestApps.OrchardCore.Tests.Agent;
using CrestApps.OrchardCore.Tests.Agent.AIModels;
using CrestApps.OrchardCore.Tests.Doubles;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;
using OrchardCore.DisplayManagement;
using OrchardCore.Locking;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Tests.Customers.AI;

/// <summary>
/// The customer tools check the Customers pages' permissions first, apply the customer form's, the address book's and the
/// balance cards' rules, record who changed store credit and points, never put a customer's exported data in the
/// conversation, and erase through the privacy service.
/// </summary>
public sealed class CustomerToolTests
{
    private static readonly DateTime _now = new(2026, 11, 18, 12, 0, 0, DateTimeKind.Utc);

    private readonly InMemoryCustomerStore _customers = new();
    private readonly List<StoreCreditAccount> _credit = [];
    private readonly List<LoyaltyAccount> _points = [];
    private readonly List<string> _erased = [];

    public CustomerToolTests()
    {
        var dana = new Customer
        {
            ItemId = "c-dana",
            UserId = "u-dana",
            FirstName = "Dana",
            LastName = "Reyes",
            Email = "dana@example.com",
            Phone = "+15555550100",
            CreatedUtc = _now.AddDays(-30),
            LastActivityUtc = _now.AddDays(-1),
            PrivacyLog = [new CustomerPrivacyEntry { Utc = _now.AddDays(-2), Action = CustomerPrivacyAction.Exported, By = "admin" }],
        };

        dana.Addresses.Add(new CustomerAddress
        {
            AddressId = "home",
            Label = "Home",
            Address = new Address { Name = "Dana Reyes", AddressLine1 = "1 Main St", City = "Springfield", Region = "IL", PostalCode = "62701", Country = "US", Phone = "+15555550100" },
        });
        dana.DefaultBillingAddressId = "home";
        dana.DefaultShippingAddressId = "home";

        _customers.Customers.Add(dana);
        _customers.Customers.Add(new Customer { ItemId = "c-guest", Email = "guest@example.com", FirstName = "Gus", LastActivityUtc = _now.AddDays(-3) });
        _customers.Customers.Add(new Customer { ItemId = "c-gone", Status = CustomerStatus.Anonymized, LastActivityUtc = _now.AddDays(-90) });
        _credit.Add(new StoreCreditAccount { ItemId = "credit-1", CustomerId = "c-dana", Currency = "USD", Balance = 10m });
    }

    public static TheoryData<string, string> ToolsAndPermissions => new()
    {
        { ListCustomersTool.TheName, "ViewCustomers" },
        { GetCustomerTool.TheName, "ViewCustomers" },
        { SaveCustomerTool.TheName, "ManageCustomers" },
        { SaveCustomerAddressTool.TheName, "ManageCustomers" },
        { DeleteCustomerAddressTool.TheName, "ManageCustomers" },
        { ExportCustomerDataTool.TheName, "ManageCustomers" },
        { EraseCustomerDataTool.TheName, "ManageCustomers" },
        { AdjustCustomerStoreCreditTool.TheName, "ManageCustomers" },
        { AdjustCustomerLoyaltyPointsTool.TheName, "ManageCustomers" },
    };

    [Theory]
    [MemberData(nameof(ToolsAndPermissions))]
    public async Task Tool_RefusesACallerWhoIsNotSignedIn(string toolName, string permission)
    {
        // Act
        var result = await InvokeAsync(toolName, Build(signedIn: false), Arguments(toolName));

        // Assert
        Assert.Equal(CustomersAIToolBase.NotAllowedMessage, result);
        Assert.NotNull(permission);
        Assert.Empty(_erased);
    }

    [Theory]
    [MemberData(nameof(ToolsAndPermissions))]
    public async Task Tool_RefusesACallerWithoutThePermission(string toolName, string permission)
    {
        // Arrange
        var services = Build(authorize: (asked, _) => asked.Name != permission);

        // Act
        var result = await InvokeAsync(toolName, services, Arguments(toolName));

        // Assert
        Assert.Equal(CustomersAIToolBase.NotAllowedMessage, result);
        Assert.Equal(permission, Assert.Single(services.GetRequiredService<FakeAuthorizationService>().Checks).Permission);
        Assert.Empty(_erased);
        Assert.Equal(10m, _credit[0].Balance);
    }

    [Fact]
    public async Task ListCustomers_FiltersAsTheListPage_AndRefusesAnUnknownFilter()
    {
        // Act
        var active = AgentToolTestContext.ReadJson(await InvokeAsync(ListCustomersTool.TheName, Build(), new()));
        var guests = AgentToolTestContext.ReadJson(await InvokeAsync(ListCustomersTool.TheName, Build(), new() { ["filter"] = "guests" }));
        var wrong = await InvokeAsync(ListCustomersTool.TheName, Build(), new() { ["filter"] = "Vip" });

        // Assert
        Assert.Equal(2, active.GetProperty("total").GetInt32());
        Assert.Equal("c-guest", Assert.Single(guests.GetProperty("items").EnumerateArray()).GetProperty("id").GetString());
        Assert.StartsWith("Nothing was listed: filter must be", wrong, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetCustomer_ReturnsTheProfile_Addresses_PrivacyLog_AndBalances()
    {
        // Act
        var json = AgentToolTestContext.ReadJson(await InvokeAsync(GetCustomerTool.TheName, Build(), new() { ["customerId"] = "c-dana" }));
        var missing = await InvokeAsync(GetCustomerTool.TheName, Build(), new() { ["customerId"] = "nobody" });

        // Assert
        var address = Assert.Single(json.GetProperty("addresses").EnumerateArray());

        Assert.Equal("Dana Reyes", json.GetProperty("name").GetString());
        Assert.True(address.GetProperty("isDefaultBilling").GetBoolean());
        Assert.Equal("Exported", Assert.Single(json.GetProperty("privacyLog").EnumerateArray()).GetProperty("action").GetString());
        Assert.Equal(10m, Assert.Single(json.GetProperty("storeCredit").EnumerateArray()).GetProperty("balance").GetDecimal());
        Assert.Equal(0, json.GetProperty("loyalty").GetProperty("points").GetInt32());
        Assert.StartsWith("Nothing was done: no customer has the id 'nobody'.", missing, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SaveCustomer_ChangesOnlyTheGivenFields_AndRefusesAnEmailForAnAccountHolder()
    {
        // Act
        var json = AgentToolTestContext.ReadJson(await InvokeAsync(SaveCustomerTool.TheName, Build(), new() { ["customerId"] = "c-dana", ["isTaxExempt"] = true, ["taxExemptionReason"] = "reseller certificate" }));
        var email = await InvokeAsync(SaveCustomerTool.TheName, Build(), new() { ["customerId"] = "c-dana", ["email"] = "other@example.com" });

        // Assert
        var dana = _customers.Customers[0];

        Assert.False(json.GetProperty("created").GetBoolean());
        Assert.True(dana.IsTaxExempt);
        Assert.Equal("reseller certificate", dana.TaxExemptionReason);
        Assert.Equal("Dana", dana.FirstName);
        Assert.Equal("+15555550100", dana.Phone);
        Assert.Equal("dana@example.com", dana.Email);
        Assert.StartsWith("Nothing was changed: the customer has an account", email, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SaveCustomer_CreatesAGuest_AndRefusesABusinessWithoutACompany_OrAnAnonymizedCustomer()
    {
        // Act
        var created = AgentToolTestContext.ReadJson(await InvokeAsync(SaveCustomerTool.TheName, Build(), new() { ["firstName"] = "Ana", ["email"] = "ana@example.com" }));
        var business = await InvokeAsync(SaveCustomerTool.TheName, Build(), new() { ["kind"] = "Business", ["email"] = "buyer@contoso.example" });
        var anonymized = await InvokeAsync(SaveCustomerTool.TheName, Build(), new() { ["customerId"] = "c-gone", ["firstName"] = "Back" });

        // Assert
        Assert.True(created.GetProperty("created").GetBoolean());
        Assert.Equal("ana@example.com", _customers.Customers.Last().Email);
        Assert.Equal("Nothing was saved: A business needs a company name.", business);
        Assert.StartsWith("Nothing was changed: the customer's personal data was erased", anonymized, StringComparison.Ordinal);
        Assert.Equal(4, _customers.Customers.Count);
    }

    [Fact]
    public async Task SaveCustomerAddress_AddsAnAddress_StartingWithTheCustomersName_AndRefusesOneItsCountryWouldNotAccept()
    {
        // Act
        var json = AgentToolTestContext.ReadJson(await InvokeAsync(SaveCustomerAddressTool.TheName, Build(), new()
        {
            ["customerId"] = "c-dana",
            ["label"] = "Office",
            ["address"] = new { addressLine1 = "500 Harbor Blvd", city = "Oakland", region = "CA", postalCode = "94607", country = "US" },
        }));
        var invalid = await InvokeAsync(SaveCustomerAddressTool.TheName, Build(), new()
        {
            ["customerId"] = "c-dana",
            ["address"] = new { addressLine1 = "500 Harbor Blvd", city = "Oakland", region = "CA", postalCode = "ABCDE", country = "US" },
        });
        var anonymized = await InvokeAsync(SaveCustomerAddressTool.TheName, Build(), new() { ["customerId"] = "c-gone", ["label"] = "Home" });

        // Assert
        var dana = _customers.Customers[0];
        var added = dana.FindAddress(json.GetProperty("addressId").GetString());

        Assert.Equal(2, dana.Addresses.Count);
        Assert.Equal("Dana Reyes", added.Address.Name);
        Assert.Equal("Office", added.Label);
        Assert.Equal("home", dana.DefaultBillingAddressId);
        Assert.StartsWith("Nothing was changed:", invalid, StringComparison.Ordinal);
        Assert.StartsWith("Nothing was changed: the customer was anonymized or merged", anonymized, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DeleteCustomerAddress_RemovesTheAddressAndItsDefaults_AndRefusesAnUnknownOne()
    {
        // Act
        var missing = await InvokeAsync(DeleteCustomerAddressTool.TheName, Build(), new() { ["customerId"] = "c-dana", ["addressId"] = "nowhere" });
        var json = AgentToolTestContext.ReadJson(await InvokeAsync(DeleteCustomerAddressTool.TheName, Build(), new() { ["customerId"] = "c-dana", ["addressId"] = "home" }));

        // Assert
        var dana = _customers.Customers[0];

        Assert.StartsWith("Nothing was deleted: the customer has no address with the id 'nowhere'.", missing, StringComparison.Ordinal);
        Assert.True(json.GetProperty("deleted").GetBoolean());
        Assert.Empty(dana.Addresses);
        Assert.Null(dana.DefaultBillingAddressId);
    }

    [Fact]
    public async Task ExportCustomerData_ReturnsTheExportLink_WithoutTheDataOrALogEntry()
    {
        // Act
        var result = await InvokeAsync(ExportCustomerDataTool.TheName, Build(), new() { ["customerId"] = "c-dana" });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);

        Assert.Equal("/Admin/customers/c-dana/export", json.GetProperty("downloadPath").GetString());
        Assert.False(json.GetProperty("exported").GetBoolean());
        Assert.DoesNotContain("1 Main St", result, StringComparison.Ordinal);
        Assert.DoesNotContain("+15555550100", result, StringComparison.Ordinal);
        Assert.Single(_customers.Customers[0].PrivacyLog);
    }

    [Fact]
    public async Task EraseCustomerData_ErasesThroughThePrivacyService_AsTheCaller_AndSkipsAnAnonymizedCustomer()
    {
        // Act
        var json = AgentToolTestContext.ReadJson(await InvokeAsync(EraseCustomerDataTool.TheName, Build(), new() { ["customerId"] = "c-dana" }));
        var again = AgentToolTestContext.ReadJson(await InvokeAsync(EraseCustomerDataTool.TheName, Build(), new() { ["customerId"] = "c-gone" }));

        // Assert
        Assert.True(json.GetProperty("erased").GetBoolean());
        Assert.Equal(new[] { "c-dana by admin" }, _erased);
        Assert.False(again.GetProperty("erased").GetBoolean());
    }

    [Fact]
    public async Task AdjustCustomerStoreCredit_RecordsTheReasonAndTheCaller_AndStopsAtZero()
    {
        // Act
        var given = AgentToolTestContext.ReadJson(await InvokeAsync(AdjustCustomerStoreCreditTool.TheName, Build(), new() { ["customerId"] = "c-dana", ["amount"] = 25, ["reason"] = "Late delivery" }));
        var taken = AgentToolTestContext.ReadJson(await InvokeAsync(AdjustCustomerStoreCreditTool.TheName, Build(), new() { ["customerId"] = "c-dana", ["amount"] = -50, ["currency"] = "usd", ["reason"] = "Correction" }));

        // Assert
        var activity = _credit[0].Activity;

        Assert.Equal(35m, given.GetProperty("balance").GetDecimal());
        Assert.Equal(-35m, taken.GetProperty("changed").GetDecimal());
        Assert.Equal(0m, _credit[0].Balance);
        Assert.Equal("Late delivery", activity[0].Reason);
        Assert.Equal(AgentToolTestContext.UserName, activity[0].Actor);
    }

    [Fact]
    public async Task AdjustCustomerStoreCredit_RefusesAGuest_ABadCurrency_OrNoCreditToTakeAway()
    {
        // Act
        var guest = await InvokeAsync(AdjustCustomerStoreCreditTool.TheName, Build(), new() { ["customerId"] = "c-guest", ["amount"] = 5, ["reason"] = "Thanks" });
        var currency = await InvokeAsync(AdjustCustomerStoreCreditTool.TheName, Build(), new() { ["customerId"] = "c-dana", ["amount"] = 5, ["currency"] = "DOLLARS", ["reason"] = "Thanks" });
        var none = await InvokeAsync(AdjustCustomerStoreCreditTool.TheName, Build(), new() { ["customerId"] = "c-dana", ["amount"] = -5, ["currency"] = "EUR", ["reason"] = "Correction" });

        // Assert
        Assert.Equal("Nothing was changed: only customers with an account can be given store credit.", guest);
        Assert.Equal("Nothing was changed: give the three-letter currency code, such as USD.", currency);
        Assert.Equal("Nothing was changed: the customer has no store credit in EUR to take away.", none);
        Assert.Equal(10m, _credit[0].Balance);
    }

    [Fact]
    public async Task AdjustCustomerLoyaltyPoints_GivesPoints_AsAnAdjustment_AndNeedsAReason()
    {
        // Act
        var json = AgentToolTestContext.ReadJson(await InvokeAsync(AdjustCustomerLoyaltyPointsTool.TheName, Build(), new() { ["customerId"] = "c-dana", ["points"] = 500, ["reason"] = "Survey" }));
        var noReason = await InvokeAsync(AdjustCustomerLoyaltyPointsTool.TheName, Build(), new() { ["customerId"] = "c-dana", ["points"] = 100, ["reason"] = " " });

        // Assert
        var activity = Assert.Single(Assert.Single(_points).Activity);

        Assert.Equal(500, json.GetProperty("points").GetInt32());
        Assert.Equal(LoyaltyActivityKind.Adjusted, activity.Kind);
        Assert.Equal(AgentToolTestContext.UserName, activity.Actor);
        Assert.Equal("Nothing was changed: say why the points change. It's kept with their activity.", noReason);
    }

    private static Dictionary<string, object> Arguments(string toolName)
        => toolName switch
        {
            ListCustomersTool.TheName => new(),
            SaveCustomerTool.TheName => new() { ["firstName"] = "Ana" },
            SaveCustomerAddressTool.TheName => new() { ["customerId"] = "c-dana", ["addressId"] = "home", ["label"] = "Home" },
            DeleteCustomerAddressTool.TheName => new() { ["customerId"] = "c-dana", ["addressId"] = "home" },
            AdjustCustomerStoreCreditTool.TheName => new() { ["customerId"] = "c-dana", ["amount"] = 5, ["reason"] = "Thanks" },
            AdjustCustomerLoyaltyPointsTool.TheName => new() { ["customerId"] = "c-dana", ["points"] = 5, ["reason"] = "Thanks" },
            _ => new() { ["customerId"] = "c-dana" },
        };

    private ServiceProvider Build(bool signedIn = true, Func<Permission, object, bool> authorize = null)
    {
        var services = AgentToolTestContext.CreateServices(signedIn, authorize);

        services.AddSingleton(typeof(IStringLocalizer<>), typeof(PassThroughStringLocalizer<>));
        services.AddAddressesCore();
        services.AddSingleton(Mock.Of<IShapeFactory>());
        services.AddScoped<CustomerAddressBook>();
        services.AddSingleton<ICustomerStore>(_customers);
        services.AddSingleton<LinkGenerator>(new TemplateLinkGenerator((name, values) => name == "CustomersExport" ? $"/Admin/customers/{values["customerId"]}/export" : null));

        var clock = new Mock<IClock>();
        clock.Setup(value => value.UtcNow).Returns(_now);
        services.AddSingleton(clock.Object);

        var site = new Mock<ISite>();
        site.Setup(value => value.GetOrCreate<CheckoutSettings>()).Returns(new CheckoutSettings { Currency = "USD" });
        site.Setup(value => value.GetOrCreate<LoyaltySettings>()).Returns(new LoyaltySettings { ProgramName = "Rewards" });
        var siteService = new Mock<ISiteService>();
        siteService.Setup(value => value.GetSiteSettingsAsync()).ReturnsAsync(site.Object);
        services.AddSingleton(siteService.Object);

        var locker = new Mock<ILocker>();
        var distributedLock = new Mock<IDistributedLock>();
        distributedLock.Setup(value => value.TryAcquireLockAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<TimeSpan?>()))
            .ReturnsAsync((locker.Object, true));
        services.AddSingleton(distributedLock.Object);
        services.AddSingleton(Mock.Of<global::YesSql.ISession>());

        var credit = new Mock<IStoreCreditStore>();
        credit.Setup(store => store.FindAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string customerId, string currency, CancellationToken _) => _credit.FirstOrDefault(account => account.CustomerId == customerId && string.Equals(account.Currency, currency, StringComparison.OrdinalIgnoreCase)));
        credit.Setup(store => store.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string id, CancellationToken _) => ValueTask.FromResult(_credit.FirstOrDefault(account => account.ItemId == id)));
        credit.Setup(store => store.ListByCustomerAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string customerId, CancellationToken _) => _credit.Where(account => account.CustomerId == customerId).ToList());
        credit.Setup(store => store.CreateAsync(It.IsAny<StoreCreditAccount>(), It.IsAny<CancellationToken>()))
            .Returns((StoreCreditAccount account, CancellationToken _) =>
            {
                _credit.Add(account);

                return ValueTask.CompletedTask;
            });
        credit.Setup(store => store.UpdateAsync(It.IsAny<StoreCreditAccount>(), It.IsAny<CancellationToken>())).Returns(ValueTask.CompletedTask);
        services.AddSingleton(credit.Object);
        services.AddScoped<StoreCreditService>();

        var points = new Mock<ILoyaltyStore>();
        points.Setup(store => store.FindByCustomerAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string customerId, CancellationToken _) => _points.FirstOrDefault(account => account.CustomerId == customerId));
        points.Setup(store => store.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string id, CancellationToken _) => ValueTask.FromResult(_points.FirstOrDefault(account => account.ItemId == id)));
        points.Setup(store => store.CreateAsync(It.IsAny<LoyaltyAccount>(), It.IsAny<CancellationToken>()))
            .Returns((LoyaltyAccount account, CancellationToken _) =>
            {
                _points.Add(account);

                return ValueTask.CompletedTask;
            });
        points.Setup(store => store.UpdateAsync(It.IsAny<LoyaltyAccount>(), It.IsAny<CancellationToken>())).Returns(ValueTask.CompletedTask);
        services.AddSingleton(points.Object);
        services.AddScoped<LoyaltyService>();

        var privacy = new Mock<ICustomerPrivacyService>();
        privacy.Setup(service => service.EraseAsync(It.IsAny<Customer>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback((Customer customer, string by, CancellationToken _) =>
            {
                _erased.Add($"{customer.ItemId} by {by}");
                customer.Anonymize(_now);
            })
            .Returns(Task.CompletedTask);
        services.AddSingleton(privacy.Object);

        return services.BuildServiceProvider();
    }

    private static Task<string> InvokeAsync(string toolName, IServiceProvider services, Dictionary<string, object> arguments)
    {
        AIFunction tool = toolName switch
        {
            ListCustomersTool.TheName => new ListCustomersTool(),
            GetCustomerTool.TheName => new GetCustomerTool(),
            SaveCustomerTool.TheName => new SaveCustomerTool(),
            SaveCustomerAddressTool.TheName => new SaveCustomerAddressTool(),
            DeleteCustomerAddressTool.TheName => new DeleteCustomerAddressTool(),
            ExportCustomerDataTool.TheName => new ExportCustomerDataTool(),
            EraseCustomerDataTool.TheName => new EraseCustomerDataTool(),
            AdjustCustomerStoreCreditTool.TheName => new AdjustCustomerStoreCreditTool(),
            _ => new AdjustCustomerLoyaltyPointsTool(),
        };

        return AgentToolTestContext.InvokeAsync(tool, services, arguments);
    }
}
