using CrestApps.OrchardCore.Accounts.Models;
using CrestApps.OrchardCore.Accounts.Services;
using Microsoft.Extensions.Logging.Abstractions;
using OrchardCore.ContentManagement.Metadata.Builders;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.Lists.Models;

namespace CrestApps.OrchardCore.Tests.Modules.Accounts;

public sealed class AccountListSynchronizerTests
{
    [Fact]
    public async Task SynchronizeAsync_NewChildType_IsOfferedAndContained()
    {
        // Arrange
        var definitions = new InMemoryContentDefinitionManager();
        await definitions.StoreTypeDefinitionAsync(Account(contained: [], offered: []));
        await definitions.StoreTypeDefinitionAsync(new ContentTypeDefinitionBuilder().WithName("Member").WithPart(AccountsTestRules.MemberPart).Build());

        // Act
        await CreateSynchronizer(definitions).SynchronizeAsync();

        // Assert
        var account = await definitions.GetTypeDefinitionAsync("Account");

        Assert.Equal(["Member"], GetList(account).ContainedContentTypes);
        Assert.Equal(["Member"], GetAccount(account).OfferedContentTypes);
    }

    [Fact]
    public async Task SynchronizeAsync_TypeAnAdministratorTookOut_StaysOut()
    {
        // Arrange - "Member" was offered before and then removed from the list by hand.
        var definitions = new InMemoryContentDefinitionManager();
        await definitions.StoreTypeDefinitionAsync(Account(contained: [], offered: ["Member"]));
        await definitions.StoreTypeDefinitionAsync(new ContentTypeDefinitionBuilder().WithName("Member").WithPart(AccountsTestRules.MemberPart).Build());
        var storesBefore = definitions.TypeStores;

        // Act
        await CreateSynchronizer(definitions).SynchronizeAsync();

        // Assert
        var account = await definitions.GetTypeDefinitionAsync("Account");

        Assert.Empty(GetList(account).ContainedContentTypes);
        Assert.Equal(storesBefore, definitions.TypeStores);
    }

    [Fact]
    public async Task SynchronizeAsync_ExcludedType_IsTakenOutOfTheList()
    {
        // Arrange - "Prospect" was contained before it became a type that must not belong to an account.
        var definitions = new InMemoryContentDefinitionManager();
        await definitions.StoreTypeDefinitionAsync(Account(contained: ["Member", "Prospect"], offered: ["Member", "Prospect"]));
        await definitions.StoreTypeDefinitionAsync(new ContentTypeDefinitionBuilder().WithName("Member").WithPart(AccountsTestRules.MemberPart).Build());
        await definitions.StoreTypeDefinitionAsync(new ContentTypeDefinitionBuilder().WithName("Prospect")
            .WithPart(AccountsTestRules.MemberPart)
            .WithPart(AccountsTestRules.ProspectPart)
            .Build());

        // Act
        await CreateSynchronizer(definitions).SynchronizeAsync();

        // Assert
        var account = await definitions.GetTypeDefinitionAsync("Account");

        Assert.Equal(["Member"], GetList(account).ContainedContentTypes);
    }

    [Fact]
    public async Task SynchronizeAsync_AccountTypeWithoutAList_IsLeftAlone()
    {
        // Arrange
        var definitions = new InMemoryContentDefinitionManager();
        await definitions.StoreTypeDefinitionAsync(new ContentTypeDefinitionBuilder().WithName("Household").WithPart("AccountPart").Build());
        await definitions.StoreTypeDefinitionAsync(new ContentTypeDefinitionBuilder().WithName("Member").WithPart(AccountsTestRules.MemberPart).Build());
        var storesBefore = definitions.TypeStores;

        // Act
        await CreateSynchronizer(definitions).SynchronizeAsync();

        // Assert
        Assert.Equal(storesBefore, definitions.TypeStores);
    }

    private static AccountListSynchronizer CreateSynchronizer(InMemoryContentDefinitionManager definitions)
        => new(definitions, new AccountContentTypeProvider(definitions, [new AccountsTestRules()]), NullLogger<AccountListSynchronizer>.Instance);

    private static ContentTypeDefinition Account(string[] contained, string[] offered)
        => new ContentTypeDefinitionBuilder()
            .WithName("Account")
            .WithPart("TitlePart")
            .WithPart("AccountPart", part => part.WithSettings(new AccountPartSettings { OfferedContentTypes = offered }))
            .WithPart("ListPart", part => part.WithSettings(new ListPartSettings { ContainedContentTypes = contained }))
            .Build();

    private static ListPartSettings GetList(ContentTypeDefinition type)
        => type.Parts.Single(part => part.Name == "ListPart").GetSettings<ListPartSettings>();

    private static AccountPartSettings GetAccount(ContentTypeDefinition type)
        => type.Parts.Single(part => part.Name == "AccountPart").GetSettings<AccountPartSettings>();
}
