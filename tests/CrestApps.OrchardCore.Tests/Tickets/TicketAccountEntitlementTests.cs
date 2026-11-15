using CrestApps.OrchardCore.Accounts.Indexes;
using CrestApps.OrchardCore.Accounts.Models;
using CrestApps.OrchardCore.Accounts.Services;
using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.ServiceLevels.Models;
using CrestApps.OrchardCore.ServiceLevels.Services;
using CrestApps.OrchardCore.Tests.Calendars;
using CrestApps.OrchardCore.Tickets;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.Services;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.ContentManagement;
using YesSql;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Tickets;

/// <summary>
/// Accounts and tickets: an account's e-mail domains file a guest's request under the account (never when two accounts
/// list the domain), and the account's ticket service level runs its tickets before the desk's.
/// </summary>
public sealed class TicketAccountEntitlementTests
{
    [Theory]
    [InlineData("Dana@Contoso.COM", "contoso.com")]
    [InlineData("@contoso.com", "contoso.com")]
    [InlineData(" fabrikam.co.uk ", "fabrikam.co.uk")]
    [InlineData("localhost", null)]
    [InlineData("", null)]
    public void ADomain_IsKeptInOneForm(string value, string expected)
        => Assert.Equal(expected, AccountEmailDomainsNormalizer.Normalize(value));

    [Fact]
    public async Task AnAddress_FindsTheOneAccountListingItsDomain()
    {
        // Arrange
        await using var harness = await CalendarTestHarness.CreateAsync(
            services => services.AddScoped<IAccountEmailDomains, AccountEmailDomains>(),
            async schema => await schema.CreateMapIndexTableAsync<AccountEmailDomainIndex>(table => table
                .Column<string>("AccountContentItemId", column => column.WithLength(26))
                .Column<string>("Domain", column => column.WithLength(253))),
            [new AccountEmailDomainIndexProvider()]);
        await harness.RunAsync(async services =>
        {
            var session = services.GetRequiredService<ISession>();
            await session.SaveAsync(Account("acme", "contoso.com", "shared.example"));
            await session.SaveAsync(Account("other", "shared.example"));
        });

        // Act
        var found = await harness.RunAsync(async services =>
        {
            var domains = services.GetRequiredService<IAccountEmailDomains>();

            return new
            {
                Contoso = await domains.FindAccountIdAsync("dana@CONTOSO.com", TestContext.Current.CancellationToken),
                Shared = await domains.FindAccountIdAsync("lee@shared.example", TestContext.Current.CancellationToken),
                Unknown = await domains.FindAccountIdAsync("cy@nowhere.example", TestContext.Current.CancellationToken),
            };
        }, commit: false);

        // Assert
        Assert.Equal("acme", found.Contoso);
        Assert.Null(found.Shared);
        Assert.Null(found.Unknown);
    }

    [Fact]
    public async Task AnAccount_CantClaimAPublicMailDomain_OrOneAnotherAccountLists()
    {
        // Arrange
        await using var harness = await CalendarTestHarness.CreateAsync(
            services => services.AddScoped<IAccountEmailDomains, AccountEmailDomains>(),
            async schema => await schema.CreateMapIndexTableAsync<AccountEmailDomainIndex>(table => table
                .Column<string>("AccountContentItemId", column => column.WithLength(26))
                .Column<string>("Domain", column => column.WithLength(253))),
            [new AccountEmailDomainIndexProvider()]);
        await harness.RunAsync(async services => await services.GetRequiredService<ISession>().SaveAsync(Account("other", "fabrikam.com")));
        var options = Microsoft.Extensions.Options.Options.Create(new AccountEmailDomainsOptions { PublicDomains = ["mail.example"] });

        // Act
        var errors = await harness.RunAsync(async services =>
        {
            var handler = new CrestApps.OrchardCore.Accounts.Handlers.AccountEmailDomainsPartHandler(services.GetRequiredService<IAccountEmailDomains>(), options, Doubles.RecipeRoundTrip.Localizer<CrestApps.OrchardCore.Accounts.Handlers.AccountEmailDomainsPartHandler>());

            async Task<List<string>> ValidateAsync(ContentItem account)
            {
                var context = new global::OrchardCore.ContentManagement.Handlers.ValidateContentContext(account);
                await handler.ValidatingAsync(context, account.GetOrCreate<AccountEmailDomainsPart>());

                return [.. context.ContentValidateResult.Errors.Select(error => error.ErrorMessage)];
            }

            return new
            {
                Public = await ValidateAsync(Account("acme", "contoso.com", "Gmail.com".ToLowerInvariant())),
                Configured = await ValidateAsync(Account("acme", "mail.example")),
                Claimed = await ValidateAsync(Account("acme", "fabrikam.com")),
                OwnAgain = await ValidateAsync(Account("other", "fabrikam.com")),
                Fine = await ValidateAsync(Account("acme", "contoso.com")),
            };
        }, commit: false);

        // Assert
        Assert.Single(errors.Public);
        Assert.Contains("gmail.com", errors.Public[0], StringComparison.Ordinal);
        Assert.Single(errors.Configured);
        Assert.Single(errors.Claimed);
        Assert.Contains("fabrikam.com", errors.Claimed[0], StringComparison.Ordinal);
        Assert.Empty(errors.OwnAgain);
        Assert.Empty(errors.Fine);
    }

