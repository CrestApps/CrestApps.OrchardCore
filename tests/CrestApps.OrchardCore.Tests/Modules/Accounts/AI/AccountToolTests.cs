using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Accounts;
using CrestApps.OrchardCore.Accounts.AI;
using CrestApps.OrchardCore.Accounts.Models;
using CrestApps.OrchardCore.Accounts.Services;
using CrestApps.OrchardCore.Addresses.Core;
using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Staff.Services;
using CrestApps.OrchardCore.Tests.Agent;
using CrestApps.OrchardCore.Tests.Agent.AIModels;
using CrestApps.OrchardCore.Tests.Utilities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Handlers;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.ContentManagement.Records;
using OrchardCore.Security.Permissions;
using OrchardCore.Users;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;
using ISession = YesSql.ISession;
using IStore = YesSql.IStore;
using StoreFactory = YesSql.StoreFactory;

namespace CrestApps.OrchardCore.Tests.Modules.Accounts.AI;

/// <summary>
/// The account tools run the rules the account editor's parts run (members, locations, team), which saving an account
/// through the content tools skips; they check the editor's permissions with the account as the resource and save the
/// change the way the editor's buttons do, never publishing someone's pending draft.
/// </summary>
public sealed class AccountToolTests : IAsyncLifetime
{
    private const string AccountId = "northwind";

    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"account-tools-{Guid.NewGuid():N}.db");
    private readonly List<string> _actions = [];
    private IStore _store;
    private ContentItem _published;
    private ContentItem _latest;
    private ContentItem _draft;

    public static TheoryData<string> ToolNames => new()
    {
        ListAccountsTool.TheName,
        GetAccountTool.TheName,
        SetAccountMembersTool.TheName,
        SaveAccountLocationTool.TheName,
        DeleteAccountLocationTool.TheName,
        SetAccountTeamTool.TheName,
    };

    public async ValueTask InitializeAsync()
    {
        _published = Account("v1", published: true);
        _latest = _published;

        _store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={_databasePath};Pooling=False"));
        _store.RegisterIndexes([new ContentItemIndexProvider()]);
        await _store.InitializeAsync(TestContext.Current.CancellationToken);

        await using (var session = _store.CreateSession())
        {
            var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
            var schemaBuilder = new SchemaBuilder(_store.Configuration, transaction);

            await schemaBuilder.CreateMapIndexTableAsync<ContentItemIndex>(table => table
                .Column<string>("ContentItemId", column => column.WithLength(26))
                .Column<string>("ContentItemVersionId", column => column.WithLength(26))
                .Column<bool>("Published")
                .Column<bool>("Latest")
                .Column<string>("ContentType", column => column.WithLength(255))
                .Column<DateTime>("ModifiedUtc")
                .Column<DateTime>("PublishedUtc")
                .Column<DateTime>("CreatedUtc")
                .Column<string>("Owner", column => column.WithLength(255))
                .Column<string>("Author", column => column.WithLength(255))
                .Column<string>("DisplayText", column => column.WithLength(255)));

            await transaction.CommitAsync(TestContext.Current.CancellationToken);
        }

        await using (var session = _store.CreateSession())
        {
            await session.SaveAsync(Account("v1", published: true), cancellationToken: TestContext.Current.CancellationToken);
            await session.SaveAsync(new ContentItem { ContentItemId = "contoso", ContentItemVersionId = "contoso-v1", ContentType = "Account", DisplayText = "Contoso", Latest = true, Published = false }, cancellationToken: TestContext.Current.CancellationToken);
            await session.SaveAsync(new ContentItem { ContentItemId = "lead-1", ContentItemVersionId = "lead-1-v1", ContentType = "Lead", DisplayText = "Northwind lead", Latest = true, Published = true }, cancellationToken: TestContext.Current.CancellationToken);
            await session.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
    }

    public ValueTask DisposeAsync()
    {
        TemporarySqliteDatabase.DisposeAndDelete(_store, _databasePath);

        return ValueTask.CompletedTask;
    }

    [Theory]
    [MemberData(nameof(ToolNames))]
    public async Task Tool_RefusesACallerWhoIsNotSignedIn(string toolName)
    {
        // Act
        var result = await InvokeAsync(toolName, Build(signedIn: false), Arguments(toolName));

        // Assert
        Assert.Equal(AccountsAIToolBase.NotAllowedMessage, result);
        Assert.Empty(_actions);
    }

    [Theory]
    [InlineData(ListAccountsTool.TheName, "ListContent", false)]
    [InlineData(GetAccountTool.TheName, "AccessAdminPanel", false)]
    [InlineData(GetAccountTool.TheName, "ListContent", false)]
    [InlineData(GetAccountTool.TheName, "ViewContent", true)]
    [InlineData(SetAccountMembersTool.TheName, "ManageAccountMembers", false)]
    [InlineData(SetAccountMembersTool.TheName, "EditContent", true)]
    [InlineData(SaveAccountLocationTool.TheName, "EditContent", true)]
    [InlineData(DeleteAccountLocationTool.TheName, "EditContent", true)]
    [InlineData(SetAccountTeamTool.TheName, "EditContent", true)]
    public async Task Tool_RefusesACallerWithoutThePermission(string toolName, string permission, bool onTheAccount)
    {
        // Arrange
        var services = Build(authorize: (asked, _) => asked.Name != permission);

        // Act
        var result = await InvokeAsync(toolName, services, Arguments(toolName));

        // Assert
        var check = services.GetRequiredService<FakeAuthorizationService>().Checks.Last();

        Assert.Equal(AccountsAIToolBase.NotAllowedMessage, result);
        Assert.Equal(permission, check.Permission);

        if (onTheAccount)
        {
            Assert.Same(_latest, check.Resource);
        }

        Assert.Empty(_actions);
    }

    [Fact]
    public async Task ListAccounts_FindsAccountsByName_InTheTypesTheCallerMayView()
    {
        // Act
        var all = AgentToolTestContext.ReadJson(await InvokeAsync(ListAccountsTool.TheName, Build(), new()));
        var named = AgentToolTestContext.ReadJson(await InvokeAsync(ListAccountsTool.TheName, Build(), new() { ["name"] = "north" }));
        var hidden = AgentToolTestContext.ReadJson(await InvokeAsync(ListAccountsTool.TheName, Build(authorize: (asked, _) => asked.Name != "ViewContent"), new()));

        // Assert
        var account = Assert.Single(named.GetProperty("items").EnumerateArray());

        Assert.Equal(2, all.GetProperty("total").GetInt32());
        Assert.Equal(AccountId, account.GetProperty("id").GetString());
        Assert.Equal(1, account.GetProperty("memberCount").GetInt32());
        Assert.Equal("Head office", Assert.Single(account.GetProperty("locations").EnumerateArray()).GetProperty("name").GetString());
        Assert.Equal("Sam Carter", account.GetProperty("ownerName").GetString());
        Assert.Equal(0, hidden.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task GetAccount_ReturnsTheMembersLocationsAndTeam_AsTheAccountPageShowsThem()
    {
        // Act
        var result = await InvokeAsync(GetAccountTool.TheName, Build(), new() { ["contentItemId"] = AccountId });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);
        var member = Assert.Single(json.GetProperty("members").EnumerateArray());
        var location = Assert.Single(json.GetProperty("locations").EnumerateArray());
        var team = json.GetProperty("team");

        Assert.Equal(AccountId, json.GetProperty("contentItemId").GetString());
        Assert.Equal("Northwind Traders", json.GetProperty("name").GetString());
        Assert.Equal("Account", json.GetProperty("type").GetString());
        Assert.True(json.GetProperty("published").GetBoolean());
        Assert.False(json.GetProperty("hasUnpublishedChanges").GetBoolean());
        Assert.Equal("jane@northwind.example", member.GetProperty("user").GetString());
        Assert.Equal("Member", member.GetProperty("role").GetString());
        Assert.Equal("contact-1", member.GetProperty("contactContentItemId").GetString());
        Assert.Equal("hq", location.GetProperty("locationId").GetString());
        Assert.Equal("Seattle", location.GetProperty("address").GetProperty("city").GetString());
        Assert.Equal("s-sam", team.GetProperty("owner").GetProperty("staffId").GetString());
        Assert.Equal("Sam Carter", team.GetProperty("owner").GetProperty("name").GetString());
        Assert.Empty(team.GetProperty("members").EnumerateArray());

        // The content item is never serialized: no user id, no part names.
        Assert.DoesNotContain("u-jane", result, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(AccountMembersPart), result, StringComparison.Ordinal);
        Assert.Empty(_actions);
    }

    [Fact]
    public async Task GetAccount_ReturnsTheEmailDomains_AndTheBillingLocation_OfTheAccountsEditor()
    {
        // Arrange
        _latest.Alter<AccountLocationsPart>(part => part.Locations[0].IsBilling = true);

        // Act
        var json = AgentToolTestContext.ReadJson(await InvokeAsync(GetAccountTool.TheName, Build(), new() { ["contentItemId"] = AccountId }));

        // Assert
        Assert.Equal(["northwind.example", "northwind-traders.example"], json.GetProperty("emailDomains").EnumerateArray().Select(domain => domain.GetString()));
        Assert.True(Assert.Single(json.GetProperty("locations").EnumerateArray()).GetProperty("isBilling").GetBoolean());
    }

    [Fact]
    public async Task GetAccount_SaysWhenItShowsUnpublishedChanges_AndRefusesAnItemThatIsNotAnAccount()
    {
        // Arrange
        _latest = Account("v2", published: false);

        // Act
        var draft = AgentToolTestContext.ReadJson(await InvokeAsync(GetAccountTool.TheName, Build(), new() { ["contentItemId"] = AccountId }));
        var lead = await InvokeAsync(GetAccountTool.TheName, Build(), new() { ["contentItemId"] = "lead-1" });

        // Assert
        Assert.True(draft.GetProperty("published").GetBoolean());
        Assert.True(draft.GetProperty("hasUnpublishedChanges").GetBoolean());
        Assert.Contains("unpublished changes", draft.GetProperty("note").GetString(), StringComparison.Ordinal);
        Assert.StartsWith("Nothing was done: no account has the id 'lead-1'.", lead, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SetAccountMembers_ResolvesTheUsers_KeepsTheirContact_AndPublishes()
    {
        // Act
        var json = AgentToolTestContext.ReadJson(await InvokeAsync(SetAccountMembersTool.TheName, Build(), new()
        {
            ["contentItemId"] = AccountId,
            ["members"] = new object[] { new { user = "jane@northwind.example", role = "Admin" }, new { user = "tom" }, new { user = "TOM@northwind.example" } },
        }));

        // Assert
        var members = _draft.Get<AccountMembersPart>(nameof(AccountMembersPart)).Members;

        Assert.True(json.GetProperty("published").GetBoolean());
        Assert.Equal(new[] { "update", "validate", "publish" }, _actions);
        Assert.Equal(new[] { "u-jane", "u-tom" }, members.Select(member => member.UserId));
        Assert.Equal(AccountMemberRole.Admin, members[0].Role);
        Assert.Equal("contact-1", members[0].ContactContentItemId);
        Assert.Equal(1, json.GetProperty("added").GetInt32());
    }

    [Fact]
    public async Task SetAccountMembers_ChangesNothing_WhenAUserDoesNotQualify()
    {
        // Act
        var result = await InvokeAsync(SetAccountMembersTool.TheName, Build(), new()
        {
            ["contentItemId"] = AccountId,
            ["members"] = new object[] { new { user = "jane@northwind.example" }, new { user = "nobody@example.com" }, new { user = "alex" } },
        });

        // Assert
        Assert.StartsWith("Nothing was changed:", result, StringComparison.Ordinal);
        Assert.Contains("There's no user nobody@example.com", result, StringComparison.Ordinal);
        Assert.Contains("alex is already a member of Contoso", result, StringComparison.Ordinal);
        Assert.Empty(_actions);
    }

    [Fact]
    public async Task SaveAccountLocation_AddsALocation_AddressedToTheAccount()
    {
        // Act
        var json = AgentToolTestContext.ReadJson(await InvokeAsync(SaveAccountLocationTool.TheName, Build(), new()
        {
            ["contentItemId"] = AccountId,
            ["address"] = new { addressLine1 = "500 Harbor Blvd", city = "Oakland", region = "CA", postalCode = "94607", country = "US" },
        }));

        // Assert
        var locations = _draft.Get<AccountLocationsPart>(nameof(AccountLocationsPart)).Locations;
        var added = locations.Last();

        Assert.True(json.GetProperty("created").GetBoolean());
        Assert.Equal(2, locations.Count);
        Assert.Equal("Oakland", added.Name);
        Assert.Equal("Northwind Traders", added.Address.Company);
        Assert.Null(added.Address.Name);
        Assert.False(string.IsNullOrEmpty(added.LocationId));
    }

    [Fact]
    public async Task SaveAccountLocation_RefusesAnAddressItsCountryWouldNotAccept()
    {
        // Act
        var result = await InvokeAsync(SaveAccountLocationTool.TheName, Build(), new()
        {
            ["contentItemId"] = AccountId,
            ["name"] = "Warehouse",
            ["address"] = new { addressLine1 = "500 Harbor Blvd", city = "Oakland", region = "CA", postalCode = "ABCDE", country = "US" },
        });

        // Assert
        Assert.StartsWith("Nothing was changed: the address does not have its country's form.", result, StringComparison.Ordinal);
        Assert.Empty(_actions);
    }

    [Fact]
    public async Task SaveAccountLocation_KeepsTheBillingLocation_WhenItChanges()
    {
        // Arrange
        _latest.Alter<AccountLocationsPart>(part => part.Locations[0].IsBilling = true);

        // Act
        var json = AgentToolTestContext.ReadJson(await InvokeAsync(SaveAccountLocationTool.TheName, Build(), new()
        {
            ["contentItemId"] = AccountId,
            ["locationId"] = "hq",
            ["name"] = "Headquarters",
        }));

        // Assert
        var location = Assert.Single(_draft.Get<AccountLocationsPart>(nameof(AccountLocationsPart)).Locations);

        Assert.Equal("Headquarters", location.Name);
        Assert.True(location.IsBilling);
        Assert.True(json.GetProperty("isBilling").GetBoolean());
    }

    [Fact]
    public async Task SaveAccountLocation_RefusesASecondBillingLocation()
    {
        // Arrange
        _latest.Alter<AccountLocationsPart>(part => part.Locations[0].IsBilling = true);

        // Act
        var result = await InvokeAsync(SaveAccountLocationTool.TheName, Build(), new()
        {
            ["contentItemId"] = AccountId,
            ["address"] = new { addressLine1 = "500 Harbor Blvd", city = "Oakland", region = "CA", postalCode = "94607", country = "US" },
            ["isBilling"] = true,
        });

        // Assert
        Assert.StartsWith("Nothing was changed: only one location can be the billing location, and 'Head office'", result, StringComparison.Ordinal);
        Assert.Empty(_actions);
    }

    [Fact]
    public async Task DeleteAccountLocation_RemovesTheLocation_AndRefusesAnUnknownOne()
    {
        // Act
        var missing = await InvokeAsync(DeleteAccountLocationTool.TheName, Build(), new() { ["contentItemId"] = AccountId, ["locationId"] = "nowhere" });
        var json = AgentToolTestContext.ReadJson(await InvokeAsync(DeleteAccountLocationTool.TheName, Build(), new() { ["contentItemId"] = AccountId, ["locationId"] = "hq" }));

        // Assert
        Assert.StartsWith("Nothing was removed: the account has no location with the id 'nowhere'.", missing, StringComparison.Ordinal);
        Assert.True(json.GetProperty("removed").GetBoolean());
        Assert.Empty(_draft.Get<AccountLocationsPart>(nameof(AccountLocationsPart)).Locations);
    }

    [Fact]
    public async Task SetAccountTeam_SetsTheOwnerAndTheTeam_WithoutTheOwnerInTheTeam()
    {
        // Act
        var json = AgentToolTestContext.ReadJson(await InvokeAsync(SetAccountTeamTool.TheName, Build(), new()
        {
            ["contentItemId"] = AccountId,
            ["ownerStaffId"] = "s-ann",
            ["teamStaffIds"] = new[] { "s-sam", "s-ann", "s-sam" },
        }));

        // Assert
        var team = _draft.Get<AccountTeamPart>(nameof(AccountTeamPart));

        Assert.Equal("s-ann", team.OwnerStaffId);
        Assert.Equal(new[] { "s-sam" }, team.TeamStaffIds);
        Assert.Equal("Ann Lee", json.GetProperty("owner").GetProperty("name").GetString());
    }

    [Fact]
    public async Task SetAccountTeam_RefusesAnIdThatIsNotStaff()
    {
        // Act
        var result = await InvokeAsync(SetAccountTeamTool.TheName, Build(), new() { ["contentItemId"] = AccountId, ["ownerStaffId"] = "s-ghost" });

        // Assert
        Assert.StartsWith("Nothing was changed: 's-ghost' is not a staff member id.", result, StringComparison.Ordinal);
        Assert.Empty(_actions);
    }

    [Fact]
    public async Task AChange_NeverPublishesAPendingDraft_AndNeedsPublishContentToPublish()
    {
        // Arrange
        _latest = Account("v2", published: false);

        // Act
        var refused = await InvokeAsync(SetAccountTeamTool.TheName, Build(), new() { ["contentItemId"] = AccountId, ["ownerStaffId"] = "s-ann" });
        var draft = AgentToolTestContext.ReadJson(await InvokeAsync(SetAccountTeamTool.TheName, Build(), new() { ["contentItemId"] = AccountId, ["ownerStaffId"] = "s-ann", ["saveAsDraft"] = true }));

        _latest = _published;
        _actions.Clear();

        var noPublish = await InvokeAsync(SetAccountTeamTool.TheName, Build(authorize: (asked, _) => asked.Name != "PublishContent"), new() { ["contentItemId"] = AccountId, ["ownerStaffId"] = "s-ann" });

        // Assert
        Assert.Contains("unpublished changes", refused, StringComparison.Ordinal);
        Assert.True(draft.GetProperty("savedAsDraft").GetBoolean());
        Assert.Contains("permission to publish", noPublish, StringComparison.Ordinal);
        Assert.Empty(_actions);
    }

    [Fact]
    public async Task ATool_RefusesAnItemThatIsNotAnAccount()
    {
        // Act
        var result = await InvokeAsync(SetAccountTeamTool.TheName, Build(), new() { ["contentItemId"] = "lead-1", ["ownerStaffId"] = "s-ann" });

        // Assert
        Assert.StartsWith("Nothing was done: no account has the id 'lead-1'.", result, StringComparison.Ordinal);
    }

    private static Dictionary<string, object> Arguments(string toolName)
        => toolName switch
        {
            GetAccountTool.TheName => new() { ["contentItemId"] = AccountId },
            SetAccountMembersTool.TheName => new() { ["contentItemId"] = AccountId, ["members"] = new object[] { new { user = "tom" } } },
            SaveAccountLocationTool.TheName => new() { ["contentItemId"] = AccountId, ["name"] = "HQ" },
            DeleteAccountLocationTool.TheName => new() { ["contentItemId"] = AccountId, ["locationId"] = "hq" },
            SetAccountTeamTool.TheName => new() { ["contentItemId"] = AccountId, ["ownerStaffId"] = "s-ann" },
            _ => new(),
        };

    private static ContentItem Account(string versionId, bool published)
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

        account.Alter<AccountMembersPart>(part => part.Members = [new AccountMember { UserId = "u-jane", UserName = "jane", Email = "jane@northwind.example", ContactContentItemId = "contact-1" }]);
        account.Alter<AccountLocationsPart>(part => part.Locations = [new AccountLocation { LocationId = "hq", Name = "Head office", Address = new Address { AddressLine1 = "1 Main St", City = "Seattle", Region = "WA", PostalCode = "98101", Country = "US" } }]);
        account.Alter<AccountTeamPart>(part => part.OwnerStaffId = "s-sam");
        account.Alter<AccountEmailDomainsPart>(part => part.Domains = ["northwind.example", "northwind-traders.example"]);

        return account;
    }

    private static ContentItem Copy(ContentItem source, string versionId)
    {
        var copy = Account(versionId, published: false);

        copy.Alter<AccountMembersPart>(part => part.Members = [.. source.Get<AccountMembersPart>(nameof(AccountMembersPart)).Members]);
        copy.Alter<AccountLocationsPart>(part => part.Locations = [.. source.Get<AccountLocationsPart>(nameof(AccountLocationsPart)).Locations]);

        return copy;
    }

    private ServiceProvider Build(bool signedIn = true, Func<Permission, object, bool> authorize = null)
    {
        var services = AgentToolTestContext.CreateServices(signedIn, authorize);

        services.AddScoped(_ => _store.CreateSession());
        services.AddSingleton(typeof(IStringLocalizer<>), typeof(PassThroughStringLocalizer<>));
        services.AddAddressesCore();

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
        contentManager.Setup(manager => manager.GetAsync("contoso", It.IsAny<VersionOptions>()))
            .ReturnsAsync(new ContentItem { ContentItemId = "contoso", ContentType = "Account", DisplayText = "Contoso" });
        contentManager.Setup(manager => manager.GetAsync("lead-1", It.IsAny<VersionOptions>()))
            .ReturnsAsync(new ContentItem { ContentItemId = "lead-1", ContentType = "Lead", DisplayText = "Northwind lead", Latest = true, Published = true });
        contentManager.Setup(manager => manager.UpdateAsync(It.IsAny<ContentItem>())).Callback(() => _actions.Add("update")).Returns(Task.CompletedTask);
        contentManager.Setup(manager => manager.ValidateAsync(It.IsAny<ContentItem>())).Callback(() => _actions.Add("validate")).ReturnsAsync(new ContentValidateResult());
        contentManager.Setup(manager => manager.PublishAsync(It.IsAny<ContentItem>())).Callback(() => _actions.Add("publish")).ReturnsAsync(true);
        contentManager.Setup(manager => manager.SaveDraftAsync(It.IsAny<ContentItem>())).Callback(() => _actions.Add("saveDraft")).Returns(Task.CompletedTask);
        services.AddSingleton(contentManager.Object);

        var definition = new ContentTypeDefinition(
            "Account",
            "Account",
            new[] { "AccountPart", "AccountMembersPart", "AccountLocationsPart", "AccountTeamPart", "AccountEmailDomainsPart" }
                .Select(name => new ContentTypePartDefinition(name, new ContentPartDefinition(name, [], new JsonObject()), new JsonObject())),
            new JsonObject());

        var definitionManager = new Mock<IContentDefinitionManager>();
        definitionManager.Setup(manager => manager.GetTypeDefinitionAsync("Account")).ReturnsAsync(definition);
        services.AddSingleton(definitionManager.Object);

        var typeProvider = new Mock<IAccountContentTypeProvider>();
        typeProvider.Setup(provider => provider.GetAccountTypesAsync()).ReturnsAsync(new[] { definition });
        typeProvider.Setup(provider => provider.IsAccountTypeAsync(It.IsAny<string>())).ReturnsAsync((string type) => type == "Account");
        services.AddSingleton(typeProvider.Object);

        var users = new Dictionary<string, (string Id, string Name, string Email)>(StringComparer.OrdinalIgnoreCase)
        {
            ["jane"] = ("u-jane", "jane", "jane@northwind.example"),
            ["tom"] = ("u-tom", "tom", "tom@northwind.example"),
            ["alex"] = ("u-alex", "alex", "alex@contoso.example"),
        };

        var userManager = new Mock<UserManager<IUser>>(Mock.Of<IUserStore<IUser>>(), null, null, null, null, null, null, null, null);
        userManager.Setup(manager => manager.FindByNameAsync(It.IsAny<string>()))
            .ReturnsAsync((string name) => users.TryGetValue(name, out var user) ? new TestUser(user.Id, user.Name, user.Email) : null);
        userManager.Setup(manager => manager.FindByEmailAsync(It.IsAny<string>()))
            .ReturnsAsync((string email) => users.Values.Where(user => string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase)).Select(user => (IUser)new TestUser(user.Id, user.Name, user.Email)).FirstOrDefault());
        userManager.Setup(manager => manager.GetUserIdAsync(It.IsAny<IUser>())).ReturnsAsync((IUser user) => ((TestUser)user).Id);
        userManager.Setup(manager => manager.GetUserNameAsync(It.IsAny<IUser>())).ReturnsAsync((IUser user) => user.UserName);
        userManager.Setup(manager => manager.GetEmailAsync(It.IsAny<IUser>())).ReturnsAsync((IUser user) => ((TestUser)user).Email);
        services.AddSingleton(userManager.Object);

        var memberService = new Mock<IAccountMemberService>();
        memberService.Setup(service => service.FindOtherAccountIdAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string userId, string _, CancellationToken _) => userId == "u-alex" ? "contoso" : null);
        services.AddSingleton(memberService.Object);

        var staff = new Dictionary<string, string> { ["s-sam"] = "Sam Carter", ["s-ann"] = "Ann Lee" };
        var directory = new Mock<IStaffDirectory>();
        directory.Setup(item => item.GetNamesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<string> ids, CancellationToken _) => ids.Where(staff.ContainsKey).Distinct().ToDictionary(id => id, id => staff[id]));
        services.AddSingleton(directory.Object);

        return services.BuildServiceProvider();
    }

    private static Task<string> InvokeAsync(string toolName, IServiceProvider services, Dictionary<string, object> arguments)
    {
        AIFunction tool = toolName switch
        {
            ListAccountsTool.TheName => new ListAccountsTool(),
            GetAccountTool.TheName => new GetAccountTool(),
            SetAccountMembersTool.TheName => new SetAccountMembersTool(),
            SaveAccountLocationTool.TheName => new SaveAccountLocationTool(),
            DeleteAccountLocationTool.TheName => new DeleteAccountLocationTool(),
            _ => new SetAccountTeamTool(),
        };

        return AgentToolTestContext.InvokeAsync(tool, services, arguments);
    }

    private sealed class TestUser : IUser
    {
        public TestUser(string id, string userName, string email)
        {
            Id = id;
            UserName = userName;
            Email = email;
        }

        public string Id { get; }

        public string UserName { get; }

        public string Email { get; }
    }
}
