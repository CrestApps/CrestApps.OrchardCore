using CrestApps.OrchardCore.Accounts.Services;
using OrchardCore.ContentManagement.Metadata.Builders;

namespace CrestApps.OrchardCore.Tests.Modules.Accounts;

public sealed class AccountContentTypeProviderTests
{
    [Fact]
    public void IsAccountChildType_TypeARuleAllows_IsAChild()
    {
        // Arrange
        var provider = new AccountContentTypeProvider(new InMemoryContentDefinitionManager(), [new AccountsTestRules()]);
        var member = new ContentTypeDefinitionBuilder().WithName("Member").WithPart(AccountsTestRules.MemberPart).Build();

        // Act & Assert
        Assert.True(provider.IsAccountChildType(member));
        Assert.False(provider.IsExcludedFromAccounts(member));
    }

    [Fact]
    public void IsAccountChildType_TypeARuleExcludes_IsNeverAChild()
    {
        // Arrange - the exclusion wins over the rule that would allow the type.
        var provider = new AccountContentTypeProvider(new InMemoryContentDefinitionManager(), [new AccountsTestRules()]);
        var prospect = new ContentTypeDefinitionBuilder().WithName("Prospect")
            .WithPart(AccountsTestRules.MemberPart)
            .WithPart(AccountsTestRules.ProspectPart)
            .Build();

        // Act & Assert
        Assert.False(provider.IsAccountChildType(prospect));
        Assert.True(provider.IsExcludedFromAccounts(prospect));
    }

    [Fact]
    public void IsAccountChildType_AccountType_IsNeverItsOwnChild()
    {
        // Arrange
        var provider = new AccountContentTypeProvider(new InMemoryContentDefinitionManager(), [new AccountsTestRules()]);
        var household = new ContentTypeDefinitionBuilder().WithName("Household")
            .WithPart("AccountPart")
            .WithPart(AccountsTestRules.MemberPart)
            .Build();

        // Act & Assert
        Assert.True(AccountRecordKinds.IsAccount(household));
        Assert.False(provider.IsAccountChildType(household));
    }

    [Fact]
    public void IsAccountChildType_NoRules_NothingBelongsToAnAccount()
    {
        // Arrange - the Accounts module knows no child types of its own.
        var provider = new AccountContentTypeProvider(new InMemoryContentDefinitionManager(), []);
        var member = new ContentTypeDefinitionBuilder().WithName("Member").WithPart(AccountsTestRules.MemberPart).Build();

        // Act & Assert
        Assert.False(provider.IsAccountChildType(member));
        Assert.False(provider.IsAccountChildType(null));
    }

    [Fact]
    public async Task GetAccountTypesAsync_ReturnsEveryTypeWithTheAccountPart()
    {
        // Arrange
        var definitions = new InMemoryContentDefinitionManager();
        await definitions.StoreTypeDefinitionAsync(new ContentTypeDefinitionBuilder().WithName("Account").WithPart("AccountPart").Build());
        await definitions.StoreTypeDefinitionAsync(new ContentTypeDefinitionBuilder().WithName("Household").WithPart("AccountPart").Build());
        await definitions.StoreTypeDefinitionAsync(new ContentTypeDefinitionBuilder().WithName("Member").WithPart(AccountsTestRules.MemberPart).Build());
        var provider = new AccountContentTypeProvider(definitions, [new AccountsTestRules()]);

        // Act
        var accountTypes = await provider.GetAccountTypesAsync();

        // Assert
        Assert.Equal(["Account", "Household"], accountTypes.Select(type => type.Name).Order(StringComparer.Ordinal));
        Assert.True(await provider.IsAccountTypeAsync("Household"));
        Assert.False(await provider.IsAccountTypeAsync("Member"));
        Assert.False(await provider.IsAccountTypeAsync(null));
        Assert.True(await provider.IsAccountChildTypeAsync("Member"));
    }
}
