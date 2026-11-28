using System.Security.Claims;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Accounts;
using CrestApps.OrchardCore.Accounts.B2B;
using CrestApps.OrchardCore.Accounts.B2B.AI;
using CrestApps.OrchardCore.Accounts.B2B.Models;
using CrestApps.OrchardCore.Accounts.B2B.Services;
using CrestApps.OrchardCore.Accounts.Models;
using CrestApps.OrchardCore.Accounts.Services;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Tests.Agent;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Handlers;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;
using OrchardCore.Settings;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.Tests.Commerce.B2B.AI;

/// <summary>
/// The business buying tools check <c>ManageB2BAccounts</c> first and the account editor's content permissions on the
/// account; they apply the Business buying card's rules, save the account the way the editor does, never issue, return
/// or accept a punchout key, and keep price lists as the Price lists pages do.
/// </summary>
public sealed class B2BAccountToolTests
{
    private const string AccountId = "northwind";
    private const string KeyHash = "0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF";

    private readonly List<string> _actions = [];
    private readonly List<PriceList> _priceLists =
    [
        new PriceList
        {
            ItemId = "wholesale",
            Name = "Wholesale 15",
            PercentOff = 15m,
            Currency = "USD",
            Entries = [.. Enumerable.Range(1, 150).Select(index => new PriceListEntry { Sku = $"SKU-{index}", UnitPrice = index })],
            CreatedUtc = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
        },
    ];

    private readonly List<B2BAccount> _savedAccounts = [];
    private ContentItem _published;
    private ContentItem _latest;
    private ContentItem _draft;

