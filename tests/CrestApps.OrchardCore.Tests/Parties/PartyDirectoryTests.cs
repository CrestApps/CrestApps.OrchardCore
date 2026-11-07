using CrestApps.OrchardCore.Assets.Models;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Parties.Services;

namespace CrestApps.OrchardCore.Tests.Parties;

/// <summary>
/// A party is an open kind and an id; each module that keeps parties of a kind registers a source, and the directory
/// finds and names them without knowing the module. Asset owners read the same sources, their stored kinds unchanged.
/// </summary>
public sealed class PartyDirectoryTests
{
    [Fact]
    public async Task TheDirectory_NamesPartiesOfEveryKind_WithOneLookupPerKind()
    {
        // Arrange
        var users = new Source(PartyKinds.User, ("u1", "Ana"), ("u2", "Ben"));
        var accounts = new Source(PartyKinds.Account, ("a1", "Contoso"));
        var directory = new PartyDirectory([users, accounts, new Source(PartyKinds.User, ("u1", "Shadowed"))]);

        // Act
        var names = await directory.GetNamesAsync(
            [new PartyRef(PartyKinds.User, "u1"), new PartyRef(PartyKinds.User, "u2"), new PartyRef(PartyKinds.Account, "a1"), new PartyRef(PartyKinds.Contact, "c1"), default],
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal([PartyKinds.User, PartyKinds.Account], directory.Kinds);
        Assert.Equal("Ana", names[new PartyRef(PartyKinds.User, "u1")]);
        Assert.Equal("Ben", names[new PartyRef(PartyKinds.User, "u2")]);
        Assert.Equal("Contoso", names[new PartyRef(PartyKinds.Account, "a1")]);
        Assert.Equal(3, names.Count);
        Assert.Equal(1, users.NameLookups);
        Assert.Same(users, directory.GetSource("user"));
        Assert.Null(await directory.GetNameAsync(new PartyRef(PartyKinds.Contact, "c1"), TestContext.Current.CancellationToken));
        Assert.Empty(await directory.SearchAsync(PartyKinds.Contact, "x", 10, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("User:abc", "User", "abc")]
    [InlineData("Account:4x:7", "Account", "4x:7")]
    public void APartyRef_RoundTripsAsKindAndId(string text, string kind, string id)
    {
        Assert.True(PartyRef.TryParse(text, out var party));
        Assert.Equal(new PartyRef(kind, id), party);
        Assert.Equal(text, party.ToString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("User")]
    [InlineData(":abc")]
    [InlineData("User:")]
    public void APartyRef_WithoutAKindOrAnId_IsNotRead(string text)
    {
        Assert.False(PartyRef.TryParse(text, out _));
    }

    [Fact]
    public void AnAssetsOwner_IsReadAsAParty_WithTheStoredKindKept()
    {
        // A "Customer" owner has always stored a user id, so it is a User party.
        Assert.Equal(new PartyRef(PartyKinds.User, "u1"), new Asset { OwnerKind = AssetOwnerKind.Customer, OwnerId = "u1" }.ToParty());
        Assert.Equal(new PartyRef(PartyKinds.Account, "a1"), new Asset { OwnerKind = AssetOwnerKind.Account, OwnerId = "a1" }.ToParty());
        Assert.Equal(new PartyRef(PartyKinds.Contact, "c1"), new Asset { OwnerKind = AssetOwnerKind.Contact, OwnerId = "c1" }.ToParty());
        Assert.True(new Asset { OwnerKind = AssetOwnerKind.Internal, OwnerId = "x" }.ToParty().IsEmpty);
    }

    private sealed class Source(string kind, params (string Id, string Name)[] parties) : IPartySource
    {
        public int NameLookups { get; private set; }

        public string Kind => kind;

        public Task<IReadOnlyList<PartyOption>> SearchAsync(string query, int count, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<PartyOption>>([.. parties.Where(party => party.Name.Contains(query ?? string.Empty, StringComparison.OrdinalIgnoreCase)).Take(count).Select(party => new PartyOption(party.Id, party.Name))]);

        public Task<IReadOnlyDictionary<string, string>> GetNamesAsync(IReadOnlyCollection<string> ids, CancellationToken cancellationToken = default)
        {
            NameLookups++;

            return Task.FromResult<IReadOnlyDictionary<string, string>>(parties.Where(party => ids.Contains(party.Id)).ToDictionary(party => party.Id, party => party.Name));
        }
    }
}
