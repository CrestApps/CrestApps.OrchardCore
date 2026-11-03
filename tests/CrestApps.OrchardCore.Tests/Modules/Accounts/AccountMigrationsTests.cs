using CrestApps.OrchardCore.Accounts.Migrations;
using CrestApps.OrchardCore.Accounts.Models;
using CrestApps.OrchardCore.Accounts.Services;
using Microsoft.Extensions.Logging.Abstractions;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Builders;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.Lists.Models;

namespace CrestApps.OrchardCore.Tests.Modules.Accounts;

public sealed class AccountMigrationsTests
{
    [Fact]
    public async Task CreateAsync_FreshSite_CreatesTheAccountTypeWithItsChildTypes()
    {
        // Arrange
        var definitions = new InMemoryContentDefinitionManager();
        await definitions.StoreTypeDefinitionAsync(new ContentTypeDefinitionBuilder().WithName("Member").WithPart(AccountsTestRules.MemberPart).Build());
        var migrations = CreateMigrations(definitions);

        // Act
        var version = await migrations.CreateAsync();

        // Assert
        Assert.Equal(1, version);

        var account = await definitions.GetTypeDefinitionAsync("Account");

        Assert.NotNull(account);
        Assert.Equal(["TitlePart", "AccountPart", "ListPart"], account.Parts.Select(part => part.Name));
        Assert.Equal(["Member"], GetPart(account, "ListPart").GetSettings<ListPartSettings>().ContainedContentTypes);
        Assert.Equal(["Member"], GetPart(account, "AccountPart").GetSettings<AccountPartSettings>().OfferedContentTypes);
        Assert.True(account.GetSettings<ContentTypeSettings>().Creatable);

        var part = await definitions.GetPartDefinitionAsync("AccountPart");

        Assert.True(part.GetSettings<ContentPartSettings>().Attachable);
    }

    [Fact]
    public async Task CreateAsync_SiteThatAlreadyHasTheAccountType_LeavesTheTypeAsItIs()
    {
        // Arrange - a site where the CRM created the type, then an administrator renamed it and changed its list.
        var definitions = new InMemoryContentDefinitionManager();
        var existing = new ContentTypeDefinitionBuilder()
            .WithName("Account")
            .WithDisplayName("Customer Account")
            .WithPart("TitlePart")
            .WithPart("AccountPart", part => part.WithSettings(new AccountPartSettings { OfferedContentTypes = ["Contact"] }))
            .WithPart("ListPart", part => part.WithSettings(new ListPartSettings { PageSize = 50, ContainedContentTypes = ["Contact"] }))
            .Build();
        await definitions.StoreTypeDefinitionAsync(existing);
        var storesBefore = definitions.TypeStores;
        var migrations = CreateMigrations(definitions);

        // Act
        await migrations.CreateAsync();
        await migrations.CreateAsync();

        // Assert
        var account = await definitions.GetTypeDefinitionAsync("Account");

        Assert.Equal(storesBefore, definitions.TypeStores);
        Assert.Same(existing, account);
        Assert.Equal("Customer Account", account.DisplayName);
        Assert.Equal(50, GetPart(account, "ListPart").GetSettings<ListPartSettings>().PageSize);
    }

    [Fact]
    public async Task CreateAsync_RunTwiceOnAFreshSite_CreatesTheTypeOnce()
    {
        // Arrange
        var definitions = new InMemoryContentDefinitionManager();
        var migrations = CreateMigrations(definitions);

        // Act
        await migrations.CreateAsync();
        var storesAfterFirstRun = definitions.TypeStores;
        await migrations.CreateAsync();

        // Assert
        Assert.Equal(1, storesAfterFirstRun);
        Assert.Equal(storesAfterFirstRun, definitions.TypeStores);
    }

    private static AccountMigrations CreateMigrations(IContentDefinitionManager definitions)
    {
        var provider = new AccountContentTypeProvider(definitions, [new AccountsTestRules()]);
        var synchronizer = new AccountListSynchronizer(definitions, provider, NullLogger<AccountListSynchronizer>.Instance);

        return new AccountMigrations(definitions, synchronizer);
    }

    private static ContentTypePartDefinition GetPart(ContentTypeDefinition type, string name)
        => type.Parts.Single(part => part.Name == name);
}
