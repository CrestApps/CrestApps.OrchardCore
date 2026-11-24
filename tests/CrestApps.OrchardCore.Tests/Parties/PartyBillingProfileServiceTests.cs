using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Parties;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Parties.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Parties;

/// <summary>
/// A party's billing profile is built by every source of its kind, in registration order, a later source filling or
/// overriding an earlier one; a party no source names is named by the directory.
/// </summary>
public sealed class PartyBillingProfileServiceTests
{
    private static readonly PartyRef _account = new(PartyKinds.Account, "a1");

    [Fact]
    public async Task GetAsync_RunsTheSourcesOfTheKindInOrder_ALaterOneOverriding()
    {
        // Arrange
        var calls = new List<string>();
        var first = new Source(PartyKinds.Account, calls, "first", profile =>
        {
            profile.Contact = profile.Contact with { Name = "Contoso", Email = "first@example.com" };
            profile.TaxId = "OLD";
        });
        var second = new Source("account", calls, "second", profile =>
        {
            profile.TaxId = "GB123";
            profile.NetDays = 30;
        });
        var other = new Source(PartyKinds.Customer, calls, "other", profile => profile.TaxId = "WRONG");
        var service = new PartyBillingProfileService([first, other, second], new PartyDirectory([]));

        // Act
        var profile = await service.GetAsync(_account, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(["first", "second"], calls);
        Assert.Equal(_account, profile.Party);
        Assert.Equal("Contoso", profile.Contact.Name);
        Assert.Equal("first@example.com", profile.Contact.Email);
        Assert.Equal("GB123", profile.TaxId);
        Assert.Equal(30, profile.NetDays);
    }

    [Fact]
    public async Task GetAsync_NamesAPartyNoSourceNamed_FromTheDirectory()
    {
        // Arrange
        var directory = new PartyDirectory([new NamingSource(PartyKinds.Account, "a1", "Contoso Ltd")]);
        var service = new PartyBillingProfileService([new Source(PartyKinds.Account, [], "email", profile => profile.Contact = profile.Contact with { Email = "billing@contoso.test" })], directory);

        // Act
        var profile = await service.GetAsync(_account, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("Contoso Ltd", profile.Contact.Name);
        Assert.Equal("billing@contoso.test", profile.Contact.Email);
    }

    [Fact]
    public async Task GetAsync_GivesAProfile_ForAPartyNothingIsKnownAbout_AndNoneForAnEmptyReference()
    {
        // Arrange
        var service = new PartyBillingProfileService([], new PartyDirectory([]));

        // Act
        var unknown = await service.GetAsync(new PartyRef(PartyKinds.Contact, "c1"), TestContext.Current.CancellationToken);
        var empty = await service.GetAsync(default, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(unknown);
        Assert.NotNull(unknown.Contact);
        Assert.False(unknown.Contact.HasAnyValue);
        Assert.Empty(unknown.CopyTo);
        Assert.False(unknown.TaxExempt);
        Assert.Null(empty);
    }

    [Fact]
    public void AddCopyTo_LeavesOutBlankRepeatedAndTheContactsOwnAddress()
    {
        // Arrange
        var profile = new PartyBillingProfile { Contact = ContactInfo.From("Contoso", "billing@contoso.test", null) };

        // Act
        var added = new[]
        {
            profile.AddCopyTo("ap@contoso.test"),
            profile.AddCopyTo(" AP@contoso.test "),
            profile.AddCopyTo("Billing@Contoso.test"),
            profile.AddCopyTo(" "),
        };

        // Assert
        Assert.Equal([true, false, false, false], added);
        Assert.Equal(["ap@contoso.test"], profile.CopyTo);
    }

    [Fact]
    public void TheRegistrationHelpers_RegisterTheServicesOnce_AndEachSourceInOrder()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddPartyBillingProfileSource<FirstSource>();
        services.AddPartyBillingProfileSource<SecondSource>();
        services.AddPartyBillingProfileSource<FirstSource>();
        services.AddPartyMemberSource<MemberSource>();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        // Assert
        Assert.IsType<PartyBillingProfileService>(scope.ServiceProvider.GetRequiredService<IPartyBillingProfileService>());
        Assert.IsType<PartyMembershipService>(scope.ServiceProvider.GetRequiredService<IPartyMembershipService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IPartyDirectory>());
        Assert.Equal([typeof(FirstSource), typeof(SecondSource)], scope.ServiceProvider.GetServices<IPartyBillingProfileSource>().Select(source => source.GetType()));
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IPartyBillingProfileService));
    }

    private sealed class Source : IPartyBillingProfileSource
    {
        private readonly List<string> _calls;
        private readonly string _name;
        private readonly Action<PartyBillingProfile> _contribute;

        public Source(string kind, List<string> calls, string name, Action<PartyBillingProfile> contribute)
        {
            Kind = kind;
            _calls = calls;
            _name = name;
            _contribute = contribute;
        }

        public string Kind { get; }

        public Task ContributeAsync(PartyRef party, PartyBillingProfile profile, CancellationToken cancellationToken = default)
        {
            _calls.Add(_name);
            _contribute(profile);

            return Task.CompletedTask;
        }
    }

    private sealed class NamingSource : IPartySource
    {
        private readonly string _id;
        private readonly string _name;

        public NamingSource(string kind, string id, string name)
        {
            Kind = kind;
            _id = id;
            _name = name;
        }

        public string Kind { get; }

        public Task<IReadOnlyList<PartyOption>> SearchAsync(string query, int count, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<PartyOption>>([new PartyOption(_id, _name)]);

        public Task<IReadOnlyDictionary<string, string>> GetNamesAsync(IReadOnlyCollection<string> ids, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyDictionary<string, string>>(ids.Contains(_id) ? new Dictionary<string, string> { [_id] = _name } : new Dictionary<string, string>());
    }

    private sealed class FirstSource : IPartyBillingProfileSource
    {
        public string Kind => PartyKinds.Account;

        public Task ContributeAsync(PartyRef party, PartyBillingProfile profile, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class SecondSource : IPartyBillingProfileSource
    {
        public string Kind => PartyKinds.Account;

        public Task ContributeAsync(PartyRef party, PartyBillingProfile profile, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class MemberSource : IPartyMemberSource
    {
        public string Kind => PartyKinds.Account;

        public Task<IReadOnlyList<string>> GetMemberUserIdsAsync(PartyRef party, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<string>>([]);

        public Task<IReadOnlyList<PartyRef>> GetPartiesAsync(string userId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<PartyRef>>([]);
    }
}