    public B2BAccountToolTests()
    {
        _published = Account("v1", published: true, part => part.Punchout = new AccountPunchout { Enabled = true, UserId = "u-jane", KeyHash = KeyHash, KeyCreatedUtc = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc) });
        _latest = _published;
    }

    public static TheoryData<string> ToolNames => new()
    {
        GetB2BAccountSummaryTool.TheName,
        SaveB2BAccountSettingsTool.TheName,
        ConfigureB2BPunchoutTool.TheName,
        ListPriceListsTool.TheName,
        SavePriceListTool.TheName,
        DeletePriceListTool.TheName,
    };

    [Theory]
    [MemberData(nameof(ToolNames))]
    public async Task Tool_RefusesACallerWhoIsNotSignedIn(string toolName)
    {
        // Act
        var result = await InvokeAsync(toolName, Build(signedIn: false), Arguments(toolName));

        // Assert
        Assert.Equal(B2BAccountsAIToolBase.NotAllowedMessage, result);
        Assert.Empty(_actions);
    }

    [Theory]
    [MemberData(nameof(ToolNames))]
    public async Task Tool_RefusesACallerWhoDoesNotManageB2BAccounts(string toolName)
    {
        // Arrange
        var services = Build(authorize: (asked, _) => asked.Name != B2BAccountsPermissions.ManageB2BAccounts.Name);

        // Act
        var result = await InvokeAsync(toolName, services, Arguments(toolName));

        // Assert
        Assert.Equal(B2BAccountsAIToolBase.NotAllowedMessage, result);
        Assert.Equal(B2BAccountsPermissions.ManageB2BAccounts.Name, Assert.Single(services.GetRequiredService<FakeAuthorizationService>().Checks).Permission);
        Assert.Empty(_actions);
        Assert.Single(_priceLists);
    }

    [Theory]
    [InlineData(GetB2BAccountSummaryTool.TheName, "ViewContent")]
    [InlineData(SaveB2BAccountSettingsTool.TheName, "EditContent")]
    [InlineData(ConfigureB2BPunchoutTool.TheName, "EditContent")]
    public async Task AccountTool_RefusesACallerWithoutTheContentPermission_OnTheAccount(string toolName, string permission)
    {
        // Arrange
        var services = Build(authorize: (asked, _) => asked.Name != permission);

        // Act
        var result = await InvokeAsync(toolName, services, Arguments(toolName));

        // Assert
        var check = services.GetRequiredService<FakeAuthorizationService>().Checks.Last();

        Assert.Equal(B2BAccountsAIToolBase.NotAllowedMessage, result);
        Assert.Equal(permission, check.Permission);
        Assert.Same(_latest, check.Resource);
        Assert.Empty(_actions);
    }

    [Fact]
    public async Task GetB2BAccountSummary_ReturnsTheStanding_AndNeverThePunchoutKey()
    {
        // Arrange
        _published = Account("v1", published: true, part =>
        {
            part.Terms = new AccountTerms { NetDays = 30, CreditLimit = 20_000m, RequirePurchaseOrder = true };
            part.PriceListId = "wholesale";
            part.OrderLimits = [new BuyerOrderLimit { UserId = "u-tom", Limit = 1_000m }];
            part.Punchout = new AccountPunchout { Enabled = true, UserId = "u-tom", KeyHash = KeyHash, KeyCreatedUtc = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc) };
        });
        _latest = _published;

        // Act
        var result = await InvokeAsync(GetB2BAccountSummaryTool.TheName, Build(), new() { ["contentItemId"] = AccountId });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);

        Assert.Equal(500m, json.GetProperty("owed").GetDecimal());
        Assert.Equal(19_500m, json.GetProperty("availableCredit").GetDecimal());
        Assert.Equal(1, json.GetProperty("openInvoices").GetProperty("total").GetInt32());
        Assert.Equal(30, json.GetProperty("terms").GetProperty("netDays").GetInt32());
        Assert.Equal("Wholesale 15", json.GetProperty("priceList").GetProperty("name").GetString());
        Assert.Equal(1_000m, json.GetProperty("buyers")[1].GetProperty("orderLimit").GetDecimal());
        Assert.True(json.GetProperty("punchout").GetProperty("hasKey").GetBoolean());
        Assert.DoesNotContain(KeyHash, result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetB2BAccountSummary_RefusesAnAccountTypeWithoutTheB2BPart()
    {
        // Act
        var result = await InvokeAsync(GetB2BAccountSummaryTool.TheName, Build(), new() { ["contentItemId"] = "household" });

        // Assert
        Assert.StartsWith("Nothing was done: the account type 'Household' has no B2BAccountPart", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SaveB2BAccountSettings_ChangesOnlyTheGivenSettings_MapsBuyersByEmail_AndKeepsPunchout()
    {
        // Act
        var json = AgentToolTestContext.ReadJson(await InvokeAsync(SaveB2BAccountSettingsTool.TheName, Build(), new()
        {
            ["contentItemId"] = AccountId,
            ["netDays"] = 30,
            ["creditLimit"] = 20_000,
            ["requirePurchaseOrder"] = true,
            ["priceListId"] = "wholesale",
            ["orderLimits"] = new object[] { new { user = "TOM@northwind.example", limit = 1_000 }, new { user = "jane", limit = (decimal?)null } },
        }));

        // Assert
        var part = _draft.Get<B2BAccountPart>(nameof(B2BAccountPart));

        Assert.True(json.GetProperty("published").GetBoolean());
        Assert.Equal(new[] { "update", "validate", "publish" }, _actions);
        Assert.Equal(30, part.Terms.NetDays);
        Assert.Equal(20_000m, part.Terms.CreditLimit);
        Assert.True(part.Terms.RequirePurchaseOrder);
        Assert.Equal("wholesale", part.PriceListId);
        Assert.Equal("VAT-1", part.TaxId);
        Assert.Equal("u-tom", Assert.Single(part.OrderLimits).UserId);
        Assert.Equal(KeyHash, part.Punchout.KeyHash);
        Assert.True(part.Punchout.Enabled);
    }

    [Fact]
    public async Task SaveB2BAccountSettings_ChangesNothing_ForANonMemberBuyer_TermsOutOfRange_OrAnUnknownPriceList()
    {
        // Act
        var result = await InvokeAsync(SaveB2BAccountSettingsTool.TheName, Build(), new()
        {
            ["contentItemId"] = AccountId,
            ["netDays"] = 400,
            ["priceListId"] = "gone",
            ["orderLimits"] = new object[] { new { user = "stranger@example.com", limit = 50 } },
        });

        // Assert
        Assert.StartsWith("Nothing was changed:", result, StringComparison.Ordinal);
        Assert.Contains("stranger@example.com is not a member of the account", result, StringComparison.Ordinal);
        Assert.Contains("Net days must be from 0 to 365", result, StringComparison.Ordinal);
        Assert.Contains("no price list with the id 'gone'", result, StringComparison.Ordinal);
        Assert.Empty(_actions);
    }

    [Fact]
    public async Task ConfigureB2BPunchout_RefusesToTurnPunchoutOn_WhenTheAccountHasNoKey()
    {
        // Arrange
        _published = Account("v1", published: true, part => part.Punchout = null);
        _latest = _published;

        // Act
        var json = AgentToolTestContext.ReadJson(await InvokeAsync(ConfigureB2BPunchoutTool.TheName, Build(), new() { ["contentItemId"] = AccountId, ["enabled"] = true, ["buyer"] = "tom" }));

        // Assert
        Assert.False(json.GetProperty("changed").GetBoolean());
        Assert.Contains("has no punchout key yet", json.GetProperty("message").GetString(), StringComparison.Ordinal);
        Assert.Empty(_actions);
    }

    [Fact]
    public async Task ConfigureB2BPunchout_ChoosesAMember_KeepsTheKey_AndNeverReturnsIt()
    {
        // Act
        var result = await InvokeAsync(ConfigureB2BPunchoutTool.TheName, Build(), new() { ["contentItemId"] = AccountId, ["buyer"] = "tom@northwind.example" });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);
        var punchout = _draft.Get<B2BAccountPart>(nameof(B2BAccountPart)).Punchout;

        Assert.True(json.GetProperty("changed").GetBoolean());
        Assert.Equal("u-tom", punchout.UserId);
        Assert.Equal(KeyHash, punchout.KeyHash);
        Assert.True(json.GetProperty("hasKey").GetBoolean());
        Assert.DoesNotContain(KeyHash, result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ConfigureB2BPunchout_RefusesASiteAdministratorAsTheBuyer()
    {
        // Act
        var result = await InvokeAsync(ConfigureB2BPunchoutTool.TheName, Build(), new() { ["contentItemId"] = AccountId, ["buyer"] = "jane" });

        // Assert
        Assert.Equal("Nothing was changed: a site administrator can't be the punchout buyer. Use a user who only buys.", result);
        Assert.Empty(_actions);
    }

    [Fact]
    public async Task ConfigureB2BPunchout_RefusesAKeyArgument()
    {
        // Act
        var result = await InvokeAsync(ConfigureB2BPunchoutTool.TheName, Build(), new() { ["contentItemId"] = AccountId, ["newKey"] = true });

        // Assert
        Assert.StartsWith("Nothing was done: 'newKey' is not an argument", result, StringComparison.Ordinal);
        Assert.Empty(_actions);
    }

    [Fact]
    public async Task ListPriceLists_ListsByName_AndPagesThePricesOfOne()
    {
        // Act
        var lists = AgentToolTestContext.ReadJson(await InvokeAsync(ListPriceListsTool.TheName, Build(), new() { ["name"] = "whole" }));
        var prices = AgentToolTestContext.ReadJson(await InvokeAsync(ListPriceListsTool.TheName, Build(), new() { ["priceListId"] = "wholesale" }));
        var missing = await InvokeAsync(ListPriceListsTool.TheName, Build(), new() { ["priceListId"] = "gone" });

        // Assert
        var list = Assert.Single(lists.GetProperty("items").EnumerateArray());

        Assert.Equal(150, list.GetProperty("entryCount").GetInt32());
        Assert.Equal(100, prices.GetProperty("prices").GetProperty("items").GetArrayLength());
        Assert.True(prices.GetProperty("prices").GetProperty("truncated").GetBoolean());
        Assert.StartsWith("There is no price list with the id 'gone'.", missing, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SavePriceList_CreatesAList_InTheStoresCurrency_WithALaterPriceReplacingAnEarlierOne()
    {
        // Act
        var json = AgentToolTestContext.ReadJson(await InvokeAsync(SavePriceListTool.TheName, Build(), new()
        {
            ["name"] = " Wholesale 20 ",
            ["percentOff"] = 20,
            ["prices"] = new object[] { new { sku = "A-1", unitPrice = 5 }, new { sku = "a-1", unitPrice = 4.5m } },
        }));

        // Assert
        var created = _priceLists.Last();

        Assert.True(json.GetProperty("created").GetBoolean());
        Assert.Equal("Wholesale 20", created.Name);
        Assert.Equal(20m, created.PercentOff);
        Assert.Equal("USD", created.Currency);
        Assert.Equal(4.5m, Assert.Single(created.Entries).UnitPrice);
    }

    [Fact]
    public async Task SavePriceList_SavesNothing_ForABadPrice_OrAPercentageOutOfRange()
    {
        // Act
        var result = await InvokeAsync(SavePriceListTool.TheName, Build(), new()
        {
            ["priceListId"] = "wholesale",
            ["percentOff"] = 120,
            ["prices"] = new object[] { new { sku = "A-1", unitPrice = -1 } },
        });

        // Assert
        Assert.StartsWith("Nothing was saved:", result, StringComparison.Ordinal);
        Assert.Contains("percentOff must be from 0 to 100", result, StringComparison.Ordinal);
        Assert.Contains("These prices aren't a SKU", result, StringComparison.Ordinal);
        Assert.Equal(15m, _priceLists[0].PercentOff);
        Assert.Equal(150, _priceLists[0].Entries.Count);
    }

    [Fact]
    public async Task DeletePriceList_PutsItsAccountsBackOnTheStoresPrices()
    {
        // Act
        var missing = await InvokeAsync(DeletePriceListTool.TheName, Build(), new() { ["priceListId"] = "gone" });
        var json = AgentToolTestContext.ReadJson(await InvokeAsync(DeletePriceListTool.TheName, Build(), new() { ["priceListId"] = "wholesale" }));

        // Assert
        Assert.StartsWith("Nothing was deleted: there is no price list with the id 'gone'.", missing, StringComparison.Ordinal);
        Assert.True(json.GetProperty("deleted").GetBoolean());
        Assert.Empty(_priceLists);
        Assert.Null(Assert.Single(_savedAccounts).Part.PriceListId);
    }

    private static Dictionary<string, object> Arguments(string toolName)
        => toolName switch
        {
            GetB2BAccountSummaryTool.TheName => new() { ["contentItemId"] = AccountId },
            SaveB2BAccountSettingsTool.TheName => new() { ["contentItemId"] = AccountId, ["netDays"] = 30 },
            ConfigureB2BPunchoutTool.TheName => new() { ["contentItemId"] = AccountId, ["buyer"] = "tom" },
            SavePriceListTool.TheName => new() { ["name"] = "Retail" },
            DeletePriceListTool.TheName => new() { ["priceListId"] = "wholesale" },
            _ => new(),
        };

    private static ContentItem Account(string versionId, bool published, Action<B2BAccountPart> configure = null)
    {
        var account = new ContentItem
        {
            ContentItemId = AccountId,
            ContentItemVersionId = versionId,
            ContentType = "Account",
            DisplayText = "Northwind Traders",
            Published = published,
            Latest = true,
        };

        account.Alter<AccountMembersPart>(part => part.Members =
        [
            new AccountMember { UserId = "u-jane", UserName = "jane", Email = "jane@northwind.example", Role = AccountMemberRole.Admin },
            new AccountMember { UserId = "u-tom", UserName = "tom", Email = "tom@northwind.example" },
        ]);
        account.Alter<B2BAccountPart>(part =>
        {
            part.Enabled = true;
            part.TaxId = "VAT-1";
            configure?.Invoke(part);
        });

        return account;
    }

    private static ContentItem Copy(ContentItem source, string versionId)
    {
        var copy = Account(versionId, published: false);
        var part = source.Get<B2BAccountPart>(nameof(B2BAccountPart));

        copy.Alter<B2BAccountPart>(saved =>
        {
            saved.Enabled = part.Enabled;
            saved.TaxId = part.TaxId;
            saved.Terms = part.Terms;
            saved.PriceListId = part.PriceListId;
            saved.OrderLimits = part.OrderLimits;
            saved.MonthlyBudget = part.MonthlyBudget;
            saved.Punchout = part.Punchout is null ? null : new AccountPunchout { Enabled = part.Punchout.Enabled, UserId = part.Punchout.UserId, KeyHash = part.Punchout.KeyHash, KeyCreatedUtc = part.Punchout.KeyCreatedUtc };
        });

        return copy;
    }

    private ServiceProvider Build(bool signedIn = true, Func<Permission, object, bool> authorize = null)
    {
        var services = AgentToolTestContext.CreateServices(signedIn, authorize);

        var contentManager = new Mock<IContentManager>();
        contentManager.Setup(manager => manager.GetAsync(AccountId, It.IsAny<VersionOptions>()))
            .ReturnsAsync((string _, VersionOptions options) =>
            {
                if (options.IsPublished)
                {
                    return _published;
                }

                if (options.IsDraftRequired)
                {
                    _draft = Copy(_latest, "v3");

                    return _draft;
                }

                return _latest;
            });
        contentManager.Setup(manager => manager.GetAsync("household", It.IsAny<VersionOptions>()))
            .ReturnsAsync(new ContentItem { ContentItemId = "household", ContentItemVersionId = "household-v1", ContentType = "Household", DisplayText = "The Lees", Latest = true, Published = true });
        contentManager.Setup(manager => manager.UpdateAsync(It.IsAny<ContentItem>())).Callback(() => _actions.Add("update")).Returns(Task.CompletedTask);
        contentManager.Setup(manager => manager.ValidateAsync(It.IsAny<ContentItem>())).Callback(() => _actions.Add("validate")).ReturnsAsync(new ContentValidateResult());
        contentManager.Setup(manager => manager.PublishAsync(It.IsAny<ContentItem>())).Callback(() => _actions.Add("publish")).ReturnsAsync(true);
        contentManager.Setup(manager => manager.SaveDraftAsync(It.IsAny<ContentItem>())).Callback(() => _actions.Add("saveDraft")).Returns(Task.CompletedTask);
        services.AddSingleton(contentManager.Object);

        var account = Type("Account", "AccountPart", "AccountMembersPart", "B2BAccountPart");
        var household = Type("Household", "AccountPart");
        var definitionManager = new Mock<IContentDefinitionManager>();
        definitionManager.Setup(manager => manager.GetTypeDefinitionAsync("Account")).ReturnsAsync(account);
        definitionManager.Setup(manager => manager.GetTypeDefinitionAsync("Household")).ReturnsAsync(household);
        services.AddSingleton(definitionManager.Object);

        var typeProvider = new Mock<IAccountContentTypeProvider>();
        typeProvider.Setup(provider => provider.IsAccountTypeAsync(It.IsAny<string>())).ReturnsAsync((string type) => type is "Account" or "Household");
        services.AddSingleton(typeProvider.Object);

        var site = new Mock<ISite>();
        site.Setup(value => value.GetOrCreate<CheckoutSettings>()).Returns(new CheckoutSettings { Currency = "USD" });
        var siteService = new Mock<ISiteService>();
        siteService.Setup(value => value.GetSiteSettingsAsync()).ReturnsAsync(site.Object);
        services.AddSingleton(siteService.Object);

        var priceLists = new Mock<IPriceListStore>();
        priceLists.Setup(store => store.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string id, CancellationToken _) => ValueTask.FromResult(_priceLists.FirstOrDefault(list => list.ItemId == id)));
        priceLists.Setup(store => store.PageAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string search, int skip, int take, CancellationToken _) =>
            {
                var matches = _priceLists.Where(list => search is null || list.Name.Contains(search, StringComparison.OrdinalIgnoreCase)).ToList();

                return (matches.Count, matches.Skip(skip).Take(take).ToList());
            });
        priceLists.Setup(store => store.CreateAsync(It.IsAny<PriceList>(), It.IsAny<CancellationToken>()))
            .Returns((PriceList list, CancellationToken _) =>
            {
                _priceLists.Add(list);

                return ValueTask.CompletedTask;
            });
        priceLists.Setup(store => store.UpdateAsync(It.IsAny<PriceList>(), It.IsAny<CancellationToken>())).Returns(ValueTask.CompletedTask);
        priceLists.Setup(store => store.DeleteAsync(It.IsAny<PriceList>(), It.IsAny<CancellationToken>()))
            .Returns((PriceList list, CancellationToken _) => ValueTask.FromResult(_priceLists.Remove(list)));
        services.AddSingleton(priceLists.Object);

        var accounts = new Mock<IB2BAccountStore>();
        accounts.Setup(store => store.ListByPriceListAsync("wholesale", It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => [new B2BAccount(Account("v1", published: true, part => part.PriceListId = "wholesale"))]);
        accounts.Setup(store => store.SaveAsync(It.IsAny<B2BAccount>()))
            .Callback((B2BAccount saved) => _savedAccounts.Add(saved))
            .Returns(Task.CompletedTask);
        services.AddSingleton(accounts.Object);

        var transactions = new Mock<ITransactionStore>();
        transactions.Setup(store => store.GetByParentReferenceAsync(AccountsConstants.ReferenceTypes.Account, AccountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new Transaction
                {
                    ItemId = "invoice-1",
                    Source = B2BAccountsConstants.TransactionSource,
                    Currency = "USD",
                    Amount = 500m,
                    TotalAmount = 500m,
                    Status = TransactionStatus.Outstanding,
                    CreatedUtc = new DateTime(2026, 10, 2, 0, 0, 0, DateTimeKind.Utc),
                },
            ]);

        var b2bStore = new Mock<IB2BAccountStore>();
        var accountService = new B2BAccountService(b2bStore.Object, new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal() } });
        services.AddSingleton(new AccountTermsService(transactions.Object, accountService, siteService.Object));

        var users = new Dictionary<string, bool> { ["u-jane"] = true, ["u-tom"] = false };
        var userManager = new Mock<UserManager<IUser>>(Mock.Of<IUserStore<IUser>>(), null, null, null, null, null, null, null, null);
        userManager.Setup(manager => manager.FindByIdAsync(It.IsAny<string>()))
            .ReturnsAsync((string id) => users.ContainsKey(id) ? new TestUser(id) : null);
        userManager.Setup(manager => manager.IsInRoleAsync(It.IsAny<IUser>(), It.IsAny<string>()))
            .ReturnsAsync((IUser user, string _) => users[((TestUser)user).Id]);
        services.AddSingleton(userManager.Object);

        var clock = new Mock<IClock>();
        clock.Setup(value => value.UtcNow).Returns(new DateTime(2026, 11, 18, 12, 0, 0, DateTimeKind.Utc));
        services.AddSingleton(clock.Object);

        return services.BuildServiceProvider();
    }

    private static ContentTypeDefinition Type(string name, params string[] parts)
        => new(
            name,
            name,
            parts.Select(part => new ContentTypePartDefinition(part, new ContentPartDefinition(part, [], new JsonObject()), new JsonObject())),
            new JsonObject());

    private static Task<string> InvokeAsync(string toolName, IServiceProvider services, Dictionary<string, object> arguments)
    {
        AIFunction tool = toolName switch
        {
            GetB2BAccountSummaryTool.TheName => new GetB2BAccountSummaryTool(),
            SaveB2BAccountSettingsTool.TheName => new SaveB2BAccountSettingsTool(),
            ConfigureB2BPunchoutTool.TheName => new ConfigureB2BPunchoutTool(),
            ListPriceListsTool.TheName => new ListPriceListsTool(),
            SavePriceListTool.TheName => new SavePriceListTool(),
            _ => new DeletePriceListTool(),
        };

        return AgentToolTestContext.InvokeAsync(tool, services, arguments);
    }

    private sealed class TestUser : IUser
    {
        public TestUser(string id)
        {
            Id = id;
        }

        public string Id { get; }

        public string UserName => Id;
    }
}