    [Fact]
    public async Task AGuestsUnconfirmedAddress_NeverFilesTheRequestUnderAnAccount()
    {
        // Arrange
        var domains = new Mock<IAccountEmailDomains>();
        domains.Setup(service => service.FindAccountIdAsync("dana@contoso.com", It.IsAny<CancellationToken>())).ReturnsAsync("acme");
        await using var kit = await TicketKit.CreateAsync(configureServices: services => services.AddSingleton(domains.Object));

        // Act
        var created = await kit.RunAsync(services => services.GetRequiredService<ITicketService>().CreateAsync(new TicketCreateRequest
        {
            Subject = "Can't print",
            Body = "It jams.",
            DeskId = "support",
            Channel = TicketsConstants.Channels.WebForm,
            RequesterContact = new ContactInfo { Name = "Dana", Email = "dana@contoso.com" },
            RequesterUnverified = true,
        }, TestContext.Current.CancellationToken));

        // Assert
        var part = (await kit.GetAsync(created.Ticket.ContentItemId)).GetOrCreate<TicketPart>();
        Assert.Null(part.AccountId);
        Assert.True(part.RequesterUnverified);
        domains.Verify(service => service.FindAccountIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AGuestsRequest_IsFiledUnderTheirAccount_AndRunsOnTheAccountsServiceLevel()
    {
        // Arrange
        const string premium = "4tkpremium000000000000000a";
        var domains = new Mock<IAccountEmailDomains>();
        domains.Setup(service => service.FindAccountIdAsync("dana@contoso.com", It.IsAny<CancellationToken>())).ReturnsAsync("acme");
        await using var kit = await TicketKit.CreateAsync(withPolicy: true, configureServices: services => services.AddSingleton(domains.Object));
        await kit.RunAsync(async services =>
        {
            var policies = services.GetRequiredService<IServiceLevelPolicyManager>();
            var policy = await policies.NewAsync();
            policy.ItemId = premium;
            policy.Name = "Premium";
            policy.Scope = TicketsConstants.ServiceLevelScope;
            policy.Targets = [new ServiceLevelTarget { Metric = ServiceLevelMetrics.FirstResponse, Minutes = 15 }];
            await policies.CreateAsync(policy);

            var account = Account("acme");
            account.Apply(new TicketAccountPart { ServiceLevelPolicyId = premium });
            await services.GetRequiredService<ISession>().SaveAsync(account);

            return true;
        });

        // Act
        var created = await kit.RunAsync(services => services.GetRequiredService<ITicketService>().CreateAsync(new TicketCreateRequest
        {
            Subject = "Can't print",
            Body = "It jams.",
            DeskId = "support",
            Channel = TicketsConstants.Channels.WebForm,
            RequesterContact = new ContactInfo { Name = "Dana", Email = "dana@contoso.com" },
        }, TestContext.Current.CancellationToken));

        // Assert
        var ticketId = created.Ticket.ContentItemId;
        Assert.Equal("acme", (await kit.GetAsync(ticketId)).GetOrCreate<TicketPart>().AccountId);
        Assert.All(await kit.TimersAsync(ticketId), timer => Assert.Equal(premium, timer.PolicyId));
    }

    private static ContentItem Account(string id, params string[] domains)
    {
        var item = new ContentItem { ContentItemId = id, ContentType = "Account", DisplayText = id, Published = true, Latest = true };
        item.Apply(new AccountEmailDomainsPart { Domains = [.. domains] });

        return item;
    }
}
